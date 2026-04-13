using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class Size
    {
        [Key]
        public int IdSize { get; set; }
        public string? Name { get; set; }
    }
}
