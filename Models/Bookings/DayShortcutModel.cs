using System;
using System.Collections.Generic;

namespace NoufirTours.Models.Bookings
{
    public class DayShortcutModel
    {
        public string Label { get; set; } = "";   // "Saturday"
        public string DateIso { get; set; } = ""; // yyyy-MM-dd
        public bool IsActive { get; set; }
    }
}
