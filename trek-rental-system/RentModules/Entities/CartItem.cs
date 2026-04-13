using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class CartItem
    {
        [Key]
        public int CartItem_Id { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }

        public int VariantId { get; set; }
        public string? Variant { get; set; }
        public int Quantity { get; set; }
        public string? CoverImage { get; set; }
        public decimal TotalPrice { get; set; }
        public string? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MobileNo { get; set; }
        public string? Email { get; set; }
        public int? TrekId { get; set; }
        public string? TrekName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime OrderDate { get; set; }
        public string BookingId { get; set; }
        public int DepartureId { get; set; }
        public DateTime BlockEndDate { get; set; }
        public int StoreId { get; set; }
        public string BookingSource { get; set; }
    }
}
