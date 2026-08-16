namespace NoufirTours.Models.Trips.Buses
{
    public class BusListItemModel
    {
        public Guid Id { get; set; }
        public string BusNumber { get; set; } = "";
        public string ChassisNumber { get; set; } = "";
        public string? PlateNumber { get; set; }
        public string? ModelName { get; set; }
        public int? ModelYear { get; set; }
        public int SeatsCount { get; set; }
        public bool IsActive { get; set; }
    }
}
