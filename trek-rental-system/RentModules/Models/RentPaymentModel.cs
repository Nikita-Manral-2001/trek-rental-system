using DocumentFormat.OpenXml.VariantTypes;
using TTH.Models;

namespace TTH.Areas.Super.Models.Rent
{
    public class RentPaymentModel
    {
        public int RentId { get; set; }
        public string TrekName { get; set; }
        public string BookingId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        public decimal TotalAmount { get; set; }
        public int DepartureId { get; set; }
        public string ProductName { get; set; }
        public string ProductId { get; set; }
        public string Variants { get; set; }
        public string Quantity { get; set; }
      
        public DateTime BlockEndDate { get; set; }
    }
}
