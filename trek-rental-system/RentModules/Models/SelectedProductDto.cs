namespace TTH.Areas.Super.Models.Rent
{
    public class SelectedProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string SizeName { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerDay { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
