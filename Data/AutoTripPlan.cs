using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("auto_trip_plans")]
    public class AutoTripPlan
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required, MaxLength(120)]
        [Column("name")]
        public string Name { get; set; } = "Default Plan";

        [Column("is_enabled")]
        public int IsEnabledInt { get; set; } = 1;

        [Column("is_done")]
        public bool isDone { get; set; } = false;

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("schedule_type")]
        public int ScheduleTypeInt { get; set; } = (int)AutoPlanScheduleType.Daily;

        [MaxLength(20)]
        [Column("specific_date")]
        public string? SpecificDate { get; set; } // yyyy-MM-dd when SpecificDate

        [Column("activation_mode")]
        public int ActivationModeInt { get; set; } = (int)AutoPlanActivationMode.ParallelAllActive;

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        [Column("updated_at")]
        public long? UpdatedAtUnix { get; set; }

        public List<AutoTripPlanItem> Items { get; set; } = new();
    }

    [Table("auto_trip_plan_items")]
    public class AutoTripPlanItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("plan_id")]
        public Guid PlanId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public AutoTripPlan? Plan { get; set; }

        [Column("order_no")]
        public int OrderNo { get; set; } = 1;

        // Template enabled
        [Column("is_enabled")]
        public int IsEnabledInt { get; set; } = 1;

        [Required, MaxLength(120)]
        [Column("trip_name")]
        public string TripName { get; set; } = "Trip";

        [Required, MaxLength(10)]
        [Column("depart_time")]
        public string DepartTime { get; set; } = "05:00"; // HH:mm

        // نوع الرحلة
        [Column("price_type")]
        public int PriceTypeInt { get; set; } = (int)TripPriceType.Round;

        [MaxLength(80)]
        [Column("from_city")]
        public string? FromCity { get; set; }

        [MaxLength(80)]
        [Column("to_city")]
        public string? ToCity { get; set; }

        [MaxLength(200)]
        [Column("pickup_place")]
        public string? PickupPlace { get; set; }

        [Column("pickup_lat", TypeName = "decimal(10,7)")]
        public decimal PickupLat { get; set; }

        [Column("pickup_lon", TypeName = "decimal(10,7)")]
        public decimal PickupLon { get; set; }

        [MaxLength(200)]
        [Column("dropoff_place")]
        public string? DropoffPlace { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("seat_price_go", TypeName = "decimal(18,2)")]
        public decimal SeatPriceGo { get; set; }

        [Column("seat_price_return", TypeName = "decimal(18,2)")]
        public decimal SeatPriceReturn { get; set; }

        [Column("bus_id")]
        public Guid? BusId { get; set; }

        [ForeignKey(nameof(BusId))]
        public Bus? Bus { get; set; }

        [Column("driver_id")]
        public Guid? DriverId { get; set; }

        [ForeignKey(nameof(DriverId))]
        public Driver? Driver { get; set; }
    }
}