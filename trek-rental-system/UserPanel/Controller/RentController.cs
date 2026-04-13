using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Models.Rent;
using TTH.uirepository;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using TTH.Models.home;
using TTH.Models.user;
using TTH.Models;
using System;
using DocumentFormat.OpenXml.Spreadsheet;
using Razorpay.Api;
using TTH.Areas.Super.Data.Rent;
using TTH.Service;
using Org.BouncyCastle.Utilities.Collections;


namespace TTH.Controllers
{
    public class RentController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RentController> _logger;
        private readonly ProductRepository _productsRepository;
        private readonly AppDataContext _context;

        public RentController(ILogger<RentController> logger, ProductRepository productsRepository, AppDataContext context, UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _logger = logger;
            _productsRepository = productsRepository;
            _context = context;
        }

        public async Task<IActionResult> Index(int? trekId, string trekName, DateTime? startDate, DateTime? endDate, int departureId)
        {
            // Fetch all products

          
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);
            var products = (await _productsRepository.RentalPage()).Cast<ProductModel>().ToList();
            var userEmail = User.Identity.IsAuthenticated ? User.FindFirstValue(ClaimTypes.Email) : null;
            var productTotalPrices = new Dictionary<int, decimal>();

            // If user is authenticated and TrekId is provided, calculate product total prices
            if (userEmail != null && trekId.HasValue)
            {
                var trekDetail = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekId == trekId);

                if (trekDetail != null)
                {
                    // Fetch TrekRental to get Duration and StoreId
                    var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == trekId);
                    int duration = trekRental?.Duration ?? 0; // Use Duration from TrekRental
                    int? storeId = trekRental?.StoreId;

                    // Fetch InventoryItems to get PricePerDay for each product, filtered by StoreId
                    var inventoryItems = storeId.HasValue
                        ? await _context.InventoryItem.Where(i => i.Store_Id == storeId.Value && products.Select(p => p.Products_Id).Contains(i.Product_Id)).ToListAsync()
                        : new List<InventoryItem>();

