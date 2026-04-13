using System.ComponentModel.DataAnnotations.Schema;

namespace TTH.Areas.Super.Data.Rent
{
    public class TrekRental
    {
        public int TrekRentalId { get; set; }
        public String? TrekName { get; set; }
        public string? StoreName { get; set; }


        public int RentingWaiting { get; set; }
        public int BlockBeforeDays { get; set; }
        public int Duration { get; set; }



        public int TrekId { get; set; }

        public int StoreId { get; set; }

        [ForeignKey("StoreId")]
        public Stores? Store { get; set; }
    }
}
