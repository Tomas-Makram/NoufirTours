using NoufirTours.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("audit_log")]
    public class AuditLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Required, MaxLength(60)]
        [Column("action")]
        public string Action { get; set; } = default!; // create/edit/cancel/update_setting...

        [Required, MaxLength(60)]
        [Column("entity")]
        public string Entity { get; set; } = default!; // users/trips/bookings/app_settings...

        [Column("entity_id")]
        public string? EntityId { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        // Navigation
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;
    }
}