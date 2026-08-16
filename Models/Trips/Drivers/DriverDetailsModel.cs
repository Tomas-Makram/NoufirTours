namespace NoufirTours.Models.Trips.Drivers
{
    public class DriverTripSummaryModel
    {
        public Guid TripId { get; set; }
        public string Title { get; set; } = "";
        public string? DepartDate { get; set; }           // yyyy-MM-dd
        public string? DepartTime { get; set; }           // HH:mm
        public DateTime? DepartAtCairo { get; set; }      // Cairo local datetime
        public bool IsUpcoming { get; set; }
        public bool IsPast { get; set; }
        public bool IsToday { get; set; }

        public string StatusLabel
            => IsToday ? "Today" : (IsUpcoming ? "Upcoming" : "Past");
    }

    public class DriverDetailsModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string NationalId { get; set; } = "";
        public string? Address { get; set; }
        public string? LicenseNumber { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }

        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        public List<(string phone, bool isPrimary)> Phones { get; set; } = new();

        public List<DriverTripSummaryModel> Trips { get; set; } = new();
        public int TripsTotal => Trips?.Count ?? 0;
        public int TripsUpcoming => Trips?.Count(t => t.IsUpcoming) ?? 0;
        public int TripsPast => Trips?.Count(t => t.IsPast) ?? 0;
    }
}