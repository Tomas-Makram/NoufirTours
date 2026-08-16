namespace NoufirTours.Models.Bookings
{
    public sealed class BookingIndexModel
    {
        public string? FromCity { get; set; }
        public string? ToCity { get; set; }
        public string? Date { get; set; } // yyyy-MM-dd

        public int? SeatsCount { get; set; }
        public string? SelectedType { get; set; }   // "Go" | "Return" | "Round"
        public string? ReturnDate { get; set; }     // yyyy-MM-dd
        public int? ReturnSeatsCount { get; set; }

        public bool HasSearched { get; set; }
        public string? ErrorMessage { get; set; }

        public List<string> AvailableCities { get; set; } = new();
        public List<DayShortcutModel> WeekShortcuts { get; set; } = new();

        // GO results
        public List<TripSearchRowModel> Results { get; set; } = new();

        // RETURN results (only when Round)
        public List<TripSearchRowModel> ReturnResults { get; set; } = new();
    }
}
