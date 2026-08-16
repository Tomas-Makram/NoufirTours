using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Drivers
{
    public class DriverCreateModel
    {
        [Required, MaxLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string NationalId { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(40)]
        public string? LicenseNumber { get; set; }

        // Joined Date in UI (auto today)
        [DataType(DataType.Date)]
        public DateTime JoinedAtDate { get; set; } = DateTime.UtcNow.Date;

        // License expiry default = today + 3 years
        [DataType(DataType.Date)]
        public DateTime? LicenseExpiryDate { get; set; } = DateTime.UtcNow.Date.AddYears(3);

        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public List<DriverPhoneInputModel> Phones { get; set; } = new()
        {
            new DriverPhoneInputModel { IsPrimary = true }
        };
    }
}