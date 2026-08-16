using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("deleted_tickets")]
    public class DeletedTicket
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("booking_id")]
        public Guid BookingId { get; set; }

        [MaxLength(16)]
        [Column("ticket_code")]
        public string? TicketCode { get; set; }

        [Required]
        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Column("return_trip_id")]
        public Guid? ReturnTripId { get; set; }

        [Required, MaxLength(120)]
        [Column("customer_name")]
        public string CustomerName { get; set; } = default!;

        [Required, MaxLength(30)]
        [Column("phone")]
        public string Phone { get; set; } = default!;

        [Required, MaxLength(150)]
        [Column("company_from")]
        public string CompanyFrom { get; set; } = default!;

        [MaxLength(300)]
        [Column("notes")]
        public string? Notes { get; set; }

        [Required]
        [Column("seats")]
        public string SeatsText { get; set; } = default!;

        [Column("seats_return")]
        public string? SeatsReturnText { get; set; }

        [Column("booking_type")]
        public int BookingTypeInt { get; set; }

        [Column("paid_amount", TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(200)]
        [Column("destination_place_name")]
        public string? DestinationPlaceName { get; set; }

        [MaxLength(200)]
        [Column("return_destination_place_name")]
        public string? ReturnDestinationPlaceName { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        [Column("deleted_at")]
        public long DeletedAtUnix { get; set; }

        [Column("deleted_by_user_id")]
        public Guid? DeletedByUserId { get; set; }

        [MaxLength(300)]
        [Column("delete_reason")]
        public string? DeleteReason { get; set; }

        // Snapshot trip info
        [MaxLength(120)]
        [Column("trip_name")]
        public string? TripName { get; set; }

        [MaxLength(20)]
        [Column("trip_depart_date")]
        public string? TripDepartDate { get; set; }

        [MaxLength(20)]
        [Column("trip_depart_time")]
        public string? TripDepartTime { get; set; }

        [MaxLength(80)]
        [Column("trip_from_city")]
        public string? TripFromCity { get; set; }

        [MaxLength(80)]
        [Column("trip_to_city")]
        public string? TripToCity { get; set; }

        [MaxLength(120)]
        [Column("return_trip_name")]
        public string? ReturnTripName { get; set; }

        [MaxLength(20)]
        [Column("return_trip_depart_date")]
        public string? ReturnTripDepartDate { get; set; }

        [MaxLength(20)]
        [Column("return_trip_depart_time")]
        public string? ReturnTripDepartTime { get; set; }

        [MaxLength(80)]
        [Column("return_trip_from_city")]
        public string? ReturnTripFromCity { get; set; }

        [MaxLength(80)]
        [Column("return_trip_to_city")]
        public string? ReturnTripToCity { get; set; }

        [ForeignKey(nameof(DeletedByUserId))]
        public User? DeletedByUser { get; set; }
    }
}