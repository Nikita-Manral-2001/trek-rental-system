using TTH.Models.home;
using TTH.Models;

namespace TTH.Areas.Super.Models.Rent
{
    public class CombinedViewModel
    {
        public ProductViewModel ProductVM { get; set; }
        public ViewModel HomeVM { get; set; }
    }
}
