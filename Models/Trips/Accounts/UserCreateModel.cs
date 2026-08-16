using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Accounts
{
    public class UserCreateModel
    {
        [Required, MaxLength(80)]
        public string Username { get; set; } = "";

        [Required, MinLength(6)]
        public string Password { get; set; } = "";

        [MaxLength(150)]
        public string? FullName { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        [Required, MaxLength(20)]
        public string RoleText { get; set; } = "admin";

        public bool IsActive { get; set; } = true;
    }
}
