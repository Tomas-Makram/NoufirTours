using Microsoft.AspNetCore.Mvc.Rendering;
using NoufirTours.Data;
using System.ComponentModel.DataAnnotations;

namespace NoufirTours.Models.Trips.Trips
{
    public class AutoTripTemplateModalModel
    {
        public Guid PlanId { get; set; }
        public int OrderNo { get; set; }
        public Guid? ItemId { get; set; } // existing item id

        public string? CheckDate { get; set; } // yyyy-MM-dd (optional)

        public bool IsEnabled { get; set; }
        public bool AutoEveryDay { get; set; }

        [Required] public string TripName { get; set; } = "Trip";
        [Required] public string DepartTime { get; set; } = "05:00";

        public TripPriceType PriceType { get; set; } = TripPriceType.Round;

        public string? FromCity { get; set; }
        public string? ToCity { get; set; }

        public string? PickupPlace { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLon { get; set; }

        public string? DropoffPlace { get; set; }
        public string? Notes { get; set; }

        public decimal? SeatPriceGo { get; set; }
        public decimal? SeatPriceReturn { get; set; }
        public decimal? SeatPriceRound { get; set; }

        public Guid? BusId { get; set; }
        public Guid? DriverId { get; set; }

        public bool IsAvailableForDate { get; set; } = true;
        public string? UnavailableReason { get; set; }

        public List<SelectListItem> Buses { get; set; } = new();
        public List<SelectListItem> Drivers { get; set; } = new();
    }
}