using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Home.Settings
{
    public class FinanceEventRowModel
    {
        public long Unix { get; set; }
        public string DateText { get; set; } = "";

        public string Type { get; set; } = "";   // collection/cancel/booking
        public string Action { get; set; } = ""; // action name

        public string? BookingId { get; set; }
        public Guid? TripId { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }

        public decimal? Amount { get; set; }
        public decimal? PaidAmount { get; set; }
        public decimal? TotalAmount { get; set; }

        public string? Method { get; set; }
        public string? Note { get; set; }
    }
}