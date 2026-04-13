namespace TTH.Areas.Super.Models.Rent
{
    public class OrderedProductViewModel
    {
        public string ProductName { get; set; }
        public string VariantName { get; set; }
        public decimal TotalAmount { get; set; }
        public int Quantity { get; set; }
    }
}
