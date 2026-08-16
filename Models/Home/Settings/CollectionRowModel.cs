namespace NoufirTours.Models.Home.Settings
{
    public class CollectionRowModel
    {
        public Guid BookingId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public long CollectedAtUnix { get; set; }
        public string CollectedAtText { get; set; } = "";

        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
    }
}
