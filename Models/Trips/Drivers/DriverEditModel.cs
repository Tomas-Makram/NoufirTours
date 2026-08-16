using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Drivers
{
    public class DriverEditModel
    {
        public Guid Id { get; set; }

        [Required, MaxLength(120)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(14)]
        [MinLength(14)]
        [Display(Name = "National ID")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "National ID must be exactly 14 digits.")]
        public string NationalId { get; set; } = string.Empty;

        [MaxLength(250)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [MaxLength(40)]
        [Display(Name = "License Number")]
        public string? LicenseNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "License Expiry Date")]
        public DateTime? LicenseExpiryDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Joined Date")]
        public DateTime JoinedAtDate { get; set; }

        [Display(Name = "Active Driver")]
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        [MinLength(1, ErrorMessage = "At least one phone number is required.")]
        public List<DriverPhoneInputModel> Phones { get; set; } = new();
        public bool LockCoreFields { get; set; }
    }
}
