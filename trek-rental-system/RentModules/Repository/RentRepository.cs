using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Models;
using TTH.Areas.Super.Models.Departure;
using TTH.Areas.Super.Models.Rent;
using TTH.Areas.Super.Models.Trek;
using TTH.Models;


namespace TTH.Areas.Super.Repository.RentRepository
{
    public class AdminProductsRepository
    {


        private readonly AppDataContext _context;



        public AdminProductsRepository(AppDataContext context)
        {
            _context = context;

        }
        public async Task<int> AddProducts(ProductModel model)
        {
            var newProducts = new Products()
            {

                CoverImageUrl = model.CoverImgUrl,
                ProductName = model.ProductName,
                Description = model.Description,
                ProductGallery = new List<ProductGallery>()
            };

            foreach (var file in model.Gallery)
            {
                newProducts.ProductGallery.Add(new ProductGallery()
                {
                    Name = file.Name,
                    URL = file.URL,
                });
            }

            await _context.Products.AddAsync(newProducts);
            await _context.SaveChangesAsync();

            if (model.SelectedSizes != null && model.SelectedSizes.Count > 0)
            {
                foreach (var sizeId in model.SelectedSizes)
                {
                    var size = await _context.Size.FindAsync(sizeId);
                    if (size != null)
                    {
                        var sizeVariant = new Product_Size_Varient
                        {
                            S_Id = sizeId,
                            Name = size.Name,
                            ProductId = newProducts.Products_Id
                        };
                        _context.Product_Size_Varient.Add(sizeVariant);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return newProducts.Products_Id;
        }
        public async Task<List<ProductModel>> RentalPage()
        {
            var products = await _context.Products
                .Include(p => p.ProductGallery)
                .ToListAsync();

            return products.Select(product => new ProductModel
            {
                Products_Id = product.Products_Id,


                CoverImgUrl = product.CoverImageUrl,
                Description = product.Description,
                ProductName = product.ProductName,
                Gallery = product.ProductGallery.Select(g => new GalleryModel
                {
                    Name = g.Name,
                    URL = g.URL,
                }).ToList()
            }).ToList();
        }

        public async Task<ProductModel> GetProductByid(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductGallery)
                .FirstOrDefaultAsync(p => p.Products_Id == id);

            if (product == null)
            {
                return null;
            }

            var selectedSizes = await _context.Product_Size_Varient
                .Where(v => v.ProductId == id)
                .Select(v => v.S_Id)
                .ToListAsync();

            return new ProductModel
            {
                Products_Id = product.Products_Id,

                ProductName = product.ProductName,
                CoverImgUrl = product.CoverImageUrl,
                Description = product.Description,
                SelectedSizes = selectedSizes,
                Gallery = product.ProductGallery?.Select(g => new GalleryModel
                {
                    Gallery_Id = g.Gallery_Id,
                    Name = g.Name,
                    URL = g.URL
                }).ToList() ?? new List<GalleryModel>()
            };
        }

        public async Task UpdateProduct(ProductModel model)
        {
            var product = await _context.Products
                .Include(p => p.ProductGallery)
                .FirstOrDefaultAsync(p => p.Products_Id == model.Products_Id);

            if (product != null)
            {

                product.ProductName = model.ProductName;
                product.Description = model.Description;
                product.CoverImageUrl = model.CoverImgUrl;

                if (model.Gallery != null && model.Gallery.Count > 0)
                {
                    _context.Gallery.RemoveRange(product.ProductGallery);
                    product.ProductGallery = new List<ProductGallery>();

                    foreach (var file in model.Gallery)
                    {
                        product.ProductGallery.Add(new ProductGallery()
                        {
                            Name = file.Name,
                            URL = file.URL,
                        });
                    }
                }

                // Update size variants
                var existingSizes = await _context.Product_Size_Varient
                    .Where(v => v.ProductId == model.Products_Id)
                    .ToListAsync();

                _context.Product_Size_Varient.RemoveRange(existingSizes);

                if (model.SelectedSizes != null && model.SelectedSizes.Count > 0)
                {
                    foreach (var sizeId in model.SelectedSizes)
                    {
                        var size = await _context.Size.FindAsync(sizeId);
                        if (size != null)
                        {
                            var sizeVariant = new Product_Size_Varient
                            {
                                S_Id = sizeId,
                                Name = size.Name,
                                ProductId = product.Products_Id
                            };
                            _context.Product_Size_Varient.Add(sizeVariant);
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        private async Task<string> SaveFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/images/" + uniqueFileName;
        }

        public async Task DeleteGalleryImage(ProductGallery galleryImage)
        {
            _context.Gallery.Remove(galleryImage);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task<ProductGallery> GetGalleryImageById(int id)
        {
            return await _context.Gallery.FindAsync(id);
        }


        //Adding Stores

        public async Task<int> AddStore(StoresModel model)
        {
            var newStore = new Stores()
            {
                StoreName = model.StoreName
            };

            await _context.Store.AddAsync(newStore);
            await _context.SaveChangesAsync();
            return newStore.Store_Id;
        }

        public async Task<List<StoresModel>> GetStores()
        {
            var stores = await _context.Store.ToListAsync();
            return stores.Select(store => new StoresModel
            {
                Store_Id = store.Store_Id,
                StoreName = store.StoreName
            }).ToList();
        }

        public async Task<StoresModel> GetStoreById(int id)
        {
            var store = await _context.Store.FindAsync(id);
            if (store == null)
            {
                return null;
            }
            return new StoresModel
            {
                Store_Id = store.Store_Id,
                StoreName = store.StoreName
            };
        }
        public async Task UpdateStore(StoresModel model)
        {
            var existingStore = await _context.Store.FindAsync(model.Store_Id);
            if (existingStore != null)
            {
                existingStore.StoreName = model.StoreName;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> DeleteStore(int id)
        {
            var store = await _context.Store.FindAsync(id);
            if (store != null)
            {
                _context.Store.Remove(store);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<List<TrekModel>> GetAllTreks()
        {
            var trekDetails = await _context.TrekDetails.ToListAsync();
            var treks = trekDetails.Select(t => new TrekModel
            {
                TrekId = t.TrekId,
                TrekName = t.TrekName,

            }).ToList();

            return treks;
        }


        public async Task<List<TrekRentalModel>> GetAllRentals()
        {
            var rentals = await _context.TrekRental.ToListAsync();
            return rentals.Select(r => new TrekRentalModel
            {
                TrekRentalId = r.TrekRentalId,
                TrekName = r.TrekName,
                StoreName = r.StoreName,
                RentingWaiting = r.RentingWaiting,
                BlockBeforeDays = r.BlockBeforeDays,
                Duration = r.Duration
            }).ToList();
        }

        public async Task<int> AddTrekRental(TrekRentalModel model)
        {
            var trekRental = new TrekRental
            {
                TrekName = model.TrekName,
                StoreName = model.StoreName,
                RentingWaiting = model.RentingWaiting,
                BlockBeforeDays = model.BlockBeforeDays,
                Duration = model.Duration
            };

            await _context.TrekRental.AddAsync(trekRental);
            await _context.SaveChangesAsync();
            return trekRental.TrekRentalId;
        }
        public async Task<TrekModel> GetTrekByName(string trekName)
        {
            var trekDetail = await _context.TrekDetails.FirstOrDefaultAsync(t => t.TrekName == trekName);
            if (trekDetail == null)
            {
                return null;
            }

            return new TrekModel
            {
                TrekId = trekDetail.TrekId,
                TrekName = trekDetail.TrekName,
                Days = trekDetail.Days
            };
        }

        public async Task<bool> DeleteTrekRental(int id)
        {
            var rental = await _context.TrekRental.FindAsync(id);
            if (rental != null)
            {
                _context.TrekRental.Remove(rental);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
        public async Task<bool> UpdateTrekRental(TrekRental trekRental)
        {
            // Check if the StoreName exists in the Stores table
            var store = await _context.Store.FirstOrDefaultAsync(s => s.StoreName == trekRental.StoreName);

            if (store == null)
            {
                // If the StoreName is not found, return false or handle error
                throw new Exception("StoreName not found in the Stores table.");
            }

            // Find the existing TrekRental entry
            var existingRental = await _context.TrekRental.FindAsync(trekRental.TrekRentalId);
            if (existingRental != null)
            {
                // Update the values including StoreId from the Stores table
                existingRental.RentingWaiting = trekRental.RentingWaiting;
                existingRental.BlockBeforeDays = trekRental.BlockBeforeDays;
                existingRental.StoreName = trekRental.StoreName;
                existingRental.StoreId = store.Store_Id;  // Set the StoreId from the Stores table
                existingRental.Duration = trekRental.Duration;

                _context.TrekRental.Update(existingRental);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }


        public async Task<List<Product_Size_Varient>> GetSizesByProductId(int productId)
        {
            return await _context.Product_Size_Varient
                                 .Where(p => p.ProductId == productId)
                                 .Include(p => p.Product) // Include the Product details
                                 .ToListAsync();
        }

        public async Task SaveInventoryItems(List<InventoryItem> inventoryItems)
        {
            await _context.InventoryItem.AddRangeAsync(inventoryItems);
            await _context.SaveChangesAsync();
        }




    }
}