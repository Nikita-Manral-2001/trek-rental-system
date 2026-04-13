using System.ComponentModel.DataAnnotations.Schema;
using TTH.Areas.Super.Data.Rent;

namespace TTH.Areas.Super.Models.Rent
{
    public class ProductModel
    {
        public int Products_Id { get; set; }

        public List<int> SelectedSizes { get; set; } = new List<int>();
        public List<Product_Size_Varient>? Sizes { get; set; }
        public string? ProductName { get; set; }

        public decimal PricePerDay { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Description { get; set; }
        public string? CoverImgUrl { get; set; }

        [NotMapped]
        public IFormFile? CoverPhoto { get; set; }

        [NotMapped]
        public IFormFileCollection? GalleryPhoto { get; set; }

        public List<GalleryModel>? Gallery { get; set; }
    }
}
