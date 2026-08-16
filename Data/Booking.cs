using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoufirTours.Data
{
    [Table("bookings")]
    public class Booking
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        public BookingCode? CodeInfo { get; set; }

        [Required]
        [Column("trip_id")]
        public Guid TripId { get; set; }

        [Required, MaxLength(120)]
        [Column("customer_name")]
        public string CustomerName { get; set; } = default!;

        [Required, MaxLength(30)]
        [Column("phone")]
        public string Phone { get; set; } = default!;

        [Required, MaxLength(150)]
        [Column("company_from")]
        public string CompanyFrom { get; set; } = default!;

        [Required, MaxLength(300)]
        [Column("notes")]
        public string? Notes { get; set; }

        [Required]
        [Column("seats")]
        public string SeatsText { get; set; } = default!;

        [Column("return_datetime")]
        public string? ReturnDateTime { get; set; }

        [Column("paid_amount", TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column("total_amount", TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column("status")]
        public int StatusInt { get; set; }

        [Column("created_at")]
        public long CreatedAtUnix { get; set; }

        [Column("is_canceled")]
        public int IsCanceledInt { get; set; } = 0;

        [Column("canceled_at")]
        public long? CanceledAtUnix { get; set; }

        [Column("canceled_by_user_id")]
        public Guid? CanceledByUserId { get; set; }

        [Column("cancel_note")]
        public string? CancelNote { get; set; }

        [Column("created_by_user_id")]
        public Guid? CreatedByUserId { get; set; }

        [ForeignKey(nameof(TripId))]
        public Trip Trip { get; set; } = default!;

        [ForeignKey(nameof(CreatedByUserId))]
        public User? CreatedByUser { get; set; }

        [ForeignKey(nameof(CanceledByUserId))]
        public User? CanceledByUser { get; set; }

        // Existing
        [Column("booking_type")]
        public int BookingTypeInt { get; set; } = 1;

        [Column("return_trip_id")]
        public Guid? ReturnTripId { get; set; }

        [Column("seats_return")]
        public string? SeatsReturnText { get; set; }

        [ForeignKey(nameof(ReturnTripId))]
        public Trip? ReturnTrip { get; set; }

        public ICollection<BookingCollection> Collections { get; set; } = new List<BookingCollection>();

        // Destination

        [Required, MaxLength(200)]
        [Column("destination_place_name")]
        public string DestinationPlaceName { get; set; } = "";

        [Column("destination_place_id")]
        public Guid? DestinationPlaceId { get; set; }

        [ForeignKey(nameof(DestinationPlaceId))]
        public TripPlace? DestinationPlace { get; set; }

        // Round return destination
        [MaxLength(200)]
        [Column("return_destination_place_name")]
        public string? ReturnDestinationPlaceName { get; set; }

        [Column("return_destination_place_id")]
        public Guid? ReturnDestinationPlaceId { get; set; }

        [ForeignKey(nameof(ReturnDestinationPlaceId))]
        public TripPlace? ReturnDestinationPlace { get; set; }
    }
}