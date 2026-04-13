
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TTH.Areas.Super.Models.Rent
    {
        public class RentSliderModel
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public int ProductId { get; set; }  // Selected product
            public List<SelectListItem> Products { get; set; }
            public List<string>? DesktopImagePath { get; set; }
            public List<string>? MobileImagePath { get; set; }
            public int SortOrder { get; set; }
        }
    }
