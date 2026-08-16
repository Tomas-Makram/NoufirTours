using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Bookings
{
    public class BookingCreateInputModel
    {
        public Guid TripId { get; set; }

        // 1=Go 2=Return 3=Round
        public int BookingType { get; set; } = 1;

        public Guid? ReturnTripId { get; set; }

        [Required]
        public string CustomerName { get; set; } = "";

        [Required]
        public string Phone { get; set; } = "";

        public string CompanyFrom { get; set; } = "";

        public string SeatsMainCsv { get; set; } = "";
        public string? SeatsReturnCsv { get; set; }

        public int RequiredMainSeats { get; set; } = 1;
        public int RequiredReturnSeats { get; set; } = 1;

        [Required(ErrorMessage = "Location required access")]
        public string DestinationPlaceName { get; set; } = "";

        public string? ReturnDestinationPlaceName { get; set; }

        // Descrition
        public string? Description { get; set; }
    }
}
