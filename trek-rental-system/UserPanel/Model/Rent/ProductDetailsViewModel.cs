using System.ComponentModel.DataAnnotations.Schema;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Models.Rent;

namespace TTH.Models.Rent
{
    public class ProductDetailsViewModel
    {
        public int Products_Id { get; set; }


        public List<Product_Size_Varient>? Sizes { get; set; }
        public string? ProductName { get; set; }

        public decimal PricePerDay { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Description { get; set; }
        public string? CoverImgUrl { get; set; }



        public List<GalleryModel>? Gallery { get; set; }

        public int TrekId { get; set; }
        public string TrekName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DepartureId { get; set; }
        public DateTime BlockEndDate { get; set; }

        public string? UserEmail { get; set; }
    }
}
