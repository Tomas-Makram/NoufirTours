namespace NoufirTours.Models.Trips.Accounts
{
    public class UserDetailsModel
    {
        public Guid UserID { get; set; }
        public string Username { get; set; } = "";
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string RoleText { get; set; } = "";
        public bool IsActive { get; set; }

        public DateTime? LastLoginCairo { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? ActionFilter { get; set; }

        public int TotalAuditLogs { get; set; }
        public int TripsAsDriverUser { get; set; }
        public int BookingsOnThoseTrips { get; set; }

        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Remaining => TotalDue - TotalPaid;

        public List<(DateTime CairoTime, string Action, string Entity, string? EntityId, string? Details)> RecentAudit { get; set; }
            = new();
    }
}
