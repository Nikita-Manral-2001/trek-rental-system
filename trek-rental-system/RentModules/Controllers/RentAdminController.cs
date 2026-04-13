using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Evaluation;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using System.Diagnostics;
using System.Security.Claims;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Models;
using TTH.Areas.Super.Models.Booking;
using TTH.Areas.Super.Models.Rent;
using TTH.Areas.Super.Repository;
using TTH.Areas.Super.Repository.RentRepository;
using TTH.Models.user;
using TTH.Service;


namespace TTH.Areas.Super.Controllers
{
    [Area("Super")]
    [Route("super/[controller]")]
    [Authorize(Roles = "Super,RentUser")]
    public class RentAdminController : Controller
    {
        private readonly AdminProductsRepository _adminProductsRepository;
        private readonly SizeRepository _sizeRepository;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly AppDataContext _context;
        private readonly ITrekRepository _trekRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGenerateRentBookingId _generateRentBookingId;
        private readonly IDepartureRepository _departureRepository;
        private readonly IBravoMail _bravoMail;

        public RentAdminController(AdminProductsRepository adminProductsRepository, SizeRepository sizeRepository, IWebHostEnvironment webHostEnvironment, AppDataContext context, UserManager<ApplicationUser> userManager, ITrekRepository trekRepository, IGenerateRentBookingId generateRentBookingId, IDepartureRepository departureRepository ,IBravoMail bravoMail)
        {
            _adminProductsRepository = adminProductsRepository;
            _sizeRepository = sizeRepository;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
            _bravoMail = bravoMail;
            _userManager = userManager;
            _trekRepository = trekRepository;
            _generateRentBookingId = generateRentBookingId;
            _departureRepository = departureRepository;
        }

        private async Task<string> SaveImage(string folderName1, string folderName2, IFormFile file)
        {

            string folderPath = $"images/{(folderName1)}/{folderName2}/";

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string serverFilePath = Path.Combine(_webHostEnvironment.WebRootPath, folderPath, uniqueFileName);

            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(serverFilePath));

            using (var stream = new FileStream(serverFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/" + folderPath + uniqueFileName;
        }


        [HttpGet]
        [Route("AddProducts")]
        public async Task<ViewResult> AddProducts(bool isSuccess = false)
        {
            ViewBag.Size = new SelectList(await _sizeRepository.GetSize(), "IdSize", "Name");
            ViewBag.IsSuccess = isSuccess;
            return View();
        }
        [HttpPost]
        [Route("AddProducts")]
        public async Task<IActionResult> AddProducts(ProductModel productModel)
        {
            if (ModelState.IsValid)
            {
                if (productModel.CoverPhoto != null)
                {
                    string folder = "products/Cover/";
                    productModel.CoverImgUrl = await UploadImage(folder, productModel.CoverPhoto);
                }

                if (productModel.GalleryPhoto != null)
                {
                    string folder = "products/Gallery/";
                    productModel.Gallery = new List<GalleryModel>();
                    foreach (var file in productModel.GalleryPhoto)
                    {
                        var gallery = new GalleryModel()
                        {
                            Name = file.Name,
                            URL = await UploadImage(folder, file)
                        };
                        productModel.Gallery.Add(gallery);
                    }
                }

                int id = await _adminProductsRepository.AddProducts(productModel);

                if (id > 0)
                {
                    return RedirectToAction(nameof(AddProducts), new { isSuccess = true });
                }
            }

            ViewBag.Size = new SelectList(await _sizeRepository.GetSize(), "IdSize", "Name");
            return View();
        }

        private async Task<string> UploadImage(string folderPath, IFormFile file)
        {
            folderPath += Guid.NewGuid().ToString() + "_" + file.FileName;

            string serverFolder = Path.Combine(_webHostEnvironment.WebRootPath, folderPath);
            await file.CopyToAsync(new FileStream(serverFolder, FileMode.Create));
            return "/" + folderPath;
        }

        [HttpGet]
        [Route("EditProduct")]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _adminProductsRepository.GetProductByid(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Size = new SelectList(await _sizeRepository.GetSize(), "IdSize", "Name", product.SelectedSizes);
            return View(product);
        }

        [HttpPost]
        [Route("EditProduct")]
        public async Task<IActionResult> EditProduct(ProductModel productModel)
        {
            if (ModelState.IsValid)
            {
                if (productModel.CoverPhoto != null)
                {
                    string folder = "products/Cover/";
                    productModel.CoverImgUrl = await UploadImage(folder, productModel.CoverPhoto);
                }

                if (productModel.GalleryPhoto != null)
                {
                    string folder = "products/Gallery/";
                    productModel.Gallery = new List<GalleryModel>();
                    foreach (var file in productModel.GalleryPhoto)
                    {
                        var gallery = new GalleryModel()
                        {
                            Name = file.Name,
                            URL = await UploadImage(folder, file)
                        };

                        productModel.Gallery.Add(gallery);
                    }
                }

                await _adminProductsRepository.UpdateProduct(productModel);
                return RedirectToAction("EditProducts");
            }

            ViewBag.Size = new SelectList(await _sizeRepository.GetSize(), "IdSize", "Name", productModel.SelectedSizes);
            return View(productModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            var product = _context.Products.Find(id);
            if (product != null)
            {

                _context.Products.Remove(product);
                _context.SaveChanges();
            }

            return RedirectToAction("EditProducts");
        }
        [HttpGet]
        [Route("EditProducts")]
        public async Task<IActionResult> EditProducts()
        {
            var products = await _adminProductsRepository.RentalPage();
            return View(products);
        }

        [HttpPost]
        [Route("DeleteGalleryImage/{id}")]
        public async Task<IActionResult> DeleteGalleryImage(int id)
        {
            try
            {
                var galleryImage = await _adminProductsRepository.GetGalleryImageById(id);
                if (galleryImage != null)
                {
                    await _adminProductsRepository.DeleteGalleryImage(galleryImage);
                    // Redirect to EditProduct action with the same product id
                    return RedirectToAction("EditProduct", new { id = galleryImage.ProductId });
                }
                else
                {
                    // Handle case where gallery image with given id is not found
                    return RedirectToAction("EditProduct");
                }
            }
            catch (Exception)
            {
                // Handle any exceptions that might occur during deletion
                // Log the exception or provide appropriate error handling
                return RedirectToAction("EditProduct");
            }
        }

        [HttpGet]
        [Route("SizeList")]
        public async Task<IActionResult> SizeList()
        {
            var sizes = await _sizeRepository.GetSize();
            return View(sizes);
        }

        [HttpPost]
        [Route("AddSize")]
        public async Task<IActionResult> AddSize(SizeModel sizeModel)
        {
            if (ModelState.IsValid)
            {
                await _sizeRepository.AddSize(sizeModel);
                return RedirectToAction(nameof(SizeList));
            }
            return View(nameof(SizeList), await _sizeRepository.GetSize());
        }

        [HttpPost]
        [Route("DeleteSize")]
        public async Task<IActionResult> DeleteSize(int id)
        {
            await _sizeRepository.DeleteSize(id);
            return RedirectToAction(nameof(SizeList));
        }

        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var errorModel = new ErrorViewModel
            {
                RequestId = requestId
            };
            return View(errorModel);
        }

        [HttpGet]
        [Route("Stores")]
        public async Task<IActionResult> Stores()
        {
            var stores = await _adminProductsRepository.GetStores();
            return View(stores);
        }

        [HttpPost]
        [Route("AddStore")]
        public async Task<IActionResult> AddStore(StoresModel storeModel)
        {
            if (ModelState.IsValid)
            {
                await _adminProductsRepository.AddStore(storeModel);
                return RedirectToAction("Stores");
            }

            var stores = await _adminProductsRepository.GetStores();
            return View("Stores", stores);
        }


        [HttpGet]
        [Route("EditStore/{id}")]
        public async Task<IActionResult> EditStore(int id)
        {
            var store = await _adminProductsRepository.GetStoreById(id);
            if (store == null)
            {
                return NotFound();
            }
            return View(store);
        }
        [HttpPost]
        [Route("EditStore")]
        public async Task<IActionResult> EditStore(StoresModel storeModel)
        {
            if (ModelState.IsValid)
            {
                await _adminProductsRepository.UpdateStore(storeModel);
                return RedirectToAction("Stores");
            }
            return View(storeModel);
        }



        [HttpPost]
        [Route("DeleteStore/{id}")]
        public async Task<IActionResult> DeleteStore(int id)
        {
            var success = await _adminProductsRepository.DeleteStore(id);
            if (success)
            {
                return RedirectToAction("Stores");
            }
            // Handle the case when delete fails
            return BadRequest();
        }

        [HttpGet]
        [Route("RentTrek")]
        public async Task<IActionResult> RentTrek()
        {
            var treks = await _adminProductsRepository.GetAllTreks();
            var stores = await _adminProductsRepository.GetStores();
            var rentals = await _adminProductsRepository.GetAllRentals();

            ViewBag.Treks = new SelectList(treks, "TrekName", "TrekName");
            ViewBag.Stores = new SelectList(stores, "StoreName", "StoreName");

            return View(rentals);
        }

        [HttpPost]
        [Route("RentTrek")]
        public async Task<IActionResult> RentTrek(TrekRentalModel trekRentalModel)
        {
            if (ModelState.IsValid)
            {
                // Find the TrekId based on TrekName
                var trek = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekName == trekRentalModel.TrekName);
                if (trek == null)
                {
                    ModelState.AddModelError("TrekName", "Invalid Trek Name");
                    return View(trekRentalModel);
                }

                // Find the StoreId based on StoreName
                var store = await _context.Store.FirstOrDefaultAsync(s => s.StoreName == trekRentalModel.StoreName);
                if (store == null)
                {
                    ModelState.AddModelError("StoreName", "Invalid Store Name");
                    return View(trekRentalModel);
                }

                // Set the TrekId and StoreId
                var trekRental = new TrekRental
                {
                    TrekId = trek.TrekId,
                    StoreId = store.Store_Id,
                    TrekName = trekRentalModel.TrekName,
                    StoreName = trekRentalModel.StoreName,
                    RentingWaiting = trekRentalModel.RentingWaiting,
                    BlockBeforeDays = trekRentalModel.BlockBeforeDays,
                    Duration = trekRentalModel.Duration,
                    // Add other necessary properties from trekRentalModel
                };

                // Save the trek rental to the database
                _context.TrekRental.Add(trekRental);
                await _context.SaveChangesAsync();

                return RedirectToAction("RentTrek", new { isSuccess = true });
            }

            // Reload the Treks and Stores for the dropdowns
            var treks = await _adminProductsRepository.GetAllTreks();
            var stores = await _adminProductsRepository.GetStores();

            ViewBag.Treks = new SelectList(treks, "TrekName", "TrekName");
            ViewBag.Stores = new SelectList(stores, "StoreName", "StoreName");

            return View(trekRentalModel);
        }




        [HttpGet]
        [Route("GetTrekDetailsByName/{trekName}")]
        public async Task<IActionResult> GetTrekDetailsByName(string trekName)
        {
            if (string.IsNullOrEmpty(trekName))
            {
                return BadRequest("Trek name is required.");
            }

            var trek = await _adminProductsRepository.GetTrekByName(trekName);
            if (trek == null)
            {
                return NotFound();
            }
            return Ok(new { days = trek.Days });
        }

        [HttpDelete]
        [Route("DeleteTrekRental/{id}")]
        public async Task<IActionResult> DeleteTrekRental(int id)
        {
            var success = await _adminProductsRepository.DeleteTrekRental(id);
            if (success)
            {
                return Ok();
            }
            return BadRequest("Failed to delete the rental.");
        }
        [HttpPost]
        [Route("UpdateTrekRental")]
        public async Task<IActionResult> UpdateTrekRental([FromBody] TrekRental trekRental)
        {
            if (ModelState.IsValid)
            {
                var success = await _adminProductsRepository.UpdateTrekRental(trekRental);
                if (success)
                {
                    return Ok();
                }
                return BadRequest("Failed to update the rental.");
            }
            return BadRequest(ModelState);
        }




        [HttpGet]
        [Route("AddInventory")]
        public async Task<IActionResult> AddInventory()
        {
            var products = await _adminProductsRepository.RentalPage();
            var stores = await _adminProductsRepository.GetStores();
            var model = new InventoryItemModel
            {
                Products_Name = products,
                Stores_Name = stores,
            };

            return View(model);
        }
        [HttpGet]
        [Route("GetProductSizes/{productId}")]
        public async Task<IActionResult> GetProductSizes(int productId)
        {
            if (productId <= 0)
            {
                return BadRequest("Invalid product ID.");
            }

            var sizes = await _adminProductsRepository.GetSizesByProductId(productId);
            if (sizes == null || !sizes.Any())
            {
                return NotFound("No sizes found for the selected product.");
            }

            var result = sizes.Select(s => new
            {
                s.S_Id,
                s.Name,
                s.ProductId,
                ProductName = s.Product.ProductName
            });

            return Ok(result);
        }

        [HttpPost]
        [Route("SaveInventory")]
        public async Task<IActionResult> SaveInventory([FromBody] List<InventoryItemModel> inventoryItems)
        {
            if (inventoryItems == null || !inventoryItems.Any())
            {
                return BadRequest("No inventory items to save.");
            }

            foreach (var item in inventoryItems)
            {
                // Check if an inventory item with the same StoreId, ProductId, and SizeId already exists
                var existingItem = await _context.InventoryItem
                    .FirstOrDefaultAsync(i => i.Store_Id == item.Store_Id && i.Product_Id == item.Product_Id && i.SizeId == item.SizeId);

                if (existingItem != null)
                {
                    // Update the existing quantity by adding the new quantity
                    existingItem.Quantity += item.Quantity;
                    existingItem.PricePerDay = item.PricePerDay; // Update PricePerDay if necessary
                    _context.InventoryItem.Update(existingItem); // Update the existing entry
                }
                else
                {
                    // If the item doesn't exist, create a new entry
                    var newInventoryItem = new InventoryItem
                    {
                        Product_Id = item.Product_Id,
                        SizeId = item.SizeId,
                        SizeName = item.SizeName, // Store SizeName
                        Quantity = item.Quantity,
                        PricePerDay = item.PricePerDay,
                        Store_Id = item.Store_Id
                    };

                    await _context.InventoryItem.AddAsync(newInventoryItem); // Add new entry
                }
            }

            await _context.SaveChangesAsync(); // Commit changes to the database
            return Ok("Inventory saved/updated successfully.");
        }



        [HttpGet]
        [Route("EditInventory")]
        public IActionResult EditInventory()
        {
            var stores = _context.Store
                .Select(store => new StoresModel
                {
                    Store_Id = store.Store_Id,
                    StoreName = store.StoreName
                })
                .ToList();

            return View(stores);
        }

        [HttpGet]
        [Route("GetInventoryItems")]
        public async Task<IActionResult> GetInventoryItems(int storeId)
        {
            var inventoryItems = await _context.InventoryItem
                .Where(i => i.Store_Id == storeId)
                .Select(i => new
                {
                    i.Invent_Id,
                    ProductName = _context.Products
                                         .Where(p => p.Products_Id == i.Product_Id)
                                         .Select(p => p.ProductName)
                                         .FirstOrDefault(),
                    SizeName = _context.Size
                                       .Where(s => s.IdSize == i.SizeId)
                                       .Select(s => s.Name)
                                       .FirstOrDefault(),
                    i.Quantity,
                    i.PricePerDay
                })
                .ToListAsync();

            return Json(inventoryItems);
        }

        [HttpPost]
        [Route("UpdateInventoryItems")]
        public async Task<IActionResult> UpdateInventoryItems([FromBody] List<InventoryItem> inventoryItems)
        {
            foreach (var item in inventoryItems)
            {
                var existingItem = await _context.InventoryItem.FindAsync(item.Invent_Id);
                if (existingItem != null)
                {
                    existingItem.Quantity = item.Quantity;
                    existingItem.PricePerDay = item.PricePerDay;

                    

                    _context.InventoryItem.Update(existingItem);
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }




        [HttpGet]
        [Route("DashBoard")]
        public IActionResult DashBoard()
        {
            return View();
        }


        [HttpGet]
        [Route("RecentOrders")]
        public async Task<IActionResult> RecentOrders()
        {
            var cartItems = await _context.RentAllParticipant
                .Where(a=>a.PaymentStatus=="Paid"&& a.OrderDate>DateTime.Now.Date.AddDays(-7) && a.OrderDate<=DateTime.Now.Date.AddDays(1))
                .OrderByDescending(a=>a.OrderDate)
                .ToListAsync();
            return View(cartItems);
        }

        [HttpGet]
        [Route("OrderByDate")]
        public IActionResult OrderByDate()
        {
            return View();
        }

        [HttpGet]
        [Route("OrderByName")]
        public IActionResult OrderByName()
        {
            return View();
        }

        [Route("OrderByTrek")]

        public async Task<IActionResult> OrderByTrek(int TrekId, int DepartureId)
        {
            if (TrekId == 0 && DepartureId == 0)
            {
                var cartItems = await _context.RentAllParticipant
                    .Where(a =>
    (a.PaymentStatus == "Paid" || a.PaymentStatus == "Offline") &&
    a.OrderDate > DateTime.Now.Date.AddDays(-7) &&
    a.OrderDate <= DateTime.Now.Date.AddDays(1))
                    .OrderByDescending(a => a.OrderDate)
                    .ToListAsync();
                ViewBag.Treks = new SelectList(await _trekRepository.GetAllTreks(), "TrekId", "TrekName", TrekId);
                return View(cartItems);
            }
            else
            {


                var comments = await _context.SalesComments
                    .Where(comment => comment.DepartureId == DepartureId).ToListAsync();
                ViewData["Comments"] = comments;

                var Treks = await _context.TrekDetails
                          .Where(trek => trek.TrekId == TrekId).Select(trek => new TrekModelForDbTable
                          {
                              TrekId = trek.TrekId,
                              TrekName = trek.TrekName,

                          })
                          .FirstOrDefaultAsync();

                //ViewData["TrekName"] = Treks.TrekName;

                var Departure = await _context.TrekDeparture
                       .Where(departure => departure.DepartureId == DepartureId).FirstOrDefaultAsync();
                ViewData["Departure"] = Departure;

                var Batch = await _context.TrekDeparture
                      .Where(departure => departure.DepartureId == DepartureId).Select(d => d.Batch).FirstOrDefaultAsync();
                ViewData["Batch"] = Batch;


            }

            return View();
        }

        [HttpPost]
        [Route("ExportExcelData")]
        public async Task<IActionResult> ExportExcelData(string orderIds)
        {
            if (string.IsNullOrEmpty(orderIds))
                return BadRequest("No Order IDs received.");

            var orderIdList = orderIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var val) ? val : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            var participants = await _context.RentAllParticipant
                .Where(p => orderIdList.Contains(p.Participant_Id))
                .Select(p => new
                {
                    p.FirstName,
                    p.LastName,
                    p.ProductNames,       // e.g., "Walking Stick,Head Torch"
                    p.Quantities,         // e.g., "1,2"
                    p.ProductVarients,    // e.g., "Large,Rechargeable"
                    p.PaymentStatus,
                    p.Participant_Id,
                    p.BookingId,
                    p.TrekName,
                    p.StartDate,
                    p.TotalAmount,
                    p.MobileNo

                })
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("RentParticipants");

                // Headers
                worksheet.Cell(1, 1).Value = "S.No.";
                worksheet.Cell(1, 2).Value = "Client Name";
                worksheet.Cell(1, 3).Value = "Item";
                worksheet.Cell(1, 4).Value = "Quantity";
                worksheet.Cell(1, 5).Value = "Remarks";
                worksheet.Cell(1, 6).Value = "Order_Id";
                worksheet.Cell(1, 7).Value = "Total Amount";
                worksheet.Cell(1, 8).Value = "Mobile_No";

                worksheet.Range("A1:G1").Style.Font.Bold = true;


                int row = 2;
                int serialNo = 1;
                decimal grandTotal = 0;
                foreach (var p in participants)
                {
                    var clientName = $"{p.FirstName} {p.LastName}";

                    var productList = p.ProductNames?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    var quantityList = p.Quantities?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    var variantList = p.ProductVarients?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

                    for (int i = 0; i < productList.Length; i++)
                    {

                        var rawProduct = productList[i];
                        var product = rawProduct.Trim('[', ']', '"', '\\').Trim();

                        string rawVariant = i < variantList.Length ? variantList[i] : string.Empty;
                        var variant = rawVariant.Trim('[', ']', '"', '\\').Trim();

                        string rawQuantity = i < quantityList.Length ? quantityList[i] : string.Empty;
                        var quantity = rawQuantity.Trim('[', ']', '"', '\\').Trim();

                        var itemDisplay = !string.IsNullOrEmpty(variant) ? $"{product} ({variant})" : product;

                        if (i == 0)
                        {
                            worksheet.Cell(row, 1).Value = serialNo;
                            worksheet.Cell(row, 2).Value = clientName;
                        }

                        worksheet.Cell(row, 3).Value = itemDisplay;
                        worksheet.Cell(row, 4).Value = quantity;

                        if (i == 0)
                        {
                            worksheet.Cell(row, 5).Value = p.PaymentStatus;
                            worksheet.Cell(row, 6).Value = p.BookingId;
                            worksheet.Cell(row, 7).Value = p.TotalAmount;
                            worksheet.Cell(row, 8).Value = p.MobileNo;
                            grandTotal += (p.TotalAmount);
                        }

                        row++;
                    }

                    serialNo++;
                }



                worksheet.Cell(row + 1, 6).Value = "Grand Total:";
                worksheet.Cell(row + 1, 7).Value = grandTotal;
                worksheet.Range(row + 1, 6, row + 1, 7).Style.Font.Bold = true;
                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;
                var firstParticipant = participants.FirstOrDefault();

                string trekName = firstParticipant?.TrekName ?? "UnknownTrek";
                string startDate = firstParticipant?.StartDate.ToString("dd/MMM/yyyy") ?? "UnknownDate";

                // Create dynamic file name
                string fileName = $"{trekName}_{startDate}.xlsx";

                // Return Excel file with dynamic name
                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }


        [HttpGet]
        [Route("AddOffilineData")]

        public async Task<IActionResult> AddOffilineData()
        {
            var trekList = await _context.TrekRental.ToListAsync();
            var products = await _context.Products.ToListAsync();



            var model = new RentOfflineDataViewModel
            {
                Products = products,
                Treks = trekList
            };

            return View(model);
        }

        [HttpGet]
        [Route("GetDepartureDates")]
        public async Task<IActionResult> GetDepartureDates(int trekId)
        {
            var departureDates = await _departureRepository.DeparturedateByTrekId(trekId);
            ViewBag.trekDays = await _context.TrekDetails
    .Where(t => t.TrekId == trekId)
    .Select(t => t.Days)
    .FirstOrDefaultAsync();
            var formattedDates = departureDates
     .OrderBy(dep => dep.StartDate)
     .Select(dep => new
     {
         value = dep.DepartureId,
         text = $"{dep.StartDate} to {dep.EndDate}"
     })
     .ToList();


            return Json(formattedDates);
        }


        [HttpGet]
        [Route("GetProductSize")]
        public async Task<IActionResult> GetProductSize(int productId)
        {
            var size = await _context.Product_Size_Varient.Where(p => p.ProductId == productId).ToListAsync();

            var formattedSize = size

     .OrderBy(dep => dep.S_Id)
     .Select(dep => new
     {
         value = dep.S_Id,
         text = $"{dep.Name}"
     })
     .ToList();


            return Json(formattedSize);
        }



        [HttpGet]
        [Route("GetProductDetailsBySize")]
        public async Task<IActionResult> GetProductDetailsBySize(int sizeId, int trekId, int quantity, int productId)
        {
            var storeId = await _context.TrekRental
                .Where(s => s.TrekId == trekId)
                .Select(s => s.StoreId)
                .FirstOrDefaultAsync();

            // Get product details
            var product = await _context.InventoryItem
                .Where(i => i.SizeId == sizeId && i.Product_Id == productId && i.Store_Id == storeId)
                .Select(i => new
                {
                    PricePerDay = i.PricePerDay,

                    SizeName = i.SizeName,
                })
                .FirstOrDefaultAsync();

            // Get trek details (Days & TrekName)
            var trek = await _context.TrekDetails
                .Where(t => t.TrekId == trekId)
                .Select(t => new
                {
                    TrekDays = t.Days,
                    TrekName = t.TrekName
                })
                .FirstOrDefaultAsync();

            var productName = await _context.Products
                .Where(t => t.Products_Id == productId)
                .Select(t => new
                {
                    productId = t.Products_Id,
                    productName = t.ProductName
                })
                .FirstOrDefaultAsync();
            if (product == null || trek == null)
            {
                return Json(new { success = false, message = "Product or Trek not found" });
            }

            // Calculate total price
            var totalPrice =
    (product.PricePerDay * trek.TrekDays * quantity);
            // Prepare response
            var response = new
            {
                ProductId = productName.productId,
                ProductName = productName.productName,
                SizeName = product.SizeName,
                PricePerDay = product.PricePerDay,
                TrekName = trek.TrekName,
                TrekDays = trek.TrekDays,
                Quantity = quantity,
                TotalPrice = totalPrice
            };

            return Json(new { success = true, data = response });
        }

        [HttpPost]
        [Route("SaveProducts")]
        public async Task<IActionResult> SaveProducts(RentOfflineDataViewModel model, string SelectedProductsJson)
        {
            
            if (model.Email != null)
            {
                var userId = await _context.Users.Where(e => e.Email == model.Email).Select(e => e.Id)
                .FirstOrDefaultAsync();

                if (userId == null)
                {
                    userId = Guid.NewGuid().ToString();
                }

                var storeId = await _context.TrekRental
                    .Where(s => s.TrekId == model.SelectedTrekId)
                    .Select(s => s.StoreId)
                    .FirstOrDefaultAsync();

                // Deserialize selected products from JSON
                var selectedProducts = JsonConvert.DeserializeObject<List<SelectedProductDto>>(SelectedProductsJson);

                decimal grandTotal = selectedProducts.First().GrandTotal;

                string bookingId = await _generateRentBookingId.GetBookingId(userId, model.DepartureId, model.SelectedTrekId);
                if (string.IsNullOrEmpty(bookingId))
                {
                    return BadRequest("Failed to generate a booking ID.");
                }

                // Get trek details
                var trek = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekId == model.SelectedTrekId);
                if (trek == null) return View(model);

                // Get departure details
                var departure = await _context.TrekDeparture.FirstOrDefaultAsync(d => d.DepartureId == model.DepartureId);
                if (departure == null) return View(model);

                var participant = new RentAllParticipant
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    TrekId = model.SelectedTrekId,
                    TrekName = trek.TrekName,
                    StartDate = departure.StartDate,
                    EndDate = departure.EndDate,
                    Email = model.Email,
                    MobileNo = model.MobileNo,
                    BookingId = bookingId,
                    DepartureId = model.DepartureId,
                    ProductIds = JsonConvert.SerializeObject(selectedProducts.Select(p => p.ProductId).ToList()),
                    ProductNames = JsonConvert.SerializeObject(selectedProducts.Select(p => p.ProductName).ToList()),
                    ProductVarients = JsonConvert.SerializeObject(selectedProducts.Select(p => p.SizeName).ToList()),
                    Quantities = JsonConvert.SerializeObject(selectedProducts.Select(p => p.Quantity).ToList()),
                    TotalAmount = grandTotal,
                    OrderDate = DateTime.Now,
                    PaymentStatus = "Offline",
                    Note = model.Note,
                    UserId = userId,
                    StoreId = storeId,
                    BookingSource="Offline"


                };


                _context.RentAllParticipant.Add(participant);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Offline data saved successfully!";
                return RedirectToAction("AddOffilineData");
            }


