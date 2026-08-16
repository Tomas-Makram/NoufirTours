using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("trips")]
    public class Trip
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("trip_origin")]
        public int TripOriginInt { get; set; } = (int)TripOrigin.Manual;

        [Column("auto_plan_id")]
        public Guid? AutoPlanId { get; set; }

        [Column("auto_plan_item_id")]
        public Guid? AutoPlanItemId { get; set; }

        [NotMapped]
        public TripOrigin TripOrigin
        {
            get => (TripOrigin)TripOriginInt;
            set => TripOriginInt = (int)value;
        }

        [Required, MaxLength(120)]
        [Column("trip_name")]
        public string TripName { get; set; } = default!;

        [Required, MaxLength(20)]
        [Column("depart_date")]
        public string DepartDate { get; set; } = default!; // "2026-02-18"

        [Required, MaxLength(20)]
        [Column("depart_time")]
        public string DepartTime { get; set; } = default!; // "12:30"

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
        public decimal? PickupLat { get; set; }

        [Column("pickup_lon", TypeName = "decimal(10,7)")]
        public decimal? PickupLon { get; set; }

        [Column("dropoff_lat", TypeName = "decimal(10,7)")]
        public decimal? DropoffLat { get; set; }

        [Column("dropoff_lon", TypeName = "decimal(10,7)")]
        public decimal? DropoffLon { get; set; }

        [MaxLength(200)]
        [Column("dropoff_place")]
        public string? DropoffPlace { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [MaxLength(80)]
        [Column("driver_name")]
        public string? DriverName { get; set; }

        [MaxLength(30)]
        [Column("driver_phone")]
        public string? DriverPhone { get; set; }

        [Column("driver_user_id")]
        public Guid? DriverUserId { get; set; }

        [Column("seat_price_go", TypeName = "decimal(18,2)")]
        public decimal SeatPriceGo { get; set; } = 0m;

        [Column("seat_price_return", TypeName = "decimal(18,2)")]
        public decimal SeatPriceReturn { get; set; } = 0m;

        //[Column("seat_price_round", TypeName = "decimal(18,2)")]
        //public decimal SeatPriceRound { get; set; } = 0m;

        [Column("is_archived")]
        public int IsArchivedInt { get; set; } = 0;

        [Column("is_active")]
        public int IsActiveInt { get; set; } = 1;

        [Column("archived_at")]
        public long? ArchivedAtUnix { get; set; }

        [Column("archived_by_user_id")]
        public Guid? ArchivedByUserId { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        [Column("bus_id")]
        public Guid? BusId { get; set; }

        [Column("driver_id")]
        public Guid? DriverId { get; set; }

        [ForeignKey(nameof(BusId))]
        public Bus? Bus { get; set; }

        [ForeignKey(nameof(DriverId))]
        public Driver? Driver { get; set; }

        [NotMapped]
        public bool IsArchived => IsArchivedInt == 1;

        [NotMapped]
        public bool IsActive => IsActiveInt == 1;

        [ForeignKey(nameof(DriverUserId))]
        public User? DriverUser { get; set; }

        [Column("price_type")]
        public int PriceTypeInt { get; set; } = (int)TripPriceType.Round;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        public ICollection<TripPlace> Places { get; set; } = new List<TripPlace>();
    }
}