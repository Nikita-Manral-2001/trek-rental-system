using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.VariantTypes;
using DocumentFormat.OpenXml.Wordprocessing;
using Humanizer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.Scaffolding.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;
using Org.BouncyCastle.Asn1.Cmp;
using Razorpay.Api;
using sib_api_v3_sdk.Client;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TTH.Areas.Super.Controllers;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Data.TrekkersStore;
using TTH.Areas.Super.Models;
using TTH.Areas.Super.Models.Rent;
using TTH.Areas.Super.Models.TeamAttendance;
using TTH.Areas.Super.Models.TrekkersStore;
using TTH.Areas.Super.Models.user;
using TTH.Areas.Super.Repository;
using TTH.Models;
using TTH.Models.booking;
using TTH.Models.home;
using TTH.Models.Rent;
using TTH.Models.user;
using TTH.Service;
using TTH.uirepository;



namespace TTH.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentApiController : ControllerBase
    {

        private readonly AppDataContext _context;

        private readonly IGenerateRentBookingId _generateRentBookingId;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRazorpayService razorpayService;
    
        private readonly IConfiguration _configuration;
        private readonly IBravoMail _bravoMail;


        private readonly ILogger<RentApiController> _logger;
        private readonly ProductRepository _productsRepository;


        public RentApiController(AppDataContext context, IConfiguration configuration ,UserManager<ApplicationUser> userManager, IGenerateRentBookingId generateRentBookingId, IRazorpayService razorpayService, IBravoMail bravoMail, ProductRepository productsRepository, ILogger<RentApiController> logger)
        {

            _userManager = userManager;
            _generateRentBookingId = generateRentBookingId;
            _context = context;
            this.razorpayService = razorpayService;
            _configuration= configuration;
            _bravoMail = bravoMail;
            _productsRepository = productsRepository;
            _logger = logger;
        }




        [HttpGet("user-booked-trek-details")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UserBookedTrekDetails()
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
            {

                return NotFound(new { success = false, message = "UserNotFound." });
            }


            var user = await _context.Users
                        .Where(u => u.Id == userId)
                        .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { success = false, message = "UserNotFound." });
            }

            var userEmail = user.Email;

            if (userEmail == null)
            {
                return BadRequest(new { success = false, message = "User email not found. Please register." });
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
                                          trek.BlockBeforeDays,
                                          temp.BookingDate
                                      }).Distinct().ToListAsync();

            // Filter trek bookings based on BlockBeforeStartDate OR Grace Period
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
                    BlockBeforeStartDate = b.StartDate.Date.AddDays(-b.BlockBeforeDays),
                    b.BookingDate
                })
                .Where(b => b.BlockBeforeStartDate >= currentDate || (b.BookingDate.HasValue && b.BookingDate.Value.Date.AddDays(1) >= currentDate))
                .Select(b => new RentTrekFormViewModel
                {
                    TrekId = b.TrekId,
                    TrekName = b.TrekName,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    FirstName = b.FirstName,
                    LastName = b.LastName,
                    DepartureId = b.DepartureId,
                    BlockBeforeStartDate=b.BlockBeforeStartDate
                })
                .ToList();

            if (!filteredBookings.Any())
            {
                return Ok(new { status = false, message = "No trek bookings found.", });
            }
            return Ok(new { status = true, message = "Trek bookings retrieved successfully.", data = filteredBookings });


        }

        [HttpGet("get-all-products/{trekId?}/{departureId?}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetAllProducts(int? trekId, int? departureId)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
            {

                return NotFound(new { success = false, message = "UserNotFound." });
            }


            var user = await _context.Users
                        .Where(u => u.Id == userId)
                        .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { success = false, message = "UserNotFound." });
            }

            var userEmail = user.Email;

            var products = new List<ProductModel>();

            string trekName = string.Empty;
            DateTime? startDate = null;
            DateTime? endDate = null;




            if (trekId != null && departureId != null)
            {
                products = await _productsRepository.RentalPageApi(trekId.Value, departureId.Value);
                //var userEmail = User.Identity.IsAuthenticated ? User.FindFirstValue(ClaimTypes.Email) : null;



                List<ProductModel> filteredProducts = products; // Default to all products
                trekName = await _context.TrekDetails.Where(t => t.TrekId == trekId).Select(t => t.TrekName).FirstOrDefaultAsync();

                var departure = await _context.TrekDeparture.FirstOrDefaultAsync(t => t.DepartureId == departureId);
                if (departure == null || departure.StartDate == null || departure.EndDate == null)
                {
                    return NotFound(new { status = false, message = "Departure details not found or missing dates." });
                }

                startDate = departure.StartDate;
                endDate = departure.EndDate;

                var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == trekId);
                int rentingWaiting = trekRental?.RentingWaiting ?? 0;
                int? storeId = trekRental?.StoreId;

                DateTime blockEndDate = endDate.Value.AddDays(rentingWaiting);

                var cartItems = await _context.CartItem
                    .Where(item => item.TrekName == trekName
                                   && item.StartDate == startDate
                                   && item.EndDate == endDate
                                   && item.DepartureId == departureId
                                   && item.Email == userEmail
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

                var viewModel = new AllProductsViewModel
                {
                    Products = filteredProducts,
                    TrekId=trekId,
                    TrekName=trekName,
                    StartDate=startDate,
                    EndDate=endDate,
                    DepartureId=departureId.Value,
                    BlockEndDate = blockEndDate,


                };
                return Ok(new { status = true, data = viewModel });


            }
            else
            {
                products = await _productsRepository.RentalPage();
                var viewModel = new AllProductsViewModel
                {
                    Products = products,


                };

                return Ok(new { status = true, data = viewModel });
            }
        }


        [HttpGet]
        [Route("cart-product-count/{departureId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CartProductCount(int departureId)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
            {

                return NotFound(new { success = false, message = "UserNotFound." });
            }


            var user = await _context.Users
                        .Where(u => u.Id == userId)
                        .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { success = false, message = "UserNotFound." });
            }

            var userEmail = user.Email;

            if (userEmail == null)
            {
                return Ok(new { productCount = 0 });
            }
            var productCount = await _context.CartItem
             .Where(a => a.Email == userEmail && a.DepartureId == departureId)
             .CountAsync();
            return Ok(new { status = true, productCount });
        }


        [HttpGet]
        [Route("get-product-details/{id}/{departureId?}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetProductDetails(int id, int? departureId)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
            {

                return NotFound(new { success = false, message = "UserNotFound." });
            }


            var user = await _context.Users
                        .Where(u => u.Id == userId)
                        .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { success = false, message = "UserNotFound." });
            }

            var userEmail = user.Email;

            var productViewModelList = new List<ProductViewModel>();
            if (departureId != null)
            {
                int trekId = _context.TrekDeparture.Where(d => d.DepartureId == departureId).Select(d => d.TrekId).FirstOrDefault();

                DateTime startDate;
                DateTime endDate;
                var trekDeparture = await _context.TrekDeparture.FirstOrDefaultAsync(t => t.DepartureId == departureId);
                startDate = trekDeparture.StartDate;
                endDate = trekDeparture.EndDate;

                var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == trekId);
                int rentingWaiting = trekRental?.RentingWaiting ?? 0;
                int? storeId = trekRental?.StoreId;

                DateTime blockEndDate = endDate.AddDays(rentingWaiting);



                var product = await _productsRepository.GetProductById(id);
                if (product == null)
                {
                    return Ok(new { status = false, message = "Product not find." });
                }

                bool isTrekBooked = false;
                decimal totalPrice = product.PricePerDay;
                int duration = 0;
                int quantity = 0;



                string trekName = null;


                if (trekId != null)
                {

                    var trekDetails = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekId == trekId);
                    if (trekDetails != null)
                    {
                        trekName = trekDetails.TrekName;

                        isTrekBooked = await _context.TempParticipants
          .AnyAsync(x => x.Email == userEmail
                      && x.PaymentStatus == "Paid"
                      && x.StartDate > DateTime.Now);




                        duration = trekRental?.Duration ?? 0;
                        storeId = trekRental?.StoreId;

                        if (storeId.HasValue)
                        {

                            var inventoryItem = await _context.InventoryItem.FirstOrDefaultAsync(i => i.Product_Id == id && i.Store_Id == storeId.Value);
                            if (inventoryItem != null)
                            {
                                quantity = inventoryItem.Quantity;
                                totalPrice = (isTrekBooked ? duration * inventoryItem.PricePerDay : inventoryItem.PricePerDay);

                            }
                        }
                    }
                }


                productViewModelList.Add(new ProductViewModel
                {
                    Product = product,
                    IsTrekBooked = isTrekBooked,
                    TotalPrice = totalPrice,
                    TrekId = trekId,
                    TrekName = trekName,
                    StartDate = startDate,
                    EndDate = endDate,
                    DepartureId = departureId.Value,
                    BlockEndDate = blockEndDate,
                    UserEmail = userEmail,
                    Quantity=quantity
                });


            }

            else
            {
                var product = await _productsRepository.GetProductById(id);
                if (product == null)
                {
                    return Ok(new { status = false, message = "Product not find." });
                }

                productViewModelList.Add(new ProductViewModel
                {
                    Product = product,

                    UserEmail = userEmail,
                    TotalPrice=product.PricePerDay
                });
            }

            return Ok(new { status = true, data = productViewModelList });

        }


        [HttpGet("get-slider")]
        public async Task<IActionResult> GetSlider()
        {
            var sliders = await _context.RentSlider
    .OrderByDescending(s => s.Id)
    .ToListAsync();

            if (sliders == null || !sliders.Any())
            {
                return Ok(new { status = false, data = sliders, message = "Slider Not Found." });
            }
            return Ok(new { status = true, data = sliders });
        }


        [HttpGet]
        [Route("check-variant-quantity/{departureId}/{productId}/{selectedSizeName}/{quantity}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CheckVariantQuantity(int departureId, int productId, int selectedSizeName, int quantity)
        {

            try
            {

                var userId = User.FindFirstValue("userid");

                if (userId == null)
                {

                    return NotFound(new { success = false, message = "UserNotFound." });
                }


                var user = await _context.Users
                            .Where(u => u.Id == userId)
                            .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "UserNotFound." });
                }

                var userEmail = user.Email;

                // Find UserId from UserEmail
               
                if (user == null)
                {
                    return NotFound(new { status = false, message = "User not found." });
                }

               
                int trekId = _context.TrekDeparture.Where(d => d.DepartureId == departureId).Select(d => d.TrekId).FirstOrDefault();
                var selectedSize = await _context.Size
   .Where(s => s.IdSize == selectedSizeName)
   .Select(s => s.Name)
   .FirstOrDefaultAsync();
                // Check if the TrekId exists and retrieve the corresponding StoreId
                var trekRental = _context.TrekRental.FirstOrDefault(t => t.TrekId == trekId);
                if (trekRental == null)
                {
                    return NotFound(new { success = false, message = "Trek not found in store." });
                    // return Json(new { success = false, message = "TrekId not found." });
                }

                int storeId = trekRental.StoreId; // Get the StoreId from the TrekRental table

                // Check if the StoreId exists in the InventoryItem table
                var storeInventoryItems = _context.InventoryItem.Where(i => i.Store_Id == storeId).ToList();
                if (storeInventoryItems.Count == 0)
                {
                    return NotFound(new { status = false, message = "No items found for this StoreId." });
                }

                // Count how many items have this StoreId in the InventoryItem table

                DateTime startDate;
                DateTime endDate;
                var trekDeparture = await _context.TrekDeparture.FirstOrDefaultAsync(t => t.DepartureId == departureId);
                startDate = trekDeparture.StartDate;
                endDate = trekDeparture.EndDate;


                int rentingWaiting = trekRental?.RentingWaiting ?? 0;


                DateTime blockEndDate = endDate.AddDays(rentingWaiting);
                // Query RentAllParticipant to check for storeId and overlapping dates
                var rentParticipantsWithStoreIdAndDateOverlap = _context.RentAllParticipant
                    .Where(r => r.StoreId == storeId &&
                        (
                            (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                            (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                            (startDate.Date >= r.StartDate.Date && startDate <= r.BlockEndDate) ||
                            (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                        )
                    ).ToList();

                int overlappingDateCountForStore = rentParticipantsWithStoreIdAndDateOverlap.Count;

                // Existing logic to query RentAllParticipant for productId, sizeName, and quantity
                int sizeNameCount = 0;
                int totalQuantity = 0;
                int productIdCount = 0;

                foreach (var participant in rentParticipantsWithStoreIdAndDateOverlap)
                {
                    var productIds = JsonConvert.DeserializeObject<List<int>>(participant.ProductIds);
                    var productVarients = JsonConvert.DeserializeObject<List<string>>(participant.ProductVarients);
                    var quantities = JsonConvert.DeserializeObject<List<int>>(participant.Quantities);

                    for (int i = 0; i < productIds.Count; i++)
                    {
                        if (productIds[i] == productId && participant.PaymentStatus == "Paid") // Check PaymentStatus
                        {
                            productIdCount++;

                            if (productVarients[i].Equals(selectedSize, StringComparison.OrdinalIgnoreCase))
                            {
                                sizeNameCount++;
                                totalQuantity += quantities[i];
                            }
                        }
                    }
                }

                // Existing inventory calculation
                var inventoryItems = _context.InventoryItem
                    .Where(i => i.Product_Id == productId && i.SizeName == selectedSize && i.Store_Id == storeId)
                    .ToList();





                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);
                int quantityDifference = inventoryTotalQuantity - totalQuantity;

                // New logic to query CartItem table for the same product and size variant
                var cartItems = _context.CartItem
                    .Where(c => c.ProductId == productId &&
                                c.Variant == selectedSize &&
                                c.TrekId == trekId &&  // Adding check for TrekId
                                c.StartDate.Date == startDate.Date &&  // Adding check for StartDate
                                c.BlockEndDate.Date == blockEndDate.Date &&
                                 c.UserId == userId)  // Adding check for BlockEndDate
                    .ToList();

                int cartTotalQuantity = cartItems.Sum(c => c.Quantity);

                // Add the totalQuantity from RentAllParticipant and cartTotalQuantity from CartItem
                int usedQuantity = totalQuantity + cartTotalQuantity;

                // Check if the combined quantity exceeds the available inventory
                bool isQuantityAvailable = quantity <= (inventoryTotalQuantity - usedQuantity);


                // Return response including the usedQuantity and cart item quantities
                return Ok(new
                {
                    status = true,
                    storeId = storeId,

                    TotalRentproductCount = productIdCount,
                    TotalRentsizeCount = sizeNameCount,
                    TotalRentQuantity = totalQuantity,
                    TotalCartQuantity = cartTotalQuantity,
                    TotalusedQuantity = usedQuantity,
                    InventoryTotalQuantity = inventoryTotalQuantity,
                    RemainingQuantity = quantityDifference,
                    isQuantityAvailable = isQuantityAvailable,
                    userId = userId
                });
            }
            catch (Exception ex)
            {

                return NotFound(new { status = false, message = ex.Message });
            }
        }






        [HttpPost("add-to-cart")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = User.FindFirstValue("userid");

            if (string.IsNullOrEmpty(userId))
                return NotFound(new { success = false, message = "UserNotFound." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            if (string.IsNullOrEmpty(user.Email))
                return NotFound(new { status = false, message = "User email not found." });

            if (!dto.DepartureId.HasValue)
                return BadRequest(new { status = false, message = "You can't rent. Book your trek first." });

            var trekDeparture = await _context.TrekDeparture
                .FirstOrDefaultAsync(t => t.DepartureId == dto.DepartureId.Value);

            if (trekDeparture == null)
                return NotFound(new { status = false, message = "Invalid Departure." });

            DateTime startDate = trekDeparture.StartDate;
            DateTime endDate = trekDeparture.EndDate;
            int trekId = trekDeparture.TrekId;

            string trekName = await _context.TrekDetails
                .Where(t => t.TrekId == trekId)
                .Select(t => t.TrekName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(trekName))
                return NotFound(new { status = false, message = "Trek not found." });

            string bookingId = await _generateRentBookingId
                .GetBookingId(userId, dto.DepartureId.Value, trekId);

            if (string.IsNullOrEmpty(bookingId))
                return BadRequest(new { status = false, message = "Failed to generate BookingId." });

            var product = await _productsRepository.GetProductById(dto.ProductId);
            if (product == null)
                return NotFound(new { status = false, message = "Product not found." });

            var trekRental = await _context.TrekRental
                .FirstOrDefaultAsync(tr => tr.TrekId == trekId);

            if (trekRental == null)
                return NotFound(new { status = false, message = "Trek rental details not found." });

            var sizeName = await _context.Size
                .Where(s => s.IdSize == dto.SizeId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(sizeName))
                return NotFound(new { status = false, message = "Invalid size selected." });

            int rentingWaiting = trekRental.RentingWaiting;
            int storeId = trekRental.StoreId;
            int duration = trekRental.Duration;

            DateTime blockEndDate = endDate.AddDays(rentingWaiting);

            bool isTrekBooked = await _context.TempParticipants.AnyAsync(x =>
                x.Email == user.Email &&
                x.PaymentStatus == "Paid" &&
                x.StartDate > DateTime.Now);

            decimal totalPrice = product.PricePerDay;

            if (storeId!=null)
            {
                var inventoryItem = await _context.InventoryItem.FirstOrDefaultAsync(i =>
                    i.Product_Id == dto.ProductId &&
                    i.Store_Id == storeId);

                if (inventoryItem != null)
                    totalPrice = isTrekBooked ? duration * inventoryItem.PricePerDay : inventoryItem.PricePerDay;
            }

            var rentParticipants = await _context.RentAllParticipant
                .Where(r => r.StoreId == storeId &&
                    (
                        (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                        (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                        (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                        (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                    ))
                .ToListAsync();

            int totalQuantity = 0;

            foreach (var participant in rentParticipants)
            {
                List<int> productIds = participant.ProductIds?.Trim().StartsWith("[") == true
                    ? JsonConvert.DeserializeObject<List<int>>(participant.ProductIds)
                    : new List<int> { Convert.ToInt32(participant.ProductIds) };

                List<int> quantities = participant.Quantities?.Trim().StartsWith("[") == true
                    ? JsonConvert.DeserializeObject<List<int>>(participant.Quantities)
                    : new List<int> { Convert.ToInt32(participant.Quantities) };

                List<string> variants = participant.ProductVarients?.Trim().StartsWith("[") == true
                    ? JsonConvert.DeserializeObject<List<string>>(participant.ProductVarients)
                    : new List<string> { participant.ProductVarients };

                // 🔒 HARD SAFETY AGAINST INDEX ERROR
                int minCount = Math.Min(productIds.Count, Math.Min(quantities.Count, variants.Count));

                for (int i = 0; i < minCount; i++)
                {
                    if (productIds[i] == dto.ProductId &&
                        participant.PaymentStatus == "Paid" &&
                        !string.IsNullOrEmpty(variants[i]) &&
                        variants[i].Equals(sizeName, StringComparison.OrdinalIgnoreCase))
                    {
                        totalQuantity += quantities[i];
                    }
                }
            }

            var inventoryItems = await _context.InventoryItem
                .Where(i => i.Product_Id == dto.ProductId &&
                            i.SizeName == sizeName &&
                            i.Store_Id == storeId)
                .ToListAsync();

            int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);

            var cartItems = await _context.CartItem
                .Where(c => c.ProductId == dto.ProductId &&
                            c.Variant == sizeName &&
                            c.TrekId == trekId &&
                            c.StartDate.Date == startDate.Date &&
                            c.BlockEndDate.Date == blockEndDate.Date &&
                            c.UserId == userId)
                .ToListAsync();

            int cartTotalQuantity = cartItems.Sum(c => c.Quantity);
            int usedQuantity = totalQuantity + cartTotalQuantity;

            if (dto.Quantity > (inventoryTotalQuantity - usedQuantity))
                return BadRequest(new { status = false, message = "Quantity not available." });

            var existingCartItem = await _context.CartItem.FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.ProductId == dto.ProductId &&
                c.Variant == sizeName &&
                c.TrekId == trekId &&
                c.StartDate == startDate &&
                c.EndDate == endDate);

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += dto.Quantity;
                existingCartItem.TotalPrice = existingCartItem.Quantity * totalPrice;
                existingCartItem.BlockEndDate = blockEndDate;

                _context.CartItem.Update(existingCartItem);
                await _context.SaveChangesAsync();

                return Ok(new { status = true, message = "Cart quantity updated." });
            }

            var cartItem = new CartItem
            {
                ProductId = dto.ProductId,
                ProductName = product.ProductName,
                Variant = sizeName,
                Quantity = dto.Quantity,
                TotalPrice = dto.Quantity * totalPrice,
                CoverImage = product.CoverImgUrl,
                UserId = userId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MobileNo = user.Mobile,
                Email = user.Email,
                TrekId = trekId,
                TrekName = trekName,
                StartDate = startDate,
                EndDate = endDate,
                OrderDate = DateTime.Now,
                PaymentStatus = "Unpaid",
                BookingId = bookingId,
                DepartureId = dto.DepartureId.Value,
                BlockEndDate = blockEndDate,
                StoreId = storeId,
                VariantId = dto.SizeId,
                BookingSource = "app"
            };

            _context.CartItem.Add(cartItem);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, data = cartItem, message = "Product added to cart." });
        }


        [HttpGet("user-cart-items/{departureId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UserCartItems(int departureId)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            var userEmail = user.Email;
            if (string.IsNullOrEmpty(userEmail))
                return NotFound(new { status = false, message = "Product not found." });

            var trekDeparture = await _context.TrekDeparture
                .FirstOrDefaultAsync(t => t.DepartureId == departureId);

            if (trekDeparture == null)
                return NotFound(new { status = false, message = "Departure not found." });

            DateTime startDate = trekDeparture.StartDate;
            DateTime endDate = trekDeparture.EndDate;
            int trekId = trekDeparture.TrekId;

            string trekName = await _context.TrekDetails
                .Where(u => u.TrekId == trekId)
                .Select(u => u.TrekName)
                .FirstOrDefaultAsync();

            var cartItems = await _context.CartItem
                .Where(item =>
                    item.UserId == user.Id &&
                    item.TrekName == trekName &&
                    item.StartDate == startDate &&
                    item.EndDate == endDate &&
                    item.DepartureId == departureId)
                .ToListAsync();

            List<CartItemModel> cartItemModels = new List<CartItemModel>();

            if (!cartItems.Any())
                return Ok(new { status = true, Message = "CartItem is empty" });

            var trekRental = _context.TrekRental.FirstOrDefault(t => t.TrekId == trekId);
            if (trekRental == null)
                return NotFound(new { status = false, message = "TrekId not found." });

            int storeId = trekRental.StoreId;
            int duration = trekRental.Duration;

            var storeInventoryItems = _context.InventoryItem
                .Where(i => i.Store_Id == storeId)
                .ToList();

            if (!storeInventoryItems.Any())
                return NotFound(new { status = false, message = "No items found for this StoreId in the InventoryItem table." });

            int rentingWaiting = trekRental.RentingWaiting;
            DateTime blockEndDate = endDate.AddDays(rentingWaiting);

            var rentParticipantsWithStoreIdAndDateOverlap = _context.RentAllParticipant
                .Where(r =>
                    r.StoreId == storeId &&
                    (
                        (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                        (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                        (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                        (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                    ))
                .ToList();

            foreach (var cartProduct in cartItems)
            {
                int totalQuantity = 0;

                foreach (var participant in rentParticipantsWithStoreIdAndDateOverlap)
                {
                    // 🔒 SAFE JSON DESERIALIZATION (FIX)
                    var productIds = SafeIntList(participant.ProductIds);
                    var productVarients = SafeStringList(participant.ProductVarients);
                    var quantities = SafeIntList(participant.Quantities);

                    for (int i = 0; i < productIds.Count; i++)
                    {
                        if (productIds[i] == cartProduct.ProductId &&
                            participant.PaymentStatus == "Paid" &&
                            productVarients.Count > i &&
                            quantities.Count > i &&
                            productVarients[i].Equals(cartProduct.Variant, StringComparison.OrdinalIgnoreCase))
                        {
                            totalQuantity += quantities[i];
                        }
                    }
                }

                var inventoryItems = _context.InventoryItem
                    .Where(i =>
                        i.Store_Id == storeId &&
                        i.Product_Id == cartProduct.ProductId &&
                        i.SizeName == cartProduct.Variant)
                    .ToList();

                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);
                int quantityDifference = inventoryTotalQuantity - totalQuantity;
                bool isQuantityAvailable = quantityDifference >= cartProduct.Quantity && cartProduct.Quantity != 0;

                decimal updatedTotalPrice = 0;
                foreach (var item in inventoryItems)
                {
                    updatedTotalPrice = item.PricePerDay * duration * quantityDifference;
                }

                if (!isQuantityAvailable)
                {
                    if (quantityDifference <= 0)
                    {
                        _context.CartItem.Remove(cartProduct);
                    }
                    else
                    {
                        cartProduct.Quantity = quantityDifference;
                        cartProduct.TotalPrice = updatedTotalPrice;
                    }

                    await _context.SaveChangesAsync();
                }

                cartItemModels.Add(new CartItemModel
                {
                    CartItem_Id = cartProduct.CartItem_Id,
                    ProductId = cartProduct.ProductId,
                    ProductName = cartProduct.ProductName,
                    Variant = cartProduct.Variant,
                    Quantity = isQuantityAvailable ? cartProduct.Quantity : quantityDifference,
                    TotalPrice = isQuantityAvailable ? cartProduct.TotalPrice : updatedTotalPrice,
                    CoverImage = cartProduct.CoverImage,
                    UserId = cartProduct.UserId,
                    FirstName = cartProduct.FirstName,
                    LastName = cartProduct.LastName,
                    MobileNo = cartProduct.MobileNo,
                    Email = cartProduct.Email,
                    TrekId = cartProduct.TrekId,
                    TrekName = cartProduct.TrekName,
                    StartDate = cartProduct.StartDate,
                    EndDate = cartProduct.EndDate,
                    DepartureId = cartProduct.DepartureId,
                    BookingId = cartProduct.BookingId,
                    VariantId = cartProduct.VariantId
                });
            }

            decimal AllProductsPrice = cartItemModels.Sum(p => p.TotalPrice);
            decimal gstRate = 5m;
            decimal gstPrice = AllProductsPrice * gstRate / 100;
            int gstRounded = (int)Math.Round(gstPrice, 0);
            decimal totalPricewithGST = AllProductsPrice + gstRounded;

            return Ok(new
            {
                status = true,
                data = new
                {
                    cartItems = cartItemModels,
                    gstTotalPrice = totalPricewithGST,
                    totalPrice = AllProductsPrice,
                    gstPrice = gstRounded
                }
            });
        }

        [HttpPost("quantity-decrease")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> QuantityDecrease([FromBody] QunatitityIncrementDto dto)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
            {

                return NotFound(new { success = false, message = "UserNotFound." });
            }
            try
            {
                var trekDeparture = await _context.TrekDeparture.FirstOrDefaultAsync(t => t.DepartureId == dto.departureId);
                if (trekDeparture == null)
                {
                    return NotFound(new { status = false, message = "Departure not found." });
                }
                DateTime startDate = trekDeparture.StartDate;
                DateTime endDate = trekDeparture.EndDate;
                int trekId = trekDeparture.TrekId;
                // Check if the TrekId exists and retrieve the corresponding StoreId and Duration
                var trekRental = _context.TrekRental
                    .FirstOrDefault(t => t.TrekId == trekId);

                if (trekRental == null)
                {
                    return NotFound(new { status = false, message = "TrekId not found." });
                }

                int storeId = trekRental.StoreId; // Get the StoreId from the TrekRental table
                int duration = trekRental.Duration; // Get the Duration from the TrekRental table

                var sizeName = _context.Size
    .Where(s => s.IdSize == dto.variantSize)
    .Select(s => s.Name)
    .FirstOrDefault();
                // Get the price for the specific product and size
                var inventoryItem = _context.InventoryItem
                    .FirstOrDefault(i => i.Product_Id == dto.productId && i.SizeName == sizeName && i.Store_Id == storeId);

                if (inventoryItem == null)
                {
                    return NotFound(new { status = false, message = "Product or size not found in inventory." });
                }

                decimal pricePerDay = inventoryItem.PricePerDay;
                decimal TotalPrice = pricePerDay * duration;
                decimal price = TotalPrice * dto.quantity;



                var inventoryItems = _context.InventoryItem
                    .Where(i => i.Product_Id == dto.productId && i.SizeName == sizeName && i.Store_Id == storeId)
                    .ToList();

                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);


                var cartItem = _context.CartItem
                    .FirstOrDefault(c => c.ProductId == dto.productId && c.BookingId == dto.bookingId && c.Variant == sizeName);

                if (cartItem != null)
                {
                    cartItem.Quantity -= dto.quantity; // decrease quantity
                    cartItem.TotalPrice -= price;      // decrease total price
                    _context.CartItem.Update(cartItem);
                }
                else
                {
                    return NotFound(new { status = false, message = "Cart item not found." });
                }

                await _context.SaveChangesAsync();
                var cartItems = await _context.CartItem
        .Where(item => item.UserId == userId

                       && item.DepartureId == dto.departureId)
        .ToListAsync();
                decimal AllProductsPrice = cartItems.Sum(p => p.TotalPrice);
                decimal gstRate = 5m;
                decimal gstPrice = AllProductsPrice * gstRate / 100;
                int gstRounded = (int)Math.Round(gstPrice, 0);
                decimal totalPricewithGST = AllProductsPrice + gstRounded;

                return Ok(new
                {
                    success = true,
                    totalPrice = AllProductsPrice,
                    gsttotalPrice = totalPricewithGST,
                    gstPrice = gstRounded,
                    storeId = storeId,

                    totalQuantity = cartItem.Quantity,
                    inventoryTotalQuantity = inventoryTotalQuantity,
                    Message = "Quantity Decrease"
                });

            }
            catch (Exception ex)
            {
                return NotFound(new { status = false, message = ex.Message });
            }
        }


        [HttpPost("check-quantity-availability")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> CheckQuantityAvailability([FromBody] QunatitityIncrementDto dto)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            try
            {
                var trekDeparture = await _context.TrekDeparture
                    .FirstOrDefaultAsync(t => t.DepartureId == dto.departureId);

                if (trekDeparture == null)
                    return NotFound(new { status = false, message = "Departure not found." });

                DateTime startDate = trekDeparture.StartDate;
                DateTime endDate = trekDeparture.EndDate;
                int trekId = trekDeparture.TrekId;

                var trekRental = _context.TrekRental
                    .FirstOrDefault(t => t.TrekId == trekId);

                if (trekRental == null)
                    return NotFound(new { status = false, message = "TrekId not found." });

                int storeId = trekRental.StoreId;
                int duration = trekRental.Duration;

                var sizeName = _context.Size
                    .Where(s => s.IdSize == dto.variantSize)
                    .Select(s => s.Name)
                    .FirstOrDefault();

                var inventoryItem = _context.InventoryItem
                    .FirstOrDefault(i =>
                        i.Product_Id == dto.productId &&
                        i.SizeName == sizeName &&
                        i.Store_Id == storeId);

                if (inventoryItem == null)
                    return NotFound(new { status = false, message = "Product or size not found in inventory." });

                decimal pricePerDay = inventoryItem.PricePerDay;
                decimal TotalPrice = pricePerDay * duration;
                decimal updatedTotalPrice = TotalPrice * dto.quantity;

                int rentingWaiting = trekRental.RentingWaiting;
                DateTime blockEndDate = endDate.AddDays(rentingWaiting);

                var rentParticipantsWithStoreIdAndDateOverlap = _context.RentAllParticipant
                    .Where(r =>
                        r.StoreId == storeId &&
                        (
                            (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                            (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                            (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                            (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                        ))
                    .ToList();

                int overlappingDateCountForStore = rentParticipantsWithStoreIdAndDateOverlap.Count;

                int totalQuantity = 0;
                int productIdCount = 0;
                int sizeNameCount = 0;

                foreach (var participant in rentParticipantsWithStoreIdAndDateOverlap)
                {
                    // 🔒 SAFE DESERIALIZATION (FIX ONLY)
                    var productIds = SafeIntList(participant.ProductIds);
                    var productVarients = SafeStringList(participant.ProductVarients);
                    var quantities = SafeIntList(participant.Quantities);

                    for (int i = 0; i < productIds.Count; i++)
                    {
                        if (productIds[i] == dto.productId && participant.PaymentStatus == "Paid")
                        {
                            productIdCount++;

                            if (productVarients.Count > i &&
                                quantities.Count > i &&
                                productVarients[i].Equals(sizeName, StringComparison.OrdinalIgnoreCase))
                            {
                                sizeNameCount++;
                                totalQuantity += quantities[i];
                            }
                        }
                    }
                }

                var inventoryItems = _context.InventoryItem
                    .Where(i =>
                        i.Product_Id == dto.productId &&
                        i.SizeName == sizeName &&
                        i.Store_Id == storeId)
                    .ToList();

                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);
                int quantityDifference = inventoryTotalQuantity - totalQuantity;
                bool isQuantityAvailable = dto.quantity <= quantityDifference;

                if (isQuantityAvailable)
                {
                    var cartItem = _context.CartItem
                        .FirstOrDefault(c =>
                            c.ProductId == dto.productId &&
                            c.BookingId == dto.bookingId &&
                            c.Variant == sizeName);

                    if (cartItem != null)
                    {
                        cartItem.Quantity += dto.quantity;
                        cartItem.TotalPrice += updatedTotalPrice;
                        _context.CartItem.Update(cartItem);
                    }
                    else
                    {
                        cartItem = new CartItem // 🔒 ASSIGN TO VARIABLE TO AVOID NULL EXCEPTION BELOW
                        {
                            TrekId = trekId,
                            ProductId = dto.productId,
                            Variant = sizeName,
                            Quantity = dto.quantity,
                            TotalPrice = updatedTotalPrice,
                            VariantId = dto.variantSize,
                            UserId = userId,
                            BookingId = dto.bookingId
                        };
                        _context.CartItem.Add(cartItem);
                    }

                    await _context.SaveChangesAsync();

                    var cartItems = await _context.CartItem
                        .Where(item =>
                            item.UserId == userId &&
                            item.DepartureId == dto.departureId)
                        .ToListAsync();

                    decimal AllProductsPrice = cartItems.Sum(p => p.TotalPrice);
                    decimal gstRate = 5m;
                    decimal gstPrice = AllProductsPrice * gstRate / 100;
                    int gstRounded = (int)Math.Round(gstPrice, 0);
                    decimal totalPricewithGST = AllProductsPrice + gstRounded;

                    return Ok(new
                    {
                        success = true,
                        updatedProductPrice = cartItem.TotalPrice,
                        totalPrice = AllProductsPrice,
                        gsttotalPrice = totalPricewithGST,
                        gstPrice = gstRounded,
                        storeId = storeId,
                        overlappingDateCountForStore = overlappingDateCountForStore,
                        productIdCount = productIdCount,
                        sizeNameCount = sizeNameCount,
                        totalQuantity = cartItem.Quantity,
                        inventoryTotalQuantity = inventoryTotalQuantity,
                        quantityDifference = quantityDifference,
                        isQuantityAvailable = true
                    });
                }
                else
                {
                    return NotFound(new
                    {
                        status = false,
                        message = $"Requested quantity ({dto.quantity}) exceeds available quantity ({quantityDifference}).",
                        isQuantityAvailable = false
                    });
                }
            }
            catch (Exception ex)
            {
                return NotFound(new { status = false, message = ex.Message });
            }
        }


        [HttpPost("rent-delete/{cartItemId}")]

        public IActionResult RentDelete(int cartItemId)
        {
            var itemToRemove = _context.CartItem.FirstOrDefault(item => item.CartItem_Id == cartItemId);
            if (itemToRemove != null)
            {
                _context.CartItem.Remove(itemToRemove);
                _context.SaveChanges();
            }
            return Ok(new { status = true, data = itemToRemove, Message = "Product is deleted" });

        }




        [Route("create-booking/{bookingId}")]
        public async Task<IActionResult> CreateBooking(string bookingId)
        {
            var cartItems = await _context.CartItem
              .Where(c => c.BookingId == bookingId)
              .ToListAsync();

            if (!cartItems.Any())
                return Ok(new { success = false, message = "No cart items found for this booking." });


            var first = cartItems.First();

            decimal totalPrice = cartItems.Sum(c => c.TotalPrice);
  
            decimal gstRate = 5m;
            decimal gstPrice = totalPrice * gstRate / 100;
            int gstRounded = (int)Math.Round(gstPrice, 0);
            decimal totalPricewithGST = totalPrice + gstRounded;
      

            var Rentparticipant = await _context.RentAllParticipant.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            if (Rentparticipant != null)
            {
                _context.RentAllParticipant.Remove(Rentparticipant);
            }

          


            //Payment Gateway 

            var amount = totalPricewithGST;
            try
            {
                Random random = new Random();
                IdGenerator idGenerator = new IdGenerator(_context);
                string transactionId = idGenerator.GenerateTransactionId();

                string orderId = await razorpayService.CreateOrder(amount, "INR", transactionId);

                var razorpayOrderDetails = new RazorPayModel()
                {
                    RazorpayOrderId = orderId,
                    TransactionId = transactionId,
                    Amount = amount,
                    BookingId = bookingId,
                    PaymentStatus = "Unpaid",
                    CreatedDate = DateTime.Now,
                    PaymentSource = "Rent",

                };

                _context.RazorPayOrderDetails.Add(razorpayOrderDetails);
                await _context.SaveChangesAsync();

                // Update booking details
                var bookingDetails = await _context.CartItem.Where(b => b.BookingId == bookingId).ToListAsync();
                foreach (var booking in bookingDetails)
                {
                    booking.OrderDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    OrderId = orderId,
                    totalPrice = amount,
                    gstPrice = gstRounded,
                    gsttotalPrice = totalPricewithGST,
                    BookingId = bookingId,
                    UserId = first.UserId

                });
            }
            catch (Exception ex)
            {
                // Log the exception
                return Ok(new { status = false, message = "Error creating booking." });
            }
        }
      


        private string GenerateRazorpaySignature(string orderId, string paymentId)
        {
            // Use your Razorpay secret key to generate the signature
            /*     string secret = "9Abv5143BuIm8jpOQxzdxwm6";*///testkey
            string secret = "Hctq0JOfkx8ziVyXzoZKzaxI";
            string payload = orderId + "|" + paymentId;

            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        [HttpPost]
        [Route("handle-payment-success")]
        public async Task<IActionResult> HandlePaymentSuccess([FromBody] HandlePaymentDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1️⃣ Verify Signature
                string generatedSignature = GenerateRazorpaySignature(dto.razorpay_order_id, dto.razorpay_payment_id);
                if (!string.Equals(generatedSignature, dto.razorpay_signature, StringComparison.Ordinal))
                {
                    return Ok(new { Success = false, Message = "Invalid payment signature." });
                }

                // 2️⃣ Razorpay Order Details update
                var razorOrder = await _context.RazorPayOrderDetails
                    .FirstOrDefaultAsync(r => r.RazorpayOrderId == dto.razorpay_order_id);

                if (razorOrder != null)
                {
                    razorOrder.RazorpayPaymentId = dto.razorpay_payment_id;
                    razorOrder.PaymentStatus = "Paid";
                }

                // 3️⃣ Fetch RentAllParticipant records
                var participants = await _context.RentAllParticipant
                    .Where(r => r.BookingId == dto.bookingId)
                    .ToListAsync();

                // 4️⃣ Fetch CartItems
                var cartItems = await _context.CartItem
                    .Where(c => c.BookingId == dto.bookingId)
                    .ToListAsync();

                // 🔴 Fix Missing Product Names for API (Cart items from API often miss ProductName)
                foreach(var c in cartItems)
                {
                    if (string.IsNullOrWhiteSpace(c.ProductName))
                    {
                        var prod = await _context.Products.FirstOrDefaultAsync(px => px.Products_Id == c.ProductId);
                        if (prod != null) c.ProductName = prod.ProductName;
                    }
                }

                if (!participants.Any() && !cartItems.Any())
                {
                    return Ok(new { Success = false, Message = "Booking records not found." });
                }

                DateTime orderDate = DateTime.UtcNow;
                RentAllParticipant mainRecordForEmail = null;

                if (participants.Any())
                {
                    // Case: Record already exists
                    if (participants.All(p => p.PaymentStatus == "Paid"))
                    {
                        await transaction.RollbackAsync();
                        return Ok(new { Success = true, Message = "Payment already processed.", BookingId = dto.bookingId });
                    }

                    foreach (var p in participants)
                    {
                        p.PaymentStatus = "Paid";
                        p.OrderDate = orderDate;
                        p.Note = "Updated via API";
                        p.BookingSource = dto.bookingSource;

                        if (string.IsNullOrWhiteSpace(p.Email))
                        {
                            var cartEmail = cartItems.FirstOrDefault()?.Email;
                            p.Email = string.IsNullOrWhiteSpace(cartEmail) ? "not-provided@trekthehimalayas.com" : cartEmail.Trim();
                        }

                        // 🔴 Fix missing product details in existing record
                        if (string.IsNullOrWhiteSpace(p.ProductNames) || p.ProductNames.Contains("null") || p.ProductNames == "[]")
                        {
                            p.ProductNames = JsonConvert.SerializeObject(cartItems.Select(x => x.ProductName));
                            p.ProductIds = JsonConvert.SerializeObject(cartItems.Select(x => x.ProductId));
                            p.Quantities = JsonConvert.SerializeObject(cartItems.Select(x => x.Quantity));
                            p.ProductVarients = JsonConvert.SerializeObject(cartItems.Select(x => x.Variant));
                        }
                    }
                    mainRecordForEmail = participants.First();
                }
                else
                {
                    // Case: Record doesn't exist (Create it)
                    var first = cartItems.First();
                    var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == first.TrekId!.Value);
                    if (trekRental == null) throw new Exception("TrekRental config missing");

                    DateTime blockEndDate = first.EndDate.AddDays(trekRental.RentingWaiting);
                    decimal totalPrice = Math.Round(cartItems.Sum(c => c.TotalPrice) * 1.05m, 0, MidpointRounding.AwayFromZero);
                    var newRecord = new RentAllParticipant
                    {
                        TrekName = first.TrekName ?? "Trek",
                        StartDate = first.StartDate,
                        EndDate = first.EndDate,
                        UserId = first.UserId,
                        FirstName = first.FirstName ?? "",
                        LastName = first.LastName ?? "",
                        Email = string.IsNullOrWhiteSpace(first.Email) ? "not-provided@trekthehimalayas.com" : first.Email.Trim(),
                        MobileNo = first.MobileNo,
                        TotalAmount = totalPrice,
                        ProductNames = JsonConvert.SerializeObject(cartItems.Select(x => x.ProductName)),
                        ProductIds = JsonConvert.SerializeObject(cartItems.Select(x => x.ProductId)),
                        Quantities = JsonConvert.SerializeObject(cartItems.Select(x => x.Quantity)),
                        ProductVarients = JsonConvert.SerializeObject(cartItems.Select(x => x.Variant)),
                        BookingId = dto.bookingId,
                        DepartureId = dto.departureId,
                        PaymentStatus = "Paid",
                        TrekId = first.TrekId.Value,
                        StoreId = trekRental.StoreId,
                        BlockEndDate = blockEndDate,
                        OrderDate = orderDate,
                        BookingSource = "app"
                    };

                    _context.RentAllParticipant.Add(newRecord);
                    mainRecordForEmail = newRecord;
                }

                // 5️⃣ Clear cart & Commit
                if (cartItems.Any())
                {
                    _context.CartItem.RemoveRange(cartItems);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6️⃣ Send Emails (wrapped in try-catch so email failure won't break success response)
                try
                {
                    if (mainRecordForEmail != null)
                    {
                        // 🔴 FIX: Build ordered_items directly from `cartItems` to guarantee no blank data
                        var orderedItemsArray = new JArray();
                        if (cartItems != null && cartItems.Any())
                        {
                            foreach (var cartItem in cartItems)
                            {
                                orderedItemsArray.Add(new JObject
                                {
                                    ["Name"] = string.IsNullOrWhiteSpace(cartItem.ProductName) ? "Rental Gear" : cartItem.ProductName,
                                    ["Quantity"] = cartItem.Quantity > 0 ? cartItem.Quantity : 1,
                                    ["Variant"] = string.IsNullOrWhiteSpace(cartItem.Variant) ? "N/A" : cartItem.Variant
                                });
                            }
                        }
                        else
                        {
                            // Absolute Fallback
                            orderedItemsArray.Add(new JObject { ["Name"] = "Rental Products", ["Quantity"] = 1, ["Variant"] = "N/A" });
                        }

                        // 🔴 FIX: Safeguard TotalAmount against 0 or blank values
                        decimal finalAmount = mainRecordForEmail.TotalAmount > 0 
                                              ? mainRecordForEmail.TotalAmount 
                                              : (cartItems != null && cartItems.Any() ? Math.Round(cartItems.Sum(c => c.TotalPrice) * 1.05m, 0, MidpointRounding.AwayFromZero) : 0);

                        JObject Params = new JObject
                        {
                            ["trek_name"] = mainRecordForEmail.TrekName ?? "Trek",
                            ["trek_date"] = mainRecordForEmail.StartDate.ToString("dd-MMM-yyyy"),
                            ["trekker_name"] = $"{mainRecordForEmail.FirstName} {mainRecordForEmail.LastName}".Trim(),
                            ["order_date"] = mainRecordForEmail.OrderDate.ToString("dd-MMM-yyyy"),
                            ["ordered_items"] = orderedItemsArray,
                            ["total_amount"] = finalAmount.ToString("0.00"), // Formatting string avoids precision loss or blanking by Brevo
                            ["orderid"] = mainRecordForEmail.BookingId,
                            ["status"] = "Paid"
                        };

                        string senderName = _configuration["EmailConfiguration:SenderName"] ?? "TTH";
                        string senderEmail = _configuration["EmailConfiguration:InfoEmailSender"] ?? "info@trekthehimalayas.com";

                        // Send User Email
                        if (!string.IsNullOrWhiteSpace(mainRecordForEmail.Email))
                        {
                            _bravoMail.SendEmail(senderName, senderEmail, mainRecordForEmail.Email, mainRecordForEmail.FirstName, 33, Params);
                        }

                        // Send Admin Email
                        string adminEmail = _configuration["EmailConfiguration:ToRent"] ?? "store@trekthehimalayas.com";
                        _bravoMail.SendEmail(senderName, senderEmail, adminEmail, mainRecordForEmail.FirstName, 34, Params);
                    }
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Email sending failed after payment success.");
                }

                return Ok(new { Success = true, Message = "Payment successful.", Data=mainRecordForEmail });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"API Payment success handler failed | BookingId: {dto.bookingId}");
                return Ok(new { Success = false, Message = "Internal error." });
            }
        }


        [HttpPost("get-products")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetProducts([FromBody] QunatitityIncrementDto dto)
        {
            var userId = User.FindFirstValue("userid");
            DateTime currentDate = DateTime.Now;

            if (userId == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            var data = await _context.RentAllParticipant
                .Where(p => p.UserId == userId && p.StartDate >= currentDate)
                .ToListAsync();

            var result = new List<object>();
         
            foreach (var item in data)
            {
                var productNames = string.IsNullOrEmpty(item.ProductNames)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(item.ProductNames);

                var productsize = string.IsNullOrEmpty(item.ProductVarients) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(item.ProductVarients);
                var quantities = string.IsNullOrEmpty(item.Quantities)
                    ? new List<int>()
                    : JsonConvert.DeserializeObject<List<int>>(item.Quantities);

                for (int i = 0; i < productNames.Count; i++)
                {
                    result.Add(new
                    {
                        DepartureId = item.DepartureId,
                        ProductName = productNames[i],
                        size = productsize[i],
                        Quantity = i < quantities.Count ? quantities[i] : 0
                    });
                }
            }

            return Ok(result);
        }


        [HttpGet("get-autocomplete-rent")]
        public IActionResult GetAutocompleteRent([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest(new { message = "Search term is required." });
            }

            var suggestions = _context.Products
      .Where(t => EF.Functions.Like(t.ProductName, $"%{term}%"))
      .Select(t => new
      {
          ProductName = t.ProductName,
          ProductId = t.Products_Id
      })
      .Distinct()
      .ToList();
            if (suggestions == null)
            {
                return Ok(new
                {
                    status = false,


                });
            }

            return Ok(new
            {
                status = true,

                data = suggestions
            });
        }
        [HttpGet("rent-payment-confirm")]
        public async Task<IActionResult> RentPaymentConfirm(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
            {
                return BadRequest(new { message = "BookingId is required" });
            }

            var participantDetailsList = await _context.RentAllParticipant
                .Where(c => c.BookingId == bookingId)
                .Select(c => new
                {
                    c.ProductNames,
                    c.ProductVarients,
                    c.TotalAmount,
                    c.Quantities
                })
                .ToListAsync();

            if (!participantDetailsList.Any())
            {
                return NotFound(new { message = "No data found for this bookingId" });
            }

            var totalAmount = participantDetailsList.Sum(item => item.TotalAmount);

            var resultList = new List<OrderedProductViewModel>();

            foreach (var participant in participantDetailsList)
            {
                var productNames = JsonConvert.DeserializeObject<List<string>>(participant.ProductNames);
                var variantNames = JsonConvert.DeserializeObject<List<string>>(participant.ProductVarients);
                var quantities = JsonConvert.DeserializeObject<List<int>>(participant.Quantities);

                for (int i = 0; i < productNames.Count; i++)
                {
                    resultList.Add(new OrderedProductViewModel
                    {
                        ProductName = productNames[i],
                        VariantName = i < variantNames.Count ? variantNames[i] : "N/A",
                        Quantity = i < quantities.Count ? quantities[i] : 0,
                        TotalAmount = totalAmount
                    });
                }
            }

            return Ok(new
            {
                BookingId = bookingId,
                TotalAmount = totalAmount,
                Products = resultList
            });
        }

        [HttpPost("cancellation-rent-product")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> cancellationrentproduct(int departureid)
        {
            var userId = User.FindFirstValue("userid");

            if (userId == null)
                return NotFound(new { success = false, message = "UserNotFound." });

            var rentproduct = await _context.RentAllParticipant
                .Where(p => p.UserId == userId && p.DepartureId == departureid)
                .FirstOrDefaultAsync();

            if (rentproduct == null)
                return NotFound(new { success = false, message = "No record found." });

            JObject Params = new JObject
            {
                ["trekker_name"] = rentproduct.FirstName ?? "Trekker",
                ["trekker_email"] = rentproduct.Email ?? "",
                ["treka_name"] = rentproduct.TrekName ?? "",
                ["trek_startdate"] = rentproduct.StartDate.ToString("dd-MMM-yyyy"),
                ["trek_enddate"] = rentproduct.EndDate.ToString("dd-MMM-yyyy"),
                ["orderid"] = rentproduct.BookingId ?? "",
                ["mobile_no"]=rentproduct.MobileNo??"",
            };

            string senderName = _configuration["EmailConfiguration:SenderName"];
            string senderEmail = _configuration["EmailConfiguration:InfoEmailSender"];

            // ✅ User Email
            if (!string.IsNullOrWhiteSpace(rentproduct.Email))
            {
                _bravoMail.SendEmail(senderName, senderEmail, rentproduct.Email, rentproduct.FirstName, 468, Params);
            }

            // ✅ Admin Email
            string adminEmail = "nikitamanral89@gmail.com";
            //string adminEmail = _configuration["EmailConfiguration:ToRent"];
            _bravoMail.SendEmail(senderName, senderEmail, adminEmail, rentproduct.FirstName, 469, Params);

            return Ok(new { success = true, message = "Cancellation email sent." });
        }
        private List<int> ParseIntList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<int>();

            value = value.Trim();

            // JSON array
            if (value.StartsWith("["))
                return JsonConvert.DeserializeObject<List<int>>(value) ?? new List<int>();

            // Single number
            return new List<int> { Convert.ToInt32(value) };
        }

        private List<string> ParseStringList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            value = value.Trim();

            // JSON array
            if (value.StartsWith("["))
                return JsonConvert.DeserializeObject<List<string>>(value) ?? new List<string>();

            // Single string
            return new List<string> { value };
        }

        private List<int> SafeIntList(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<int>();

            json = json.Trim();

            if (!json.StartsWith("["))
                return new List<int> { Convert.ToInt32(json) };

            return JsonConvert.DeserializeObject<List<int>>(json);
        }


        private List<string> SafeStringList(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            json = json.Trim();

            if (!json.StartsWith("["))
                return new List<string> { json };

            return JsonConvert.DeserializeObject<List<string>>(json);
        }

    }
}
