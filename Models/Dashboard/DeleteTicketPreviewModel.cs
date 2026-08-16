namespace NoufirTours.Models.Dashboard
{
    public class DeleteTicketPreviewModel
    {
        public bool Found { get; set; }
        public bool CanDelete { get; set; }
        public string? ErrorMessage { get; set; }

        public string? Code { get; set; }
        public Guid BookingId { get; set; }

        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? CompanyFrom { get; set; }

        public string? BookingType { get; set; }
        public string? MainSeats { get; set; }
        public string? ReturnSeats { get; set; }

        public string? MainTripName { get; set; }
        public string? MainTripDate { get; set; }
        public string? MainTripTime { get; set; }
        public string? MainRoute { get; set; }

        public string? ReturnTripName { get; set; }
        public string? ReturnTripDate { get; set; }
        public string? ReturnTripTime { get; set; }
        public string? ReturnRoute { get; set; }

        public decimal PaidAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string? Notes { get; set; }
        public string? DeleteDeadlineText { get; set; }
    }
}