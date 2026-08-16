using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NoufirTours.Models.Trips.Buses
{
    public class BusCreateEditMode : IValidatableObject
    {
        public Guid? Id { get; set; }

        [Required, MaxLength(30)]
        [Display(Name = "Bus Number")]
        public string BusNumber { get; set; } = "";

        [Required, MaxLength(80)]
        [Display(Name = "Chassis Number")]
        public string ChassisNumber { get; set; } = "";

        [MaxLength(40)]
        [Display(Name = "Plate Number")]
        public string? PlateNumber { get; set; }

        [MaxLength(60)]
        public string? Manufacturer { get; set; }

        [MaxLength(80)]
        [Display(Name = "Model")]
        public string? ModelName { get; set; }

        [Display(Name = "Model Year")]
        public int? ModelYear { get; set; }

        [MaxLength(60)]
        [Display(Name = "Bus Type")]
        public string? BusType { get; set; }

        [Display(Name = "Seats Count")]
        public int? SeatsCount { get; set; }

        [MaxLength(40)]
        public string? Color { get; set; }

        [MaxLength(1000)]
        public string? Specs { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Range(1, 30)]
        [Display(Name = "Layout Width")]
        public int LayoutWidth { get; set; } = 4;

        [Range(1, 60)]
        [Display(Name = "Layout Height")]
        public int LayoutHeight { get; set; } = 6;

        [Required(ErrorMessage = "Please design the layout (add at least one seat).")]
        public string SeatsJson { get; set; } = "[]";

        public bool LockCoreFields { get; set; }   // bus used in any trip
        public bool LockAllFields { get; set; }    // bus archived/deleted => no edit at all
        public bool IsArchived { get; set; }       // helpful for UI

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (string.IsNullOrWhiteSpace(SeatsJson))
            {
                results.Add(new ValidationResult("Please design the layout (add at least one seat).", new[] { nameof(SeatsJson) }));
                return results;
            }

            try
            {
                using var doc = JsonDocument.Parse(SeatsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    results.Add(new ValidationResult("Invalid layout JSON.", new[] { nameof(SeatsJson) }));
                    return results;
                }

                bool hasSeat = false;
                int driverCount = 0;
                int assistantCount = 0;
                int seatCount = 0;

                var seatCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var type = "";
                    if (el.TryGetProperty("type", out var tp)) type = tp.GetString() ?? "";

                    if (type == "Seat")
                    {
                        hasSeat = true;
                        seatCount++;

                        var role = el.TryGetProperty("role", out var rp) ? rp.GetString() ?? "Passenger" : "Passenger";

                        if (string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase)) driverCount++;
                        if (string.Equals(role, "Assistant", StringComparison.OrdinalIgnoreCase)) assistantCount++;

                        var code = "";
                        if (el.TryGetProperty("code", out var sc)) code = sc.GetString() ?? "";

                        code = (code ?? "").Trim();
                        if (role != "Driver" && role != "Assistant" && string.IsNullOrWhiteSpace(code))
                        {
                            results.Add(new ValidationResult($"Passenger seat #{seatCount} must have a Seat Code.", new[] { nameof(SeatsJson) }));
                            break;
                        }

                        if (!string.IsNullOrWhiteSpace(code) && !seatCodes.Add(code))
                        {
                            results.Add(new ValidationResult($"Seat code '{code}' is duplicated.", new[] { nameof(SeatsJson) }));
                            break;
                        }
                    }
                }

                if (!hasSeat)
                    results.Add(new ValidationResult("Please add at least one Seat in the layout.", new[] { nameof(SeatsJson) }));

                if (driverCount > 1)
                    results.Add(new ValidationResult("Only one Driver seat is allowed.", new[] { nameof(SeatsJson) }));

                if (assistantCount > 1)
                    results.Add(new ValidationResult("Only one Assistant seat is allowed.", new[] { nameof(SeatsJson) }));
            }
            catch
            {
                results.Add(new ValidationResult("Invalid layout JSON.", new[] { nameof(SeatsJson) }));
            }

            return results;
        }
    }
}