using TTH.Areas.Super.Data;
using TTH.Areas.Super.Models.Rent;

namespace TTH.Models.Rent
{
    public class AllProductsViewModel
    {
        public Dictionary<int, decimal> ProductTotalPrices { get; set; }
        public decimal PricePerDay { get; set; }


        public IEnumerable<ProductModel> Products { get; set; }

        public int? TrekId { get; set; }
        public string TrekName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DepartureId { get; set; }
        public string BookingId { get; set; }
        public DateTime BlockEndDate { get; set; }
        public int? StoreId { get; set; }
        public int Duration { get; set; }
    }
}
