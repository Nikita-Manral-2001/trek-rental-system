namespace TTH.Models.Rent
{
    public class TrekProductsRequestDto
    {
        public int? TrekId { get; set; }
        public string TrekName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DepartureId { get; set; }
    }
}
