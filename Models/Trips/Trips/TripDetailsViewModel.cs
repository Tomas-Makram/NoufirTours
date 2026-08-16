using NoufirTours.Data;

namespace NoufirTours.Models.Trips.Trips
{
    public class TripDetailsViewModel
    {
        public Guid Id { get; set; }

        public string? TripName { get; set; }
        public string? DepartDate { get; set; }
        public string? DepartTime { get; set; }

        public string? FromCity { get; set; }
        public string? ToCity { get; set; }

        public string? PickupPlace { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLon { get; set; }

        public string? DropoffPlace { get; set; }
        public string? Notes { get; set; }

        public TripPriceType PriceType { get; set; } = TripPriceType.Round;

        public decimal? SeatPriceGo { get; set; }
        public decimal? SeatPriceReturn { get; set; }
        public decimal? SeatPriceRound { get; set; }

        public bool IsArchived { get; set; }
        public bool IsActive { get; set; }

        // IDs
        public Guid? BusId { get; set; }
        public Guid? DriverId { get; set; }

        public string? DriverNameOverride { get; set; }
        public string? DriverPhoneOverride { get; set; }

        // FULL DATA
        public BusDetailsTrip? Bus { get; set; }
        public DriverDetailsTrip? Driver { get; set; }
    }

    public class BusDetailsTrip
    {
        public Guid Id { get; set; }
        public string? BusNumber { get; set; }
        public string? ChassisNumber { get; set; }
        public string? PlateNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string? ModelName { get; set; }
        public int? ModelYear { get; set; }
        public string? BusType { get; set; }
        public int? SeatsCount { get; set; }
        public string? Color { get; set; }
        public string? Specs { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public int LayoutWidth { get; set; }
        public int LayoutHeight { get; set; }

        public int SeatsTotal { get; set; }
        public int SeatsActive { get; set; }
    }

    public class DriverDetailsTrip
    {
        public Guid Id { get; set; }
        public string? FullName { get; set; }
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? LicenseNumber { get; set; }
        public long? LicenseExpiryAtUnix { get; set; }
        public long JoinedAtUnix { get; set; }

        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }

        public List<DriverPhoneTrip> Phones { get; set; } = new();
    }

    public class DriverPhoneTrip
    {
        public string PhoneNumber { get; set; } = "";
        public bool IsPrimary { get; set; }
    }
}