using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("bus_seats")]
    public class BusSeat
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("bus_id")]
        public Guid BusId { get; set; }

        // رقم/كود الكرسي داخل الباص (A1, 1, 12...) - unique per bus
        [MaxLength(20)]
        [Column("seat_code")]
        public string? SeatCode { get; set; }  // يمكن أن يكون null للـ Aisle و Door و WC

        // مكانه داخل الشبكة
        [Column("pos_x")]
        public int X { get; set; }

        [Column("pos_y")]
        public int Y { get; set; }

        // نوع العنصر: Seat, Aisle, Door, WC
        [Required, MaxLength(20)]
        [Column("element_type")]
        public string ElementType { get; set; } = "Seat";  // Seat, Aisle, Door, WC

        // هل يمكن الجلوس عليه؟ (للكراسي فقط)
        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        // الدور: Passenger/Driver/Assistant (للكراسي فقط)
        [MaxLength(20)]
        [Column("role")]
        public string? Role { get; set; } = "Passenger";

        // اختياري: كرسي مخصص لسائق معيّن
        [Column("assigned_driver_id")]
        public Guid? AssignedDriverId { get; set; }

        // للعناصر الأخرى (Door, WC, Aisle)
        [MaxLength(50)]
        [Column("label")]
        public string? Label { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        public BusSeat()
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [NotMapped]
        public bool IsActive => IsActiveInt == 1;

        [ForeignKey(nameof(BusId))]
        public Bus? Bus { get; set; }

        [MaxLength(1)]
        [Column("door_side")]
        public string? DoorSide { get; set; } // L / R / T / B

        [Column("door_offset")]
        public double? DoorOffset { get; set; } // 0..1
    }
}