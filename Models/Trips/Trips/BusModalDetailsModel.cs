namespace NoufirTours.Models.Trips.Trips
{
    public class BusModalDetailsModel
    {
        public Guid Id { get; set; }
        public string? BusNumber { get; set; }
        public string? PlateNumber { get; set; }
        public string? ChassisNumber { get; set; }

        public string? Manufacturer { get; set; }
        public string? ModelName { get; set; }
        public int? ModelYear { get; set; }
        public string? BusType { get; set; }

        public int LayoutWidth { get; set; }
        public int LayoutHeight { get; set; }

        public int SeatsCount { get; set; }
        public int SeatsActive { get; set; }

        public string? Color { get; set; }
        public string? Specs { get; set; }
        public string? Notes { get; set; }

        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }

        public List<BusSeatModalRow> Seats { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }
 
    public class BusSeatModalRow
    {
        public string? ElementType { get; set; }
        public string? SeatCode { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsActive { get; set; }
        public string? Role { get; set; }
        public string? Label { get; set; }

        public string? DoorSide { get; set; }
        public double? DoorOffset { get; set; }
    }
}