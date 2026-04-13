using System.ComponentModel.DataAnnotations;

namespace TTH.Areas.Super.Models.Rent
{
    public class TrekRentalModel
    {
        public int TrekRentalId { get; set; }

        [Required(ErrorMessage = "Trek Name Required")]
        [Display(Name = "Trek Name")]
        public String? TrekName { get; set; }
        public string? StoreName { get; set; }

        public int RentingWaiting { get; set; }
        public int BlockBeforeDays { get; set; }
        public int Duration { get; set; }
    }
}
