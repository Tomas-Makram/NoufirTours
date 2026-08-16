namespace NoufirTours.Models.Trips.Buses
{
    public class BusDetailsHeaderModel
    {
        public Guid Id { get; set; }
        public string BusNumber { get; set; } = "";
        public string ChassisNumber { get; set; } = "";
        public string? PlateNumber { get; set; }

        public string? Manufacturer { get; set; }
        public string? ModelName { get; set; }
        public int? ModelYear { get; set; }
        public string? BusType { get; set; }

        public int SeatsCount { get; set; }
        public string? Color { get; set; }
        public string? Specs { get; set; }
        public string? Notes { get; set; }

        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        
        public DateTime? ArchivedAtCairo { get; set; }

    }
}
