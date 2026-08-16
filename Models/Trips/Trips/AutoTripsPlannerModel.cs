using Microsoft.AspNetCore.Mvc.Rendering;
using NoufirTours.Data;
using System.Collections.Generic;

namespace NoufirTours.Models.Trips.Trips
{
    public class AutoTripsPlannerModel
    {
        public Guid PlanId { get; set; }
        public string Name { get; set; } = "Default Plan";

        public string? Notes { get; set; }
        public bool IsEnabled { get; set; }

        public int ScheduleType { get; set; }
        public string? SpecificDate { get; set; }

        public int ActivationMode { get; set; }

        public string? CheckDate { get; set; }

        public int AvailableBusesCount { get; set; }
        public int AvailableDriversCount { get; set; }
        public int PossibleTripsCount { get; set; }
        public string? LimitingResource { get; set; }

        public int DisplaySlots { get; set; }

        public List<AutoTripTemplateRowModel> Templates { get; set; } = new();
    }
}