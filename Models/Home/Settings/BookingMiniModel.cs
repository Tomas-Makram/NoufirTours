namespace NoufirTours.Models.Home.Settings
{
    public class BookingMiniModel
    {
        public Guid Id { get; set; }
        public Guid TripId { get; set; }
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public decimal PaidAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int IsCanceledInt { get; set; }
        public long CreatedAtUnix { get; set; }
        public long? CanceledAtUnix { get; set; }
        public string? CancelNote { get; set; }
    }
}
