namespace NoufirTours.Models.Home.Settings
{
    public class FinanceSummaryModel
    {
        public decimal TotalCollected { get; set; }
        public int CollectionsCount { get; set; }
        public decimal TotalCanceledPaidAmount { get; set; }
        public int CanceledCount { get; set; }
    }
}
