namespace NoufirTours.Models.Dashboard
{
    public class DashboardBookingRowModel
    {
        public Guid BookingId { get; set; }
        public string? CodeDel { get; set; }

        public string? Description { get; set; }

        public string CompanyFrom { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string SeatsText { get; set; } = "";

        // Optional if your real system has Go/Return/Round later:
        public string TripSegment { get; set; } = "GO"; // GO / RETURN
        public string? CustomerDropoffPlace { get; set; }
    }
}
