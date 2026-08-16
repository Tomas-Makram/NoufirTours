using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Drivers
{
    public class DriverPhoneInputModel
    {
        public Guid? Id { get; set; } // used in edit

        [Required]
        [MaxLength(30)]
        [Display(Name = "Phone Number")]
        // 01 + (0/1/2/5) + 8 digits = 11
        [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Invalid phone number format.")]
        public string PhoneNumber { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }
    }
}
