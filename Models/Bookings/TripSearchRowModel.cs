using NoufirTours.Data;
namespace NoufirTours.Models.Bookings
{
    public sealed class TripSearchRowModel
    {
        public Guid TripId { get; set; }
        public string TripName { get; set; } = "";
        public string DepartDate { get; set; } = "";
        public string DepartTime { get; set; } = "";
        public string FromCity { get; set; } = "";
        public string ToCity { get; set; } = "";
        public string BusName { get; set; } = "-";

        public int SeatsTotal { get; set; }
        public int SeatsBooked { get; set; }
        public int SeatsAvailable => Math.Max(0, SeatsTotal - SeatsBooked);

        public decimal SeatPriceGo { get; set; }
        public decimal SeatPriceReturn { get; set; }

        public TripPriceType PriceType { get; set; } = TripPriceType.Round;
    }
}
