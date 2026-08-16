namespace NoufirTours.Models.Dashboard
{
    public class DashboardIndexModel
    {
        public DashboardFilterModel Filter { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string CurrentUsername { get; set; } = "";
        public List<DashboardTripRowModel> Trips { get; set; } = new();
        public List<DashboardBookingRowModel> MyBookings { get; set; } = new();
    }
}
