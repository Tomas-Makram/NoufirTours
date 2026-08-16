using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("booking_codes")]
    public class BookingCode
    {
        [Key]
        [Column("booking_id")]
        public Guid BookingId { get; set; }

        [Required, MaxLength(16)]
        [Column("code")]
        public string Code { get; set; } = default!;

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = default!;
    }
}