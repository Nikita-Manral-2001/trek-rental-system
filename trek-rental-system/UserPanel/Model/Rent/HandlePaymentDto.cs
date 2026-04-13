namespace TTH.Models.Rent
{
    public class HandlePaymentDto
    {
        public string razorpay_payment_id { get; set; }
        public string razorpay_order_id { get; set; }
        public string razorpay_signature { get; set; }
        public string bookingSource { get; set; }
        public string? bookingId { get; set; }
        public int departureId { get; set; }
    }
}
