using NoufirTours.Data;
using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Trips
{
    public class TripCreateEditModel
    {
        public Guid? Id { get; set; }

        [Required, MaxLength(120)]
        public string? TripName { get; set; }

        [Required, MaxLength(20)]
        public string? DepartDate { get; set; }  // yyyy-MM-dd

        [Required, MaxLength(20)]
        public string? DepartTime { get; set; }  // HH:mm

        [MaxLength(80)]
        public string? FromCity { get; set; }

        [MaxLength(80)]
        public string? ToCity { get; set; }

        [MaxLength(200)]
        public string? PickupPlace { get; set; }

        public decimal? PickupLat { get; set; }
        public decimal? PickupLon { get; set; }

        [MaxLength(200)]
        public string? DropoffPlace { get; set; }

        public string? Notes { get; set; }

        [Required]
        public TripPriceType PriceType { get; set; } = TripPriceType.Round;

        [Range(0, 1_000_000)]
        public decimal? SeatPriceGo { get; set; }

        [Range(0, 1_000_000)]
        public decimal? SeatPriceReturn { get; set; }

        public Guid? BusId { get; set; }
        public Guid? DriverId { get; set; }

        [MaxLength(80)]
        public string? DriverName { get; set; }

        [MaxLength(30)]
        public string? DriverPhone { get; set; }

        public Guid? DriverUserId { get; set; }

        public int IsArchivedInt { get; set; } = 0;

        // =========================
        // UI / Lock Flags (Server decides)
        // =========================
        public bool HasAnyBookings { get; set; }
        public bool HasGoBookings { get; set; }
        public bool HasReturnBookings { get; set; }

        // Date/Time never editable (always)
        public bool LockDepartDateTime { get; set; } = true;

        // If ANY booking exists => lock “everything”, with exceptions (type/price rules)
        public bool LockAllCoreFields { get; set; }

        // Allowed edit switches (computed in controller)
        public bool CanEditCoreFields { get; set; }
        public bool CanEditBusDriver { get; set; }
        public bool CanEditPriceGo { get; set; }
        public bool CanEditPriceReturn { get; set; }
        public bool CanEditPriceType { get; set; }
    }
}