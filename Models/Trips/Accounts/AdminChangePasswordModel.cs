using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Accounts
{
    public class AdminChangePasswordModel
    {
        [Required]
        public Guid UserId { get; set; }

        public string? Username { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password is too long.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}