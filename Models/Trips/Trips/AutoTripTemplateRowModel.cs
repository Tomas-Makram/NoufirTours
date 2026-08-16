namespace NoufirTours.Models.Trips.Trips
{
    public class AutoTripTemplateRowModel
    {
        public Guid Id { get; set; }
        public int OrderNo { get; set; }

        public bool IsEnabled { get; set; }
        public bool AutoEveryDay { get; set; }

        public string TripName { get; set; } = "Trip";
        public string DepartTime { get; set; } = "05:00";

        public string? FromCity { get; set; }
        public string? ToCity { get; set; }

        public string? PickupPlace { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLon { set; get; }

        public string? DropoffPlace { get; set; }
        public string? Notes { get; set; }

        public decimal SeatPriceGo { get; set; }
        public decimal SeatPriceReturn { get; set; }
        public decimal SeatPriceRound { get; set; }

        public Guid? BusId { get; set; }
        public Guid? DriverId { get; set; }

        public bool IsAvailableForDate { get; set; } = true;
        public string? UnavailableReason { get; set; }
    }
}
