using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class Stores
    {
        [Key]
        public int Store_Id { get; set; }
        public string? StoreName { get; set; }
    }
}
