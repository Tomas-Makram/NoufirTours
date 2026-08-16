namespace NoufirTours.Models.Home.Settings
{
    public class BookingFinanceRowModel
    {
        public Guid BookingId { get; set; }
        public long CreatedAtUnix { get; set; }
        public string CreatedAtText { get; set; } = "";

        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }

        public bool IsCanceled { get; set; }
        public string StatusText { get; set; } = "";
    }
}