namespace TTH.Models.Rent
{
    public class QunatitityIncrementDto
    {
        public int productId { get; set; }
        public int quantity { get; set; }
        public int variantSize { get; set; }

        public string bookingId { get; set; }
        public int departureId { get; set; }
    }
}
