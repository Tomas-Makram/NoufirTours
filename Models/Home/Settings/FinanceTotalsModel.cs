namespace NoufirTours.Models.Home.Settings
{
    public class FinanceTotalsModel
    {
        public int TotalBookings { get; set; }
        public int ActiveBookings { get; set; }
        public int CanceledBookings { get; set; }

        public decimal ActiveTotalAmount { get; set; }
        public decimal ActivePaidAmount { get; set; }
        public decimal ActiveDueAmount { get; set; }

        public decimal CanceledTotalAmount { get; set; }
        public decimal CanceledPaidAmount { get; set; }
    }
}
