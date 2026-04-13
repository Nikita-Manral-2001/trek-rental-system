using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class InventoryItem
    {
        [Key]
        public int Invent_Id { get; set; }

        [ForeignKey("Product_Id")]
        public int Product_Id { get; set; }
        public int SizeId { get; set; }
        public string? SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerDay { get; set; }
        public int Store_Id { get; set; }
    }
}
