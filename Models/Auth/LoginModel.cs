using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Auth
{
    public class LoginModel
    {
        [Required(ErrorMessage = "Email or PhoneNumber is required")]
        public string? username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? password { get; set; }
    }
}
