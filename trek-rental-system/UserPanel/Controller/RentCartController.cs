using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.VariantTypes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Newtonsoft.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.HPSF;
using Org.BouncyCastle.Asn1.X9;
using Razorpay.Api;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TTH.Areas.Super.Controllers;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Models.Rent;
using TTH.Data;
using TTH.Models;
using TTH.Models.booking;
using TTH.Models.user;
using TTH.Service;
using TTH.uirepository;

namespace TTH.Controllers
{
    [Route("RentCart")]
    public class RentCartController : Controller
    {
        private readonly AppDataContext _context;
        private readonly ProductRepository _productRepository;
        private readonly IGenerateRentBookingId _generateRentBookingId;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBravoMail _bravoMail;
        private readonly IRazorpayService razorpayService;
        private readonly ILogger<RentCartController> _logger;
        private readonly IConfiguration _configuration;

        public RentCartController(AppDataContext context, ProductRepository productRepository, UserManager<ApplicationUser> userManager, IGenerateRentBookingId generateRentBookingId, IRazorpayService razorpayService, IBravoMail bravoMail, IConfiguration configuration,ILogger<RentCartController> logger)
        {
            _context = context;
            _productRepository = productRepository;
            _userManager = userManager;
            _generateRentBookingId = generateRentBookingId;
            this.razorpayService = razorpayService;
            _logger = logger;
            _bravoMail = bravoMail;
            _configuration = configuration;

        }



        [HttpPost("CheckTrekId")]
        public async Task<JsonResult> CheckTrekId(
       int trekId,
       DateTime startDate,
       DateTime blockEndDate,
       int productId,
       int selectedSizeName,
       int quantity,
       string userEmail)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                var selectedSize = await _context.Size
                    .Where(s => s.IdSize == selectedSizeName)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(selectedSize))
                    return Json(new { success = false, message = "Invalid size selected." });

                string userId = user.Id;

                var trekRental = await _context.TrekRental
                    .FirstOrDefaultAsync(t => t.TrekId == trekId);

                if (trekRental == null)
                    return Json(new { success = false, redirectUrl = "/rent/renttrekdetailsform/" });

                int storeId = trekRental.StoreId;

                var storeInventoryItems = await _context.InventoryItem
                    .Where(i => i.Store_Id == storeId)
                    .ToListAsync();

                if (!storeInventoryItems.Any())
                    return Json(new { success = false, message = "No items found for this StoreId in InventoryItem." });

                int storeIdCount = storeInventoryItems.Count;