            else
            {
                TempData["Error"] = "User Not Found!";
                return RedirectToAction("AddOffilineData");
            }


        }
        [HttpGet]
        [Route("AddRentSlider")]
        public async Task<IActionResult> AddRentSlider()
        {

            var model = new RentSliderModel();

            var existingSliders = await _context.RentSlider
     .OrderByDescending(s => s.Id) // latest first
     .ToListAsync();

            ViewBag.ExistingSliders = existingSliders;
            var products = await _context.Products
                       .Select(p => new SelectListItem
                       {
                           Value = p.Products_Id.ToString(),
                           Text = p.ProductName
                       })
                       .ToListAsync();

            model.Products = products;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AddSliderImage")]
        public async Task<IActionResult> AddSliderImage(RentSliderModel model, List<IFormFile> desktopImage, List<IFormFile> mobileImage)
        {
            if ((desktopImage == null || !desktopImage.Any()) || (mobileImage == null || !mobileImage.Any()))
            {
                ModelState.AddModelError("", "Please upload both desktop and mobile images.");
                return View(model);
            }

            string CreatedBy = null;
            if (User.Identity.IsAuthenticated)
            {
                var emailClaim = User.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Name || c.Type == "Email" || c.Type == "EmailAddress" || c.Type == ClaimTypes.Email);

                CreatedBy = emailClaim?.Value;
            }

            // Get the current highest SortOrder from DB
            int lastSortOrder = 0;
            if (_context.RentSlider.Any())
            {
                lastSortOrder = _context.RentSlider.Max(s => s.SortOrder);
            }

            // Ensure both lists are of equal length
            int count = Math.Max(desktopImage.Count, mobileImage.Count);

            for (int i = 0; i < count; i++)
            {
                string desktopImageName = i < desktopImage.Count
                    ? await SaveImage("Shop", "Storeslider", desktopImage[i])
                    : null;

                string mobileImageName = i < mobileImage.Count
                    ? await SaveImage("Shop", "Storeslider", mobileImage[i])
                    : null;

                var slider = new RentSlider
                {
                    Title = model.Title,
                    Product_Id = model.ProductId,
                    DesktopImagePath = desktopImageName,
                    MobileImagePath = mobileImageName,
                    CreatedOn = DateTime.Now,
                    CreatedBy = CreatedBy,
                    SortOrder = lastSortOrder + 1 // continue from last sort order
                };

                _context.RentSlider.Add(slider);
                lastSortOrder++; // increment for next image
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Slider images uploaded successfully!";
            return RedirectToAction("AddRentSlider");
        }

        [HttpPost]
        [Route("UpdateSliderOrder")]
        public IActionResult UpdateSliderOrder([FromBody] List<int> ids)
        {
            try
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    int sliderId = ids[i];
                    var slider = _context.RentSlider.FirstOrDefault(s => s.Id == sliderId);
                    if (slider != null)
                    {
                        slider.SortOrder = i; // update order
                    }
                }

                _context.SaveChanges();
                return Ok(new { success = true, message = "Sort order updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("SendEmailToParticipant")]
        public async Task<IActionResult> SendEmailToParticipant(string bookingId)
        {
            var participant = await _context.RentAllParticipant
                .FirstOrDefaultAsync(r => r.BookingId == bookingId);

            if (participant == null)
            {


                return Json(new { success = false, message = "Data is Unpaid" });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(participant.Email))
                {
                    var fallbackEmail = await _context.CartItem
                        .Where(c => c.BookingId == bookingId)
                        .Select(c => c.Email)
                        .FirstOrDefaultAsync();

                    participant.Email = !string.IsNullOrWhiteSpace(fallbackEmail) ? fallbackEmail.Trim() : "not-provided@trekthehimalayas.com";
                    await _context.SaveChangesAsync();
                }







                // Parse products, quantities, variants
                var productArray = new JArray();
                var names = participant.ProductNames.Trim('[', ']', '"').Split(',');
                var quantities = participant.Quantities.Trim('[', ']').Split(',');
                var variants = participant.ProductVarients.Trim('[', ']', '"').Split(',');

                for (int j = 0; j < names.Length; j++)
                {
                    if (j < quantities.Length && j < variants.Length)
                    {
                        var name = names[j].Replace("\"", "").Trim();
                        var quantity = quantities[j].Trim();
                        var variant = variants[j].Replace("\"", "").Trim();

                        productArray.Add(new JObject
                        {
                            ["Name"] = name,
                            ["Quantity"] = quantity,
                            ["Variant"] = variant
                        });
                    }
                }

                // --- Send email to participant ---
                try
                {
                    JObject Params = new JObject
                    {
                        ["trek_name"] = participant.TrekName,
                        ["trek_date"] = participant.StartDate.ToString("dd-MMM-yyyy"),
                        ["trekker_name"] = $"{participant.FirstName} {participant.LastName}",
                        ["order_date"] = participant.OrderDate.ToString("dd-MMM-yyyy"),
                        ["ordered_items"] = JArray.FromObject(productArray),
                        ["ordered_items_Quantity"] = participant.Quantities,
                        ["ordered_items_Size"] = participant.ProductVarients,
                        ["total_amount"] = participant.TotalAmount,
                        ["orderid"] = participant.BookingId,
                        ["status"] = participant.PaymentStatus
                    };

                    string senderName = "Trek The Himalayas";
                    string senderEmail = "no-reply@trekthehimalayas.com";
                    string ParticipantEmail = participant.Email;

                    if (string.IsNullOrEmpty(ParticipantEmail))
                    {
                        Console.WriteLine("⚠ Participant email is null or missing in configuration.");
                    }
                    else
                    {
                        Console.WriteLine($"Sending email to Participant: {ParticipantEmail}");

                        // === RETRY LOGIC ADDED HERE ===
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                _bravoMail.SendEmail(senderName, senderEmail, ParticipantEmail, participant.FirstName, 33, Params);
                                break; // success
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Attempt {i + 1} failed sending participant email: {ex.Message}");
                                if (i == 2) throw; // throw only on last attempt
                                Thread.Sleep(2000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error sending trekker email: {ex.Message}");
                }


            }



            return Json(new { success = true });
        }

        [HttpPost]
        [Route("BalanceSheetData")]
        public async Task<IActionResult> BalanceSheetData(string orderIds)
        {
            if (string.IsNullOrEmpty(orderIds))
                return BadRequest("No Order IDs received.");

            var orderIdList = orderIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var val) ? val : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            var participants = await _context.RentAllParticipant
                .Where(p => orderIdList.Contains(p.Participant_Id))
                .Select(p => new
                {
                    p.FirstName,
                    p.LastName,
                    p.ProductNames,      
                    p.Quantities,         
                    p.ProductVarients,    
                    p.PaymentStatus,
                    p.Participant_Id,
                    p.BookingId,
                    p.TrekName,
                    p.StartDate,
                    p.EndDate,
                    p.TotalAmount,
                    p.OrderDate

                })
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("RentParticipants");

                // Headers
                worksheet.Cell(1, 1).Value = "S.No.";
                worksheet.Cell(1, 2).Value = "Order Date";
                worksheet.Cell(1, 3).Value = "OrderId";
                worksheet.Cell(1, 4).Value = "Client Name";
                worksheet.Cell(1, 5).Value = "Total Amount";
                worksheet.Cell(1, 6).Value = "Trekname";
                worksheet.Cell(1, 7).Value = "StartDate";

                worksheet.Range("A1:G1").Style.Font.Bold = true;


                int row = 2;
                int serialNo = 1;
                decimal grandTotal = 0;
                foreach (var p in participants)
                {
                    var startdate = p.StartDate.ToString("dd-MM-yyyy");
                    var orderDate = p.OrderDate.ToString("dd-MM-yyyy");

                    var clientName = $"{p.FirstName} {p.LastName}";
                   
                    var orderId = p.BookingId;
                    var productList = p.ProductNames?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    var quantityList = p.Quantities?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    var variantList = p.ProductVarients?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();





                    worksheet.Cell(row, 1).Value = serialNo;
                    worksheet.Cell(row, 2).Value = orderDate;


                    worksheet.Cell(row, 3).Value = orderId;
                    worksheet.Cell(row, 4).Value = clientName;


                    worksheet.Cell(row, 5).Value = p.TotalAmount;
                    worksheet.Cell(row, 6).Value = p.TrekName;
                    worksheet.Cell(row, 7).Value = startdate;
                    grandTotal += (p.TotalAmount);


                    row++;


                    serialNo++;
                }



                worksheet.Cell(row + 1, 6).Value = "Grand Total:";
                worksheet.Cell(row + 1, 7).Value = grandTotal;
                worksheet.Range(row + 1, 6, row + 1, 7).Style.Font.Bold = true;
                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;
                var firstParticipant = participants.FirstOrDefault();

                string trekName = firstParticipant?.TrekName ?? "UnknownTrek";
                string startDate = firstParticipant?.StartDate.ToString("dd/MMM/yyyy") ?? "UnknownDate";

                // Create dynamic file name
                string fileName = $"{trekName}_{startDate}.xlsx";

                // Return Excel file with dynamic name
                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }


       
        [Route("rescheduling_data")]
        public async Task<IActionResult> rescheduling_data()
        {

            return View();

        }

       
        [Route("EditUser")]
        public async Task<IActionResult> EditUser(string email)
        {
            var trekList = await _context.TrekRental.ToListAsync();
            var user = await _context.RentAllParticipant.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return new JsonResult(new { Success = false, Message = "UserNotFound" });
            }

            try
            {
                var model = new RentreschedulingViewModel
                {
                    User = user,          // 👈 add this property in ViewModel
                    Treks = trekList
                };

                return PartialView("_edituser", model);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { Success = false, Message = ex.Message });
            }
        }
        [HttpPost]
        [Route("UpdateUser")]
        public async Task<IActionResult> UpdateUser(RentAllParticipant model)
        {
            var users = await _context.RentAllParticipant
                .Where(x => x.DepartureId == model.DepartureId)
                .ToListAsync();

            if (users == null || users.Count == 0)
            {
                return Json(new { success = false, message = "Users not found" });
            }

            // ✅ Update all matching records
            foreach (var user in users)
            {
                user.TrekName = model.TrekName;
                model.TrekId = model.TrekId;
                user.StartDate = model.StartDate;
                user.EndDate = model.EndDate;
                user.DepartureId = model.DepartureId;
                user.Note = model.Note;
                user.TotalAmount = model.TotalAmount;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Updated successfully" });
        }
    }

}