namespace NoufirTours.Models.Dashboard
{
    public class DashboardTripRowModel
    {
        public Guid TripId { get; set; }
        public string TripName { get; set; } = string.Empty;
        public string ClientName { get; set; } = "";
        public string DepartDate { get; set; } = "";
        public string DepartTime { get; set; } = "";
        public string? FromCity { get; set; }
        public string? ToCity { get; set; }
        public bool IsArchived { get; set; }

        public int BookingsCount { get; set; }
        public int SeatsCount { get; set; }

        public decimal TotalPaid { get; set; }
        public decimal TotalDue { get; set; }

        // group hint
        public List<string> Companies { get; set; } = new();
    }
}
