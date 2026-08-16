using NoufirTours.Data;

namespace NoufirTours.Models.Trips.Trips
{
    public class TripListItemModel
    {
        public Guid Id { get; set; }

        public string? DepartDate { get; set; }
        public string? DepartTime { get; set; }

        public string? TripName { get; set; }

        public string? FromCity { get; set; }
        public string? ToCity { get; set; }

        public string? PickupPlace { get; set; }
        public string? DropoffPlace { get; set; }

        public TripPriceType PriceType { get; set; } = TripPriceType.Round;

        public decimal? SeatPriceGo { get; set; }
        public decimal? SeatPriceReturn { get; set; }
        public decimal? SeatPriceRound { get; set; }

        public Guid? BusId { get; set; }
        public string? BusNumber { get; set; }

        public Guid? DriverId { get; set; }
        public string? DriverFullName { get; set; }

        public string? DriverName { get; set; }
        public string? DriverPhone { get; set; }

        public bool IsArchived { get; set; }
        public bool IsActive { get; set; }
    }
}