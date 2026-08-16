using NoufirTours.Data;

namespace NoufirTours.Models.Dashboard
{
    public class DashboardTripDetailsModel
    {
        public bool IsAdmin { get; set; }

        public string? Description { get; set; }
        public string CurrentUsername { get; set; } = "";
        public Guid TripId { get; set; }
        public string TripName { get; set; } = "";
        public string DepartDate { get; set; } = "";
        public string DepartTime { get; set; } = "";
        public string? FromCity { get; set; }
        public string? ToCity { get; set; }
        public string? PickupPlace { get; set; }
        public string? DropoffPlace { get; set; }
        public bool IsArchived { get; set; }

        public string? CompanyFilter { get; set; } // admin optional filter

        public List<DashboardBookingRowModel> Bookings { get; set; } = new();
        public List<DeletedTicket> DeletedTickets { get; set; } = new();
    }
}
