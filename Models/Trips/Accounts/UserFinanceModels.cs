using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Accounts
{
    public sealed class UserFinanceModal
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";

        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Remaining => TotalDue - TotalPaid;

        public decimal TotalCollected { get; set; }

        public DateTime? BFrom { get; set; }
        public DateTime? BTo { get; set; }
        public string? BSearch { get; set; }
        public string? BStatus { get; set; }

        public DateTime? CFrom { get; set; }
        public DateTime? CTo { get; set; }
        public string? CMethod { get; set; }

        public List<BookingRowModel> Bookings { get; set; } = new();
        public List<CollectionRowModel> Collections { get; set; } = new();

        // Target booking
        public Guid? TargetBookingId { get; set; }
        public string? TargetBookingLabel { get; set; }
    }
}