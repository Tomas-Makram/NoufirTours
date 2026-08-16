namespace NoufirTours.Models.Bookings
{
    public class SeatCellModel
    {
        public int X { get; set; }
        public int Y { get; set; }

        public string? ElementType { get; set; } // Seat / WC / Aisle ...
        public string? SeatCode { get; set; }

        public bool IsSelectable { get; set; }

        public bool IsActive { get; set; } = true;
        public string? Role { get; set; } // Passenger / Driver / Assistant

        public bool HasDoor { get; set; }
        public string? DoorSide { get; set; }   // L/R/T/B
        public double? DoorOffset { get; set; } // 0..1
    }
}