                    foreach (var product in products)
                    {
                        var inventoryItem = inventoryItems.FirstOrDefault(i => i.Product_Id == product.Products_Id);
                        if (inventoryItem != null)
                        {
                            productTotalPrices[product.Products_Id] = duration * inventoryItem.PricePerDay;
                        }
                    }
                }
            }
            else
            {
                // Calculate PricePerDay for all products if TrekId is not provided
                var inventoryItems = await _context.InventoryItem
                    .Where(i => products.Select(p => p.Products_Id).Contains(i.Product_Id))
                    .ToListAsync();

                foreach (var product in products)
                {
                    var inventoryItem = inventoryItems.FirstOrDefault(i => i.Product_Id == product.Products_Id);
                    if (inventoryItem != null)
                    {
                        productTotalPrices[product.Products_Id] = inventoryItem.PricePerDay;
                    }
                }
            }

            List<ProductModel> filteredProducts = products; // Default to all products

            if (trekId.HasValue && startDate.HasValue && endDate.HasValue)
            {
                var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == trekId);
                int rentingWaiting = trekRental?.RentingWaiting ?? 0;
                int? storeId = trekRental?.StoreId;

                DateTime blockEndDate = endDate.Value.AddDays(rentingWaiting);

                var cartItems = await _context.CartItem
                    .Where(item => item.TrekName == trekName
                                   && item.StartDate == startDate
                                   && item.EndDate == endDate
                                   && item.DepartureId == departureId
                                   &&item.Email==userEmail
                                   )
                    .ToListAsync();

                var firstCartItem = cartItems.FirstOrDefault();

                if (storeId.HasValue)
                {
                    var inventoryItems = await _context.InventoryItem
                        .Where(i => i.Store_Id == storeId)
                        .ToListAsync();

                    filteredProducts = products
                        .Where(p => inventoryItems.Any(i => i.Product_Id == p.Products_Id))
                        .ToList();
                }

                var viewModel = new ViewModel
                {
                    Products = filteredProducts,
                    ProductTotalPrices = productTotalPrices,
                    TrekId = trekId,
                    TrekName = trekName,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    DepartureId = departureId,
                    BookingId = firstCartItem?.BookingId,
                    BlockEndDate = blockEndDate,
                    StoreId = storeId
                };

                return View(viewModel);
            }
            else
            {
                var viewModel = new ViewModel
                {
                    Products = products,
                    TrekId = trekId,
                    TrekName = trekName,
                    StartDate = startDate ?? DateTime.MinValue,
                    EndDate = endDate ?? DateTime.MinValue,
                    DepartureId = departureId,
                    BookingId = null,
                    StoreId = null,
                    ProductTotalPrices = productTotalPrices
                };

                return View(viewModel);
            }
        }










        public async Task<IActionResult> GetProductDetails(int id, int? trekId, string trekName, DateTime? startDate, DateTime? endDate, int departureId, DateTime blockEndDate, Dictionary<int, decimal> productTotalPrices)
        {
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);
            // Fetch product by ID
            var product = await _productsRepository.GetProductById(id);
            if (product == null)
            {
                return NotFound();
            }

            bool isTrekBooked = false;
            decimal totalPrice = product.PricePerDay;
            int duration = 0;
            int? storeId = null;

            // Get user email if authenticated
            string userEmail = User.Identity.IsAuthenticated ? User.FindFirstValue(ClaimTypes.Email) : null;

            if (trekId.HasValue)
            {
                // Fetch trek details
                var trekDetails = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekId == trekId);
                if (trekDetails != null)
                {
                    // Check if trek is booked
                    isTrekBooked = User.Identity.IsAuthenticated
                        ? await _context.TempParticipants
                            .AnyAsync(x => x.Email == userEmail && x.PaymentStatus == "Paid" && x.StartDate > DateTime.Now)
                        : false;

                    // Fetch trek rental information
                    var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == trekId);
                    duration = trekRental?.Duration ?? 0;
                    storeId = trekRental?.StoreId;

                    if (storeId.HasValue)
                    {
                        // Find the price for the product and store
                        var inventoryItem = await _context.InventoryItem.FirstOrDefaultAsync(i => i.Product_Id == id && i.Store_Id == storeId.Value);
                        if (inventoryItem != null)
                        {
                            // Calculate total price
                            totalPrice = isTrekBooked ? duration * inventoryItem.PricePerDay : inventoryItem.PricePerDay;
                                }
                    }
                }
            }

            // Prepare ProductViewModel
            var productViewModel = new ProductViewModel
            {
                Product = product,
                IsTrekBooked = isTrekBooked,
                TotalPrice = totalPrice,
                TrekId = trekId.HasValue ? trekId.Value : 0,
                TrekName = trekName,
                StartDate = startDate ?? DateTime.MinValue,
                EndDate = endDate ?? DateTime.MinValue,
                DepartureId = departureId,
                BlockEndDate = blockEndDate,
                ProductTotalPrices = productTotalPrices,
                UserEmail = userEmail // Set the user's email here
            };


            var homeViewModel = new ViewModel
            {
                // Populate this with data as needed
                Products = await _productsRepository.RentalPage(),
                // ... populate other properties
            };

            var combinedViewModel = new CombinedViewModel
            {
                ProductVM = productViewModel,
                HomeVM = homeViewModel
            };

            return View("GetProductDetails", combinedViewModel);
        }


        public async Task<IActionResult> RentTrekDetailsForm()
        {
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);
            if (!User.Identity.IsAuthenticated)
            {

                return RedirectToAction("Register", "Account", new { isrent = "Yes" });
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Register", "Account");
            }

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == userEmail);
            if (user == null)
            {
                return RedirectToAction("Register", "Account");
            }

            var currentDate = DateTime.Now.Date;

            // Retrieve trek bookings with BlockBeforeDays using a join
            var trekBookings = await (from temp in _context.TempParticipants
                                      join trek in _context.TrekRental
                                      on temp.TrekId equals trek.TrekId
                                      where temp.Email == userEmail && temp.PaymentStatus == "Paid"
                                      select new
                                      {
                                          temp.TrekId,
                                          temp.TrekName,
                                          temp.StartDate,
                                          temp.EndDate,
                                          FirstName = user.FirstName,
                                          LastName = user.LastName,
                                          temp.DepartureId,
                                          trek.BlockBeforeDays
                                      }).Distinct().ToListAsync();

            // Filter trek bookings based on BlockBeforeStartDate
            var filteredBookings = trekBookings
                .Select(b => new
                {
                    b.TrekId,
                    b.TrekName,
                    b.StartDate,
                    b.EndDate,
                    FirstName = b.FirstName,
                    LastName = b.LastName,
                    b.DepartureId,
                    BlockBeforeStartDate = b.StartDate.AddDays(-b.BlockBeforeDays)
                })
                .Where(b => b.BlockBeforeStartDate > currentDate)
                .Select(b => new RentTrekFormViewModel
                {
                    TrekId = b.TrekId,
                    TrekName = b.TrekName,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    FirstName = b.FirstName,
                    LastName = b.LastName,
                    DepartureId = b.DepartureId,
                })
                .ToList();

            if (!filteredBookings.Any())
            {
                TempData["Message"] = "No available treks with a valid start date.";
                return RedirectToAction("Index", "Rent");
            }

            return View("RentTrekDetailsForm", filteredBookings); // Correct

        }
        public IActionResult Privacy()
        {
            var userName = HttpContext.Session.GetString("UserName");
            ViewBag.UserName = userName;
            return View();
        }

        private async Task<ViewModel> GetViewModelWithTreksAndThemesAsync()
        {
            return new ViewModel()
            {
                Treks = await _context.TrekDetails
                    .Include(t => t.Location)
                    .ToListAsync(),
                Theme = await _context.Theme.ToListAsync()
            };
        }


    }
}