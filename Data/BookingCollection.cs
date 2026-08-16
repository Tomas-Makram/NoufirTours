using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("booking_collections")]
    public class BookingCollection
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("booking_id")]
        public Guid BookingId { get; set; }

        [Required]
        [Column("amount", TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(50)]
        [Column("method")]
        public string Method { get; set; } = default!;

        [Column("collected_at")]
        public long CollectedAtUnix { get; set; }

        [Column("collected_by_user_id")]
        public Guid CollectedByUserId { get; set; } = Guid.Empty;

        // ------------ Navigation ------------

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = default!;

        [ForeignKey(nameof(CollectedByUserId))]
        public User CollectedByUser { get; set; } = default!;
    }
}