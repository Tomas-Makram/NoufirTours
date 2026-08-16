using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Dashboard
{
    public class DashboardFilterModel
    {
        [Display(Name = "From")]
        public string? From { get; set; }

        [Display(Name = "To")]
        public string? To { get; set; }

        [Display(Name = "Company")]
        public string? Company { get; set; }

        public bool AllBookings { get; set; } = false;
        public bool IncludeArchivedTrips { get; set; } = false;
    }
}
