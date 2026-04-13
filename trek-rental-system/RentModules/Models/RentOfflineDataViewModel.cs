using Microsoft.Build.Evaluation;
using Razorpay.Api;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;

namespace TTH.Areas.Super.Models.Rent
{
    public class RentOfflineDataViewModel
    {
        public int SelectedTrekId { get; set; }
        public int SelectedProductId { get; set; }


        public List<ProductItem> SelectedProducts { get; set; } = new List<ProductItem>();



        public string Email { get; set; }
        public string MobileNo { get; set; }
        public int DepartureId { get; set; }
        public string BookingId { get; set; }
        public string FirstName { get; set; }
        public string Note { get; set; }
        public string LastName { get; set; }

        public List<TrekRental> Treks { get; set; }
        public List<Products> Products { get; set; }
    }
    public class ProductItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public string SizeName { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
