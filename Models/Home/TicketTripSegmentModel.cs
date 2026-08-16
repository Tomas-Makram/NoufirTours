using NoufirTours.Data;

namespace NoufirTours.Models.Home
{
    public class TicketTripSegmentModel
    {
        public string Title { get; set; } = "";     // "GO" / "RETURN"
        public Trip? Trip { get; set; }
        public Bus? Bus { get; set; }
        public Driver? Driver { get; set; }

        public string DestinationPlaceName { get; set; } = "-";

        public HashSet<string> BookedSeats { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool IsArchived { get; set; }
        public string ArchiveReason { get; set; } = "";
    }
}
