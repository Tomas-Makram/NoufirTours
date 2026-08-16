namespace NoufirTours.Models.Trips.Trips
{
    public class DriverModalDetailsModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string NationalId { get; set; } = "";

        public string? Address { get; set; }
        public string? LicenseNumber { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }

        public DateTime JoinedAt { get; set; }
        public string? Notes { get; set; }

        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        public List<DriverPhoneModal> Phones { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }

    public class DriverPhoneModal
    {
        public string PhoneNumber { get; set; } = "";
        public bool IsPrimary { get; set; }
    }
}
