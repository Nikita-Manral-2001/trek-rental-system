namespace TTH.Areas.Super.Data.Rent
{
    public class RentSlider
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Product_Id { get; set; }
        public string? DesktopImagePath { get; set; } 
        public string? MobileImagePath { get; set; } 
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public int SortOrder { get; set; }
    }
}