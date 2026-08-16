using NoufirTours.Data;
using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Home
{
    public class TicketScanModel
    {
        [Display(Name = "Booking ID")]
        public string? BookingIdRaw { get; set; }

        public string? Error { get; set; }
        public bool HasResult => Booking != null;

        public Booking? Booking { get; set; }

        public TicketTripSegmentModel? Go { get; set; }
        public TicketTripSegmentModel? Return { get; set; }

        public bool IsRound => Return != null;
    }
}