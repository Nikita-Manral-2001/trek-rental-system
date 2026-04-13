using Razorpay.Api;
using TTH.Areas.Super.Data.Rent;

namespace TTH.Areas.Super.Models.Rent
{
    public class SizeModel
    {
        public int IdSize { get; set; }
        public string? Name { get; set; }

        public ICollection<Products>? Products { get; set; }
    }
}
