namespace TTH.Models.Rent
{
    public class HandlePaymentDtoStore
    {
        public string razorpay_payment_id { get; set; }
        public string razorpay_order_id { get; set; }
        public string razorpay_signature { get; set; }

        public string bookingId { get; set; }
       
    }
}
