using Razorpay.Api;
using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class ProductGallery
    {
        [Key]
        public int Gallery_Id { get; set; }
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public string? URL { get; set; }
        public Products? Product { get; set; }
    }
}
