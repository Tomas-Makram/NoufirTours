namespace NoufirTours.Models.Trips.Buses
{
    public class BusDetailsModel
    {
        public BusDetailsHeaderModel Bus { get; set; } = new();
        public int LayoutWidth { get; set; }
        public int LayoutHeight { get; set; }
        public List<BusSeatModel> Seats { get; set; } = new();
        public List<BusTripSummaryModel> Trips { get; set; } = new();

    }

    public class BusTripSummaryModel
    {
        public Guid TripId { get; set; }
        public string Title { get; set; } = "";
        public string? DepartDate { get; set; }
        public string? DepartTime { get; set; }
        public bool IsArchived { get; set; }
    }
}
