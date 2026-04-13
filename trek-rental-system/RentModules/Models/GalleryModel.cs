using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Models.Rent
{
    public class GalleryModel
    {
        [Key]
        public int Gallery_Id { get; set; }
        public string? Name { get; set; }
        public string? URL { get; set; }
    }
}
