using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("drivers")]
    public class Driver
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(120)]
        [Column("full_name")]
        public string FullName { get; set; } = default!;

        // الرقم القومي - Unique
        [Required, MaxLength(20)]
        [Column("national_id")]
        public string NationalId { get; set; } = default!;

        // محل الإقامة
        [MaxLength(250)]
        [Column("address")]
        public string? Address { get; set; }

        // رخصة قيادة (اختياري لكن مفيد)
        [MaxLength(40)]
        [Column("license_number")]
        public string? LicenseNumber { get; set; }

        // تاريخ انتهاء الرخصة (Unix) اختياري
        [Column("license_expiry_at")]
        public long? LicenseExpiryAtUnix { get; set; }

        // تاريخ الانضمام - تلقائي
        [Column("joined_at")]
        public long JoinedAtUnix { get; set; }

        // حالة التشغيل
        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        // ملاحظات
        [MaxLength(1000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // أرشفة (اختياري)
        [Column("is_archived")]
        public int IsArchivedInt { get; set; } = 0;

        [Column("archived_at")]
        public long? ArchivedAtUnix { get; set; }

        [Column("archived_by_user_id")]
        public Guid? ArchivedByUserId { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        public Driver()
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            JoinedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [NotMapped]
        public bool IsActive => IsActiveInt == 1;

        [NotMapped]
        public bool IsArchived => IsArchivedInt == 1;

        // ✅ أرقام الهاتف المتعددة
        public ICollection<DriverPhone> Phones { get; set; } = new List<DriverPhone>();

        // Navigation (لو هتربطها بـ Trips لاحقاً)
        public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    }
}