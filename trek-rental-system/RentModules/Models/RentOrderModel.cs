using TTH.Areas.Super.Data.Rent;
namespace TTH.Areas.Super.Models.Rent
{
    public class RentOrderModel
    {
        public string TrekName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public decimal TotalAmount { get; set; }
        public List<CartItem> CartItems { get; set; }
    }
}
