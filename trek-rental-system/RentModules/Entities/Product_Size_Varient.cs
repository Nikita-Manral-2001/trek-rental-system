using Razorpay.Api;
using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class Product_Size_Varient
    {
        [Key]
        public int S_V_Id { get; set; }
        public int S_Id { get; set; }
        public string? Name { get; set; }
        public int ProductId { get; set; }
        public Products? Product { get; set; }
    }
}
