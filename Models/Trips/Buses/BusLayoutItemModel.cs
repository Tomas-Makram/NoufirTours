using System.Text.Json.Serialization;

namespace NoufirTours.Models.Trips.Buses
{
    public class BusLayoutItemModel
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Seat"; // Seat, Door, WC

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; } // Door/WC

        [JsonPropertyName("side")]
        public string? Side { get; set; } // Door

        [JsonPropertyName("offset")]
        public double? Offset { get; set; } // Door
    }
}