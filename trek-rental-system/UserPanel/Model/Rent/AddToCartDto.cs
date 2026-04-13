namespace TTH.Models.Rent
{
    public class AddToCartDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int SizeId { get; set; }


        public int? DepartureId { get; set; }

    }
}