                var rentParticipantsWithStoreIdAndDateOverlap =
                    await _context.RentAllParticipant
                    .Where(r => r.StoreId == storeId &&
                        (
                            (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                            (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                            (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                            (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                        ))
                    .ToListAsync();

                int overlappingDateCountForStore = rentParticipantsWithStoreIdAndDateOverlap.Count;

                int sizeNameCount = 0;
                int totalQuantity = 0;
                int productIdCount = 0;

                foreach (var participant in rentParticipantsWithStoreIdAndDateOverlap)
                {
                    // ✅ SAFE parsing (FIX)
                    var productIds = ParseIntList(participant.ProductIds);
                    var productVarients = ParseStringList(participant.ProductVarients);
                    var quantities = ParseIntList(participant.Quantities);

                    int loopCount = Math.Min(productIds.Count,
                                    Math.Min(productVarients.Count, quantities.Count));

                    for (int i = 0; i < loopCount; i++)
                    {
                        if (productIds[i] == productId &&
                            string.Equals(participant.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
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

                var inventoryItems = await _context.InventoryItem
                    .Where(i => i.Product_Id == productId &&
                                i.SizeName == selectedSize &&
                                i.Store_Id == storeId)
                    .ToListAsync();

                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);

                var cartItems = await _context.CartItem
                    .Where(c => c.ProductId == productId &&
                                c.Variant == selectedSize &&
                                c.TrekId == trekId &&
                                c.StartDate.Date == startDate.Date &&
                                c.BlockEndDate.Date == blockEndDate.Date &&
                                c.UserId == userId)
                    .ToListAsync();

                var invalidCartItems = cartItems.Where(c => c.Quantity <= 0).ToList();
                if (invalidCartItems.Any())
                {
                    _context.CartItem.RemoveRange(invalidCartItems);
                    await _context.SaveChangesAsync();
                }

                cartItems = cartItems.Where(c => c.Quantity > 0).ToList();

                int cartTotalQuantity = cartItems.Sum(c => c.Quantity);
                int usedQuantity = totalQuantity + cartTotalQuantity;

                bool isQuantityAvailable = quantity <= (inventoryTotalQuantity - usedQuantity);

                return Json(new
                {
                    success = true,
                    storeId,
                    storeIdCount,
                    overlappingDateCountForStore,
                    productIdCount,
                    sizeNameCount,
                    totalQuantity,
                    cartTotalQuantity,
                    usedQuantity,
                    inventoryTotalQuantity,
                    quantityDifference = inventoryTotalQuantity - usedQuantity,
                    isQuantityAvailable,
                    userId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        [HttpPost("AddToCart")]
        public async Task<IActionResult> AddToCart(
     int productId,
     int quantity,
     int variant,
     int trekId,
     DateTime startDate,
     DateTime endDate,
     string trekName,
     bool isPaid,
     string userId,
     int departureId,
     DateTime blockEndDate)
        {
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);

            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return NotFound("User not found");

            string bookingId = await _generateRentBookingId.GetBookingId(user.Id, departureId, trekId);
            if (string.IsNullOrEmpty(bookingId))
                return BadRequest("Failed to generate a booking ID.");

            var product = await _productRepository.GetProductById(productId);
            if (product == null)
                return NotFound();

            var trekRental = await _context.TrekRental
                .FirstOrDefaultAsync(tr => tr.TrekId == trekId);

            if (trekRental == null)
                return NotFound("Trek rental details not found");

            var sizeName = await _context.Size
                .Where(s => s.IdSize == variant)
                .Select(s => s.Name)
                .FirstOrDefaultAsync() ?? "Unknown";

            var inventoryItem = await _context.InventoryItem
                .FirstOrDefaultAsync(i => i.Product_Id == productId
                    && i.SizeName == sizeName
                    && i.Store_Id == trekRental.StoreId);

            if (inventoryItem == null)
                return BadRequest("Selected product size not available in inventory.");

            decimal pricePerUnit = inventoryItem.PricePerDay;

            int alreadyInCart = (await _context.CartItem
                .Where(c => c.UserId == user.Id
                    && c.TrekName == trekName
                    && c.StartDate.Date == startDate.Date
                    && c.EndDate.Date == endDate.Date
                    && c.ProductId == productId
                    && c.Variant == sizeName)
                .SumAsync(c => (int?)c.Quantity)) ?? 0;

            int totalRequested = alreadyInCart + quantity;

            if (totalRequested > inventoryItem.Quantity)
                return BadRequest($"Only {inventoryItem.Quantity - alreadyInCart} items available for this size.");

            decimal mainTotalPrice = totalRequested * pricePerUnit;

            var existingCartItem = await _context.CartItem
                .FirstOrDefaultAsync(c => c.UserId == user.Id
                    && c.TrekName == trekName
                    && c.StartDate.Date == startDate.Date
                    && c.EndDate.Date == endDate.Date
                    && c.ProductId == productId
                    && c.Variant == sizeName);

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += quantity;
                existingCartItem.TotalPrice = existingCartItem.Quantity * pricePerUnit;
                existingCartItem.PaymentStatus = isPaid ? "Paid" : "Unpaid";
                existingCartItem.BlockEndDate = blockEndDate;

                _context.CartItem.Update(existingCartItem);
            }
            else
            {
                var cartItem = new CartItem
                {
                    ProductId = productId,
                    ProductName = product.ProductName,
                    Variant = sizeName,
                    Quantity = quantity,
                    TotalPrice = mainTotalPrice,
                    CoverImage = product.CoverImgUrl,
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    MobileNo = user.Mobile,
                    Email = user.Email,
                    TrekId = trekId,
                    TrekName = trekName,
                    StartDate = startDate,
                    EndDate = endDate,
                    OrderDate = DateTime.Now,
                    PaymentStatus = isPaid ? "Paid" : "Unpaid",
                    BookingId = bookingId,
                    DepartureId = departureId,
                    BlockEndDate = blockEndDate,
                    StoreId = trekRental.StoreId,
                    VariantId = variant,
                    BookingSource = "Web"
                };

                _context.CartItem.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            TempData["BookingId"] = bookingId;

            return RedirectToAction("Index", "Rent", new
            {
                trekId,
                trekName,
                startDate,
                endDate,
                departureId
            });
        }

        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            string trekName,
            int trekId,
            DateTime startDate,
            DateTime endDate,
            int departureId,
            DateTime blockEndDate)
        {
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);

            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return NotFound("User not found");

            var cartItems = await _context.CartItem
                .Where(item => item.UserId == user.Id
                               && item.TrekName == trekName
                               && item.StartDate == startDate
                               && item.EndDate == endDate
                               && item.DepartureId == departureId)
                .ToListAsync();

            if (!cartItems.Any())
                return View(new List<CartItemModel>());

            var trekRental = await _context.TrekRental
                .FirstOrDefaultAsync(t => t.TrekId == trekId);

            if (trekRental == null)
                return Json(new { success = false, redirectUrl = "/rent/renttrekdetailsform/" });

            int storeId = trekRental.StoreId;
            int duration = trekRental.Duration;

            var storeInventoryItems = await _context.InventoryItem
                .Where(i => i.Store_Id == storeId)
                .ToListAsync();

            if (!storeInventoryItems.Any())
                return Json(new { success = false, message = "No items found for this StoreId in the InventoryItem table." });

            var rentParticipantsWithStoreIdAndDateOverlap =
                await _context.RentAllParticipant
                .Where(r => r.StoreId == storeId &&
                    ((r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                     (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                     (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                     (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)))
                .ToListAsync();

            var cartItemModels = new List<CartItemModel>();

            foreach (var cartProduct in cartItems)
            {
                int totalBookedQuantity = 0;

                foreach (var participant in rentParticipantsWithStoreIdAndDateOverlap)
                {
                    if (participant.PaymentStatus != "Paid")
                        continue;

                    // ✅ SAFE JSON parsing (FIX)
                    var productIds = ParseIntList(participant.ProductIds);
                    var productVariants = ParseStringList(participant.ProductVarients);
                    var quantities = ParseIntList(participant.Quantities);

                    for (int i = 0; i < productIds.Count; i++)
                    {
                        if (productIds[i] == cartProduct.ProductId
                            && i < productVariants.Count
                            && productVariants[i].Equals(cartProduct.Variant, StringComparison.OrdinalIgnoreCase)
                            && i < quantities.Count)
                        {
                            totalBookedQuantity += quantities[i];
                        }
                    }
                }

                var inventoryItems = storeInventoryItems
                    .Where(i => i.Product_Id == cartProduct.ProductId
                                && i.SizeName == cartProduct.Variant)
                    .ToList();

                int inventoryTotalQuantity = inventoryItems.Sum(i => i.Quantity);
                int availableQuantity = inventoryTotalQuantity - totalBookedQuantity;

                if (availableQuantity <= 0 || cartProduct.Quantity <= 0)
                {
                    _context.CartItem.Remove(cartProduct);
                    continue;
                }

                int finalQuantity = Math.Min(cartProduct.Quantity, availableQuantity);
                decimal totalPrice = 0;

                foreach (var item in inventoryItems)
                {
                    totalPrice += item.PricePerDay * duration * finalQuantity;
                }

                cartProduct.Quantity = finalQuantity;
                cartProduct.TotalPrice = totalPrice;

                cartItemModels.Add(new CartItemModel
                {
                    CartItem_Id = cartProduct.CartItem_Id,
                    ProductId = cartProduct.ProductId,
                    ProductName = cartProduct.ProductName,
                    Variant = cartProduct.Variant,
                    Quantity = finalQuantity,
                    TotalPrice = totalPrice,
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

            await _context.SaveChangesAsync();

            decimal totalPriceWithGst =
                Math.Round(cartItemModels.Sum(x => x.TotalPrice) * 1.05m, 0,
                           MidpointRounding.AwayFromZero);

            ViewBag.GstPrice = totalPriceWithGst;
            ViewBag.BlockEndDate = blockEndDate;

            return View(cartItemModels);
        }


        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id, string trekName, int trekId, DateTime startDate, DateTime endDate, int departureId)
        {
            var itemToRemove = _context.CartItem.FirstOrDefault(item => item.CartItem_Id == id);
            if (itemToRemove != null)
            {
                _context.CartItem.Remove(itemToRemove);
                _context.SaveChanges();
            }

            // Redirect to Index with trekName, startDate, and endDate
            return RedirectToAction("Index", new { trekName = trekName, trekId = trekId, startDate = startDate, endDate = endDate, departureId = departureId });
        }

        [HttpPost("UpdateQuantityCheckTrekId")]
        public JsonResult UpdateQuantityCheckTrekId(
    int trekId,
    DateTime startDate,
    DateTime blockEndDate,
    int productId,
    int variantSize,
    int quantity,
    string bookingId)
        {
            try
            {
                var trekRental = _context.TrekRental.FirstOrDefault(t => t.TrekId == trekId);
                if (trekRental == null)
                    return Json(new { success = false, message = "TrekId not found." });

                int storeId = trekRental.StoreId;
                int duration = trekRental.Duration;

                var sizeName = _context.Size
                    .Where(s => s.IdSize == variantSize)
                    .Select(s => s.Name)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(sizeName))
                    return Json(new { success = false, message = "Invalid size selected." });

                var inventoryItem = _context.InventoryItem
                    .FirstOrDefault(i => i.Product_Id == productId &&
                                         i.SizeName == sizeName &&
                                         i.Store_Id == storeId);

                if (inventoryItem == null)
                    return Json(new { success = false, message = "Product or size not found in inventory." });

                decimal pricePerDay = inventoryItem.PricePerDay;
                decimal totalPricePerUnit = pricePerDay * duration;
                decimal updatedTotalPrice = totalPricePerUnit * quantity;

                var overlappingParticipants = _context.RentAllParticipant
                    .Where(r => r.StoreId == storeId &&
                        (
                            (r.StartDate.Date >= startDate.Date && r.StartDate.Date <= blockEndDate.Date) ||
                            (r.BlockEndDate.Date >= startDate.Date && r.BlockEndDate.Date <= blockEndDate.Date) ||
                            (startDate.Date >= r.StartDate.Date && startDate.Date <= r.BlockEndDate.Date) ||
                            (blockEndDate.Date >= r.StartDate.Date && blockEndDate.Date <= r.BlockEndDate.Date)
                        ))
                    .ToList();

                int totalBookedQuantity = 0;
                int productIdCount = 0;
                int sizeNameCount = 0;

                foreach (var participant in overlappingParticipants)
                {
                    // ✅ SAFE JSON parsing (FIX)
                    var productIds = ParseIntList(participant.ProductIds);
                    var productVariants = ParseStringList(participant.ProductVarients);
                    var quantities = ParseIntList(participant.Quantities);

                    int loopCount = Math.Min(productIds.Count,
                                    Math.Min(productVariants.Count, quantities.Count));

                    for (int i = 0; i < loopCount; i++)
                    {
                        if (productIds[i] == productId &&
                            string.Equals(participant.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                        {
                            productIdCount++;

                            if (productVariants[i].Equals(sizeName, StringComparison.OrdinalIgnoreCase))
                            {
                                sizeNameCount++;
                                totalBookedQuantity += quantities[i];
                            }
                        }
                    }
                }

                int inventoryTotalQuantity = _context.InventoryItem
                    .Where(i => i.Product_Id == productId &&
                                i.SizeName == sizeName &&
                                i.Store_Id == storeId)
                    .Sum(i => i.Quantity);

                int quantityDifference = Math.Max(inventoryTotalQuantity - totalBookedQuantity, 0);
                bool isQuantityAvailable = quantity <= quantityDifference;

                if (!isQuantityAvailable)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Requested quantity ({quantity}) exceeds available quantity ({quantityDifference}).",
                        isQuantityAvailable = false
                    });
                }

                CartItem cartItem = null;
                if (!string.IsNullOrEmpty(bookingId))
                {
                    cartItem = _context.CartItem
                        .FirstOrDefault(c => c.ProductId == productId &&
                                             c.BookingId == bookingId &&
                                             c.Variant == sizeName);
                }

                if (cartItem != null)
                {
                    cartItem.Quantity = quantity;
                    cartItem.TotalPrice = updatedTotalPrice;
                    _context.CartItem.Update(cartItem);
                }
                else
                {
                    _context.CartItem.Add(new CartItem
                    {
                        TrekId = trekId,
                        ProductId = productId,
                        Variant = sizeName,
                        Quantity = quantity,
                        TotalPrice = updatedTotalPrice,
                        BookingId = bookingId,
                        StoreId = storeId,
                        StartDate = startDate,
                        EndDate = blockEndDate
                    });
                }

                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    updatedTotalPrice = updatedTotalPrice.ToString("0.00"),
                    storeId,
                    overlappingDateCountForStore = overlappingParticipants.Count,
                    productIdCount,
                    sizeNameCount,
                    totalQuantity = totalBookedQuantity,
                    inventoryTotalQuantity,
                    quantityDifference,
                    isQuantityAvailable = true
                });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while updating the cart." });
            }
        }





       
        [Route("CreateBooking")]
        public async Task<IActionResult> CreateBooking(string bookingId)
        {
            if (string.IsNullOrWhiteSpace(bookingId))
                return Json(new { success = false, message = "Invalid bookingId." });

            var cartItems = await _context.CartItem
                .Where(c => c.BookingId == bookingId)
                .ToListAsync();

            if (!cartItems.Any())
                return Json(new { success = false, message = "No cart items found for this booking." });

            var first = cartItems.First();

            // SAFETY
            if (string.IsNullOrWhiteSpace(first.UserId))
                return Json(new { success = false, message = "User information missing." });

            decimal totalPrice = Math.Round(cartItems.Sum(c => c.TotalPrice) * 1.05m, 0, MidpointRounding.AwayFromZero);

   



          

            // Razorpay
            string transactionId = new IdGenerator(_context).GenerateTransactionId();
            string orderId = await razorpayService.CreateOrder(totalPrice, "INR", transactionId);

            _context.RazorPayOrderDetails.Add(new RazorPayModel
            {
                RazorpayOrderId = orderId,
                TransactionId = transactionId,
                Amount = totalPrice,
                BookingId = bookingId,
                PaymentStatus = "Unpaid",
                CreatedDate = DateTime.Now,
                PaymentSource = "Rent"
            });

            await _context.SaveChangesAsync();

            return Json(new { success = true, OrderId = orderId, Amount = totalPrice });
        }

        private string GenerateRazorpaySignature(string orderId, string paymentId)
        {
            string secret = "YOUR_SECRET_KEY"; // production key
            string payload = orderId + "|" + paymentId;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }


        [HttpPost]
        [Route("HandlePaymentSuccess")]
        public async Task<IActionResult> HandlePaymentSuccess(
            string razorpay_payment_id,
            string razorpay_order_id,
            string razorpay_signature,
            string bookingId,
            int departureId)
        {
            // ✅ Get logged-in userId safely (Fallback to cart data if needed)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1️⃣ Verify Signature
            string generatedSignature = GenerateRazorpaySignature(razorpay_order_id, razorpay_payment_id);
            if (!string.Equals(generatedSignature, razorpay_signature, StringComparison.Ordinal))
            {
                return Json(new { Success = false, Message = "Invalid payment signature." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 2️⃣ Razorpay Order Details update
                var razorOrder = await _context.RazorPayOrderDetails
                    .FirstOrDefaultAsync(r => r.RazorpayOrderId == razorpay_order_id);

                if (razorOrder != null)
                {
                    razorOrder.RazorpayPaymentId = razorpay_payment_id;
                    razorOrder.PaymentStatus = "Paid";
                }

                // 3️⃣ Fetch RentAllParticipant records (Existing check)
                var participants = await _context.RentAllParticipant
                    .Where(r => r.BookingId == bookingId)
                    .ToListAsync();

                // 4️⃣ Fetch CartItems (Raw data)
                var cartItems = await _context.CartItem
                    .Where(c => c.BookingId == bookingId)
                    .ToListAsync();

                if (!participants.Any() && !cartItems.Any())
                {
                    return BadRequest("Booking records not found.");
                }

                DateTime orderDate = DateTime.UtcNow;
                RentAllParticipant mainRecordForEmail = null;

                if (participants.Any())
                {
                    // Case: Record already exists (likely created by background worker or previous attempt)
                    // 🔒 CORRECT IDEMPOTENCY: Check if already paid
                    if (participants.All(p => p.PaymentStatus == "Paid"))
                    {
                        await transaction.RollbackAsync();
                        return Ok(new { Success = true, Message = "Already processed.", BookingId = bookingId });
                    }

                    foreach (var p in participants)
                    {
                        p.PaymentStatus = "Paid";
                        p.OrderDate = orderDate;
                        p.Note = "Updated via Controller";

                        // 🔴 Fix Blank Email during update
                        if (string.IsNullOrWhiteSpace(p.Email))
                        {
                            var cartEmail = cartItems.FirstOrDefault()?.Email;
                            p.Email = string.IsNullOrWhiteSpace(cartEmail) ? "not-provided@trekthehimalayas.com" : cartEmail.Trim();
                        }
                    }
                    mainRecordForEmail = participants.First();
                }
                else
                {
                    // Case: Record doesn't exist yet (Controller needs to create it)
                    var first = cartItems.First();
                    var trekRental = await _context.TrekRental.FirstOrDefaultAsync(t => t.TrekId == first.TrekId!.Value);
                    if (trekRental == null) throw new Exception("TrekRental config missing for TrekId: " + first.TrekId);

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
                        BookingId = bookingId,
                        DepartureId = departureId,
                        PaymentStatus = "Paid",
                        TrekId = first.TrekId.Value,
                        StoreId = trekRental.StoreId,
                        BlockEndDate = blockEndDate,
                        OrderDate = orderDate,
                        BookingSource = "Rent-Web"
                    };

                    _context.RentAllParticipant.Add(newRecord);
                    mainRecordForEmail = newRecord;
                }

                // 5️⃣ Clear cart
                if (cartItems.Any())
                {
                    _context.CartItem.RemoveRange(cartItems);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ================= EMAIL SECTION (FIXED FORMATTING) =================
                try
                {
                    if (mainRecordForEmail != null)
                    {
                        // Build ordered_items as array of objects for Brevo template
                        var orderedItemsArray = new JArray();
                        try
                        {
                            var names = JsonConvert.DeserializeObject<List<string>>(mainRecordForEmail.ProductNames) ?? new List<string>();
                            var counts = JsonConvert.DeserializeObject<List<int>>(mainRecordForEmail.Quantities) ?? new List<int>();
                            var sizes = JsonConvert.DeserializeObject<List<string>>(mainRecordForEmail.ProductVarients) ?? new List<string>();

                            for (int i = 0; i < names.Count; i++)
                            {
                                orderedItemsArray.Add(new JObject
                                {
                                    ["Name"] = names[i],
                                    ["Quantity"] = counts.Count > i ? counts[i] : 1,
                                    ["Variant"] = sizes.Count > i ? sizes[i] : "N/A"
                                });
                            }
                        }
                        catch
                        {
                            orderedItemsArray.Add(new JObject
                            {
                                ["Name"] = mainRecordForEmail.ProductNames ?? "",
                                ["Quantity"] = 1,
                                ["Variant"] = "N/A"
                            });
                        }

                        JObject Params = new JObject
                        {
                            ["trek_name"] = mainRecordForEmail.TrekName,
                            ["trek_date"] = mainRecordForEmail.StartDate.ToString("dd-MMM-yyyy"),
                            ["trekker_name"] = $"{mainRecordForEmail.FirstName} {mainRecordForEmail.LastName}",
                            ["order_date"] = mainRecordForEmail.OrderDate.ToString("dd-MMM-yyyy"),
                            ["ordered_items"] = orderedItemsArray,
                            ["total_amount"] = mainRecordForEmail.TotalAmount,
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
                        string adminEmail = _configuration["EmailConfiguration:ToRent"] ?? "rent@trekthehimalayas.com";
                        _bravoMail.SendEmail(senderName, senderEmail, adminEmail, mainRecordForEmail.FirstName, 34, Params);
                    }
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Email sending failed after payment success.");
                }

                return Ok(new { Success = true, Message = "Payment processed successfully.", BookingId = bookingId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Rent Payment Success Process Failed | BookingId: {bookingId}");
                return StatusCode(500, "Internal error processing payment success.");
            }
        }


        [HttpPost]
        [Route("RentCart/SearchTrekker")]
        public IActionResult SearchTrekker(string trekkerName)
        {
            if (string.IsNullOrEmpty(trekkerName))
            {
                return PartialView("_SearchResults", new List<RentAllParticipant>());
            }

            var searchResults = _context.RentAllParticipant
        .Where(ci =>
            (
                (ci.FirstName + " " + ci.LastName).Contains(trekkerName)
                || ci.Email.Contains(trekkerName)
                || ci.MobileNo.Contains(trekkerName)
            )
            && (ci.PaymentStatus == "Paid" || ci.PaymentStatus == "Offline")
        )
        .ToList();


            return PartialView("_SearchResults", searchResults);
        }

        [HttpGet]
        [Route("_SearchResults")]
        public IActionResult _SearchResults()
        {
            return View();
        }


        [HttpPost]
        [Route("SearchParticipants")]
        public async Task<IActionResult> SearchParticipants(int TrekId, int DepartureId)
        {
            var participants = await _context.RentAllParticipant
     .Where(p => p.TrekId == TrekId && p.DepartureId == DepartureId && (p.PaymentStatus == "Paid" || p.PaymentStatus == "Offline"))
     .OrderByDescending(p => p.OrderDate) // Replace `Id` with the appropriate column
     .ToListAsync();


            return PartialView("_SearchResults", participants);
        }
        [HttpGet]
        [Route("SearchOrders")]
        public ActionResult SearchOrders(DateTime? dateFrom, DateTime? dateTo, string orderStatus)
        {
            var filteredOrders = _context.RentAllParticipant
                .Where(ci => (ci.OrderDate >= dateFrom) &&
                               ci.OrderDate < dateTo.Value.AddDays(1) &&
                             (string.IsNullOrEmpty(orderStatus) || ci.PaymentStatus == orderStatus))
                .ToList();
            if (filteredOrders != null)
            {
                var totalAmount = filteredOrders.Sum(ci => ci.TotalAmount);

                ViewBag.TotalAmount = totalAmount;
            }

            return PartialView("_SearchResults", filteredOrders);
        }
        [HttpGet]
        [Route("ProductCount")]
        public async Task<IActionResult> ProductCount(int departureId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { productCount = 0 });
            }
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { productCount = 0 });
            }
            var productCount = await _context.CartItem
             .Where(a => a.Email == userEmail && a.DepartureId == departureId)
             .CountAsync();
            return Json(new { productCount });
        }



        [Route("RentPaymentSuccess")]
        public async Task<IActionResult> RentPaymentSuccess()
        {
            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);
            return View();
        }

        [Route("RentPaymentConfirm")]
        public async Task<IActionResult> RentPaymentConfirm(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
            {

                return RedirectToAction("ErrorPage", "Home");
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
            var totalAmount = participantDetailsList.Sum(item => item.TotalAmount);

            // Pass to ViewBag
            ViewBag.TotalAmount = totalAmount;

            var resultList = new List<OrderedProductViewModel>();

            foreach (var participant in participantDetailsList)
            {
                var productNames = JsonConvert.DeserializeObject<List<string>>(participant.ProductNames);
                var variantNames = JsonConvert.DeserializeObject<List<string>>(participant.ProductVarients);
                //var totalAmount = participant.TotalAmount;
                var quantities = JsonConvert.DeserializeObject<List<int>>(participant.Quantities);


                for (int i = 0; i < productNames.Count; i++)
                {
                    resultList.Add(new OrderedProductViewModel
                    {
                        ProductName = productNames[i],
                        VariantName = i < variantNames.Count ? variantNames[i] : "N/A",
                        TotalAmount = totalAmount,
                        Quantity = i < quantities.Count ? quantities[i] : 0
                    });
                }
            }




            ViewData["Treks"] = await TrekMenu.GetViewModelWithTreksAndThemesAsync(_context);

            return View(resultList);
        }
        [HttpGet]
        [Route("Error")]
        public IActionResult Error()
        {
            return View();
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

    }
}


