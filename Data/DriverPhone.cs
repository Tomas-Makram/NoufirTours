using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("driver_phones")]
    public class DriverPhone
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("driver_id")]
        public Guid DriverId { get; set; }

        // رقم الهاتف - Unique (ممنوع يتكرر على النظام كله)
        [Required, MaxLength(30)]
        [Column("phone_number")]
        public string PhoneNumber { get; set; } = default!;

        // تمييز رقم أساسي (اختياري)
        [Column("is_primary")]
        public int IsPrimaryInt { get; set; } = 0;

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        public DriverPhone()
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [NotMapped]
        public bool IsPrimary => IsPrimaryInt == 1;

        [ForeignKey(nameof(DriverId))]
        public Driver? Driver { get; set; }
    }
}
