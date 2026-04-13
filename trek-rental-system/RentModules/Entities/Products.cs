using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class Products
    {
        [Key]
        public int Products_Id { get; set; }

        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }

        public ICollection<Product_Size_Varient>? Product_Size_Varient { get; set; }
        public ICollection<ProductGallery>? ProductGallery { get; set; }
        public CartItem? CartItem { get; set; }

    }
}
