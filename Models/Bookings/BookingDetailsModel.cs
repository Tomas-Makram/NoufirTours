using NoufirTours.Data;

namespace NoufirTours.Models.Bookings
{
    public class BookingDetailsModel
    {
        public TripPriceType BookingMode { get; set; }

        public Guid TripId { get; set; }
        public string TripName { get; set; } = "";
        public string? DepartDate { get; set; }
        public string? DepartTime { get; set; }
        public string FromCity { get; set; } = "";
        public string ToCity { get; set; } = "";

        public string? PickupPlace { get; set; }
        public string? DropoffPlace { get; set; }

        public Guid? BusId { get; set; }
        public string BusName { get; set; } = "-";
        public int LayoutW { get; set; }
        public int LayoutH { get; set; }

        public decimal SeatPriceGo { get; set; }
        public decimal SeatPriceReturn { get; set; }

        public List<SeatCellModel> GridMain { get; set; } = new();
        public HashSet<string> UnavailableMain { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
        public List<SeatCellModel> MainDoors { get; set; } = new();

        // Return
        public Guid? ReturnTripId { get; set; }
        public string ReturnTripName { get; set; } = "";
        public string ReturnDepartDate { get; set; } = "";
        public string ReturnDepartTime { get; set; } = "";
        public string ReturnFromCity { get; set; } = "";
        public string ReturnToCity { get; set; } = "";

        public Guid? ReturnBusId { get; set; }
        public string ReturnBusName { get; set; } = "-";
        public int ReturnLayoutW { get; set; }
        public int ReturnLayoutH { get; set; }

        public List<SeatCellModel> GridReturn { get; set; } = new();
        public HashSet<string> UnavailableReturn { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

        public string? ErrorMessage { get; set; }

        public int RequiredMainSeats { get; set; } = 1;
        public int RequiredReturnSeats { get; set; } = 1;

        public List<string> MainDestinationSuggestions { get; set; } = new();
        public List<string> ReturnDestinationSuggestions { get; set; } = new();

        public BookingCreateInputModel Input { get; set; } = new();
    }
}
