using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Data.Rent
{
    public class RentAllParticipant
    {
        [Key]
        public int Participant_Id { get; set; }
        public string TrekName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        public decimal TotalAmount { get; set; }

        public string ProductIds { get; set; } // Store ProductIds as JSON array
        public string BookingId { get; set; }
        public int DepartureId { get; set; }
        public string ProductNames { get; set; }
        public string PaymentStatus { get; set; }
        public int TrekId { get; set; }
        public string Quantities { get; set; }
        public string ProductVarients { get; set; }
        //public int? PreviousDepartureId { get; set; }
        //public string? PreviousTrekName { get; set; }
        public DateTime BlockEndDate { get; set; }
     
        public int StoreId { get; set; }
        public DateTime OrderDate { get; set; }
        public string? Note { get; set; }

        public string BookingSource { get; set; }
    }
}
