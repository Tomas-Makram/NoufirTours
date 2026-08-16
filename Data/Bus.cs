using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("buses")]
    public class Bus
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(30)]
        [Column("bus_number")]
        public string BusNumber { get; set; } = default!;

        [Required, MaxLength(80)]
        [Column("chassis_number")]
        public string ChassisNumber { get; set; } = default!;

        [MaxLength(40)]
        [Column("plate_number")]
        public string? PlateNumber { get; set; }

        [MaxLength(60)]
        [Column("manufacturer")]
        public string? Manufacturer { get; set; }

        [MaxLength(80)]
        [Column("model_name")]
        public string? ModelName { get; set; }

        [Column("model_year")]
        public int? ModelYear { get; set; }

        [MaxLength(60)]
        [Column("bus_type")]
        public string? BusType { get; set; }

        [Column("seats_count")]
        public int? SeatsCount { get; set; }

        [MaxLength(40)]
        [Column("color")]
        public string? Color { get; set; }

        [MaxLength(1000)]
        [Column("specs")]
        public string? Specs { get; set; }

        [MaxLength(1000)]
        [Column("notes")]
        public string? Notes { get; set; }

        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        [Column("is_archived")]
        public int IsArchivedInt { get; set; } = 0;

        [Column("archived_at")]
        public long? ArchivedAtUnix { get; set; }

        [Column("archived_by_user_id")]
        public int? ArchivedByUserId { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        // ✅ أبعاد التصميم (Grid)
        [Column("layout_width")]
        public int LayoutWidth { get; set; } = 3;

        [Column("layout_height")]
        public int LayoutHeight { get; set; } = 5;

        public Bus()
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [NotMapped] public bool IsActive => IsActiveInt == 1;
        [NotMapped] public bool IsArchived => IsArchivedInt == 1;

        public ICollection<Trip> Trips { get; set; } = new List<Trip>();

        public ICollection<BusSeat> Seats { get; set; } = new List<BusSeat>();
    }
}