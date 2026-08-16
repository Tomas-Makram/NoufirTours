namespace NoufirTours.Models.Trips.Accounts
{
    public sealed class CollectionRowModel
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public decimal Amount { get; set; }
        public string? Method { get; set; }
        public string? Note { get; set; }
        public DateTime CairoTime { get; set; }
        public string CreatedBy { get; set; } = "";
    }
}
