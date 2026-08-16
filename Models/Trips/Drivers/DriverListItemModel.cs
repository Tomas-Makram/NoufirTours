namespace NoufirTours.Models.Trips.Drivers
{
    public class DriverListItemModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string NationalId { get; set; } = "";
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public string PrimaryPhone { get; set; } = "";
    }
}
