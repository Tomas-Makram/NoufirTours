using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Buses
{
    public class BusSeatModel
    {
        [Required, MaxLength(20)]
        public string SeatCode { get; set; } = "";

        public int X { get; set; }
        public int Y { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(20)]
        public string Role { get; set; } = "Passenger";

        public Guid? AssignedDriverId { get; set; }

        public string? ElementType { get; set; }
        public string? Label { get; set; }
        public string? DoorSide { get; set; }
        public double? DoorOffset { get; set; }

    }
}