namespace NoufirTours.Models.Home.Settings
{
    public class CanceledBookingRowModel
    {
        public Guid BookingId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }

        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";

        public long? CanceledAtUnix { get; set; }
        public string CanceledAtText { get; set; } = "-";
        public string? CancelNote { get; set; }
    }
}
