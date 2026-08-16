using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Accounts
{
    public class AddBookingCollectionInput
    {
        [Required]
        public Guid BookingId { get; set; }

        [Required]
        [Range(0.01, 999999999)]
        public decimal Amount { get; set; }

        [Required, MaxLength(50)]
        public string Method { get; set; } = default!;
    }
}
