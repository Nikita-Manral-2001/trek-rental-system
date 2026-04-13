using TTH.Areas.Super.Data.Rent;

namespace TTH.Areas.Super.Models.Rent
{
    public class RentreschedulingViewModel
    {
        public RentAllParticipant User { get; set; }

        public List<TrekRental> Treks { get; set; }
    }
}
