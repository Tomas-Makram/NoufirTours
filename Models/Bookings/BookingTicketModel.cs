namespace NoufirTours.Models.Bookings
{
    public class BookingTicketModel
    {
        public Guid BookingId { get; set; }
        public string? TicketCode { get; set; }

        public string? phoneCompany { get; set; }

        public string CustomerName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string CompanyFrom { get; set; } = "";

        public int BookingType { get; set; }
        public string BookingTypeLabel { get; set; } = "";
        public string CreatedAtText { get; set; } = "";

        public Guid MainTripId { get; set; }
        public string MainTripName { get; set; } = "";
        public string MainFromCity { get; set; } = "";
        public string MainToCity { get; set; } = "";
        public string MainTripDate { get; set; } = "";
        public string MainTripTime { get; set; } = "";
        public string MainBusName { get; set; } = "-";
        public string MainSeatsCsv { get; set; } = "";
        public int MainSeatsCount { get; set; }
        public decimal MainSeatPrice { get; set; }
        public decimal MainAmount { get; set; }

        public Guid? ReturnTripId { get; set; }
        public string ReturnTripName { get; set; } = "";
        public string ReturnFromCity { get; set; } = "";
        public string ReturnToCity { get; set; } = "";
        public string ReturnTripDate { get; set; } = "";
        public string ReturnTripTime { get; set; } = "";
        public string ReturnBusName { get; set; } = "-";
        public string ReturnSeatsCsv { get; set; } = "";
        public int ReturnSeatsCount { get; set; }
        public decimal ReturnSeatPrice { get; set; }
        public decimal ReturnAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public List<SeatPosModel> SeatsPositions { get; set; } = new();

        // Destination
        public string? DestinationPlaceName { get; set; }
        public string? ReturnDestinationPlaceName { get; set; }

        // Descrition
        public string? Description { get; set; }
    }
}
