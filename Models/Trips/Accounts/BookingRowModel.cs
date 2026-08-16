namespace NoufirTours.Models.Trips.Accounts
{
    public sealed class BookingRowModel
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsCanceled { get; set; }
        public DateTime CairoTime { get; set; }
        public Guid BookingId { get; internal set; }
        public Guid TripId { get; internal set; }
        public string CreatedAtText { get; internal set; }
        public string CustomerPhone { get; internal set; }

    }
}
