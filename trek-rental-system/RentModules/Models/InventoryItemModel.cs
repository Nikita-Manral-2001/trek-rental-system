namespace TTH.Areas.Super.Models.Rent
{
    public class InventoryItemModel
    {
        public int Invent_Id { get; set; }
        public List<ProductModel>? Products_Name { get; set; }
        public List<StoresModel>? Stores_Name { get; set; }
        public int Product_Id { get; set; }
        public int Store_Id { get; set; }
        public int SizeId { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerDay { get; set; }
        public string? SizeName { get; set; }
    }
}
