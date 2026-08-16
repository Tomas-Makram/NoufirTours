using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using NoufirTours.Models.Bookings;
using NoufirTours.Services;
using QRCoder;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace NoufirTours.Controllers
{
    [RequireUserId]
    [Authorize(Roles = "staff")]
    [Authorize]
    public class BookingController : Controller
    {
        private readonly DBContext _db;
        private readonly IDailyWork _dailyWork;
        private const string V_Index = "Views/Booking/Index.cshtml";
        private const string V_Details = "Views/Booking/Details.cshtml";
        private const string V_Ticket = "Views/Booking/Ticket.cshtml";
        private const int BOOKING_CLOSE_AFTER_HOURS = 9;

        public BookingController(DBContext db, IDailyWork dailyWork)
        {
            _db = db;
            _dailyWork = dailyWork;
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////////// Time ZONE ////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private Guid GetCurrentUserId()
        {
            var idStr = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        private static readonly TimeZoneInfo CairoTz = GetCairoTimeZone();

        private static DateTime CairoToday()
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CairoTz);
            return now.Date;
        }

        private static DateTime CairoNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CairoTz);
        }

        private static TimeZoneInfo GetCairoTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        }

        private static DateTime CairoFromUnix(long unixSeconds)
        {
            var dtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            return TimeZoneInfo.ConvertTimeFromUtc(dtUtc, CairoTz);
        }

        private static long UtcNowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static bool IsTripClosedForBooking(string? departDateIso, string? departTime, DateTime nowCairo)
        {
            if (!TryParseTripDepartCairo(departDateIso, departTime, out var departCairo))
                return false;

            return nowCairo >= departCairo.AddHours(BOOKING_CLOSE_AFTER_HOURS);
        }

        private static bool TryParseTripDepartCairo(string? dateIso, string? time, out DateTime departCairo)
        {
            departCairo = default;

            var d = (dateIso ?? "").Trim();
            var t = (time ?? "").Trim();

            if (string.IsNullOrWhiteSpace(d) || string.IsNullOrWhiteSpace(t))
                return false;

            string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss" };

            return DateTime.TryParseExact(
                $"{d} {t}",
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out departCairo
            );
        }

        //--------------------------------------------------------------------------------------//
        // ////////////////////////////////////// AUDIT LOG //////////////////////////////////////
        //--------------------------------------------------------------------------------------//

        private static string? AuditJson(object? obj, int maxLen = 4000)
        {
            if (obj == null) return null;

            try
            {
                var json = JsonSerializer.Serialize(obj);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return json.Length <= maxLen ? json : json.Substring(0, maxLen);
            }
            catch
            {
                return obj.ToString();
            }
        }

        private async Task WriteAuditAsync(string action, string entity, Guid? entityId = null, object? detailsObj = null, CancellationToken ct = default)
        {
            var uid = GetCurrentUserId();
            if (uid == Guid.Empty) return;

            try
            {
                var details = AuditJson(detailsObj);

                await _db.Set<NoufirTours.Data.AuditLog>().AddAsync(new NoufirTours.Data.AuditLog
                {
                    UserId = uid,
                    Action = (action ?? "").Trim(),
                    Entity = (entity ?? "").Trim(),
                    EntityId = entityId.ToString(),
                    Details = string.IsNullOrWhiteSpace(details) ? null : details,
                    CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }, ct);

                await _db.SaveChangesAsync(ct);
            }
            catch
            {
            }
        }

        //---------------------------------------------------------------------------------------//
        /////////////////////////////////////// SEAT HELPERS //////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static HashSet<string> ParseSeatsCsv(string? csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv)) return set;

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                    set.Add(part.Trim());
            }
            return set;
        }

        private static string NormalizeSeatsCsv(IEnumerable<string> seats)
        {
            var cleaned = seats
                .Select(s => (s ?? "").Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return string.Join(",", cleaned);
        }

        private HashSet<string> GetAllSelectablePassengerSeatCodes(Bus bus)
        {
            return bus.Seats
                .Where(s =>
                    (s.ElementType ?? "Seat").Equals("Seat", StringComparison.OrdinalIgnoreCase) &&
                    (s.Role ?? "Passenger").Equals("Passenger", StringComparison.OrdinalIgnoreCase) &&
                    s.IsActiveInt == 1 &&
                    !string.IsNullOrWhiteSpace(s.SeatCode))
                .Select(s => s.SeatCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<string>> LoadTripDestinationSuggestions(Guid tripId, CancellationToken ct)
        {
            var list = await _db.TripPlaces
                .AsNoTracking()
                .Where(p => p.TripId == tripId && p.IsActiveInt == 1)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.PlaceName)
                .ToListAsync(ct);

            return list.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList()!;
        }

        //---------------------------------------------------------------------------------------//
        /////////////////////////////////////// UI HELPERS ////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static List<DayShortcutModel> BuildWeekShortcuts(DateTime cairoToday, string? activeIso)
        {
            // Week starts Saturday
            int diff = ((int)cairoToday.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
            var weekStart = cairoToday.AddDays(-diff);

            string[] labels = new[]
            {
                "Saturday","Sunday","Monday","Tuesday","Wednesday","Thursday","Friday"
            };

            var list = new List<DayShortcutModel>(7);
            for (int i = 0; i < 7; i++)
            {
                var d = weekStart.AddDays(i);
                var iso = d.ToString("yyyy-MM-dd");
                list.Add(new DayShortcutModel
                {
                    Label = labels[i],
                    DateIso = iso,
                    IsActive = !string.IsNullOrWhiteSpace(activeIso) && string.Equals(activeIso, iso, StringComparison.Ordinal)
                });
            }
            return list;
        }

        private async Task<List<string>> GetAllAvailableCities()
        {
            var cairoToday = CairoToday();
            var todayIso = cairoToday.ToString("yyyy-MM-dd");

            // Cities from active trips (today and future)
            var tripCities = await _db.Trips
                .AsNoTracking()
                .Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.IsActiveInt == 1 &&
                    string.Compare(t.DepartDate ?? "", todayIso) >= 0)
                .Select(t => new
                {
                    FromCity = t.FromCity,
                    ToCity = t.ToCity
                })
                .ToListAsync();

            // Cities from enabled auto-plan items
            var planCities = await _db.AutoTripPlanItems
                .AsNoTracking()
                .Include(i => i.Plan)
                .Where(i =>
                    i.IsEnabledInt == 1 &&
                    i.Plan != null &&
                    i.Plan.IsEnabledInt == 1)
                .Select(i => new
                {
                    FromCity = i.FromCity,
                    ToCity = i.ToCity
                })
                .ToListAsync();

            // Merge both sources together
            var cities = tripCities
                .SelectMany(x => new[] { x.FromCity, x.ToCity })
                .Concat(planCities.SelectMany(x => new[] { x.FromCity, x.ToCity }))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            return cities;
        }

        private static int[] AllowedPriceTypesForSearch(string? type, bool isReturnLeg)
        {
            type = (type ?? "").Trim();

            if (type.Equals("Round", StringComparison.OrdinalIgnoreCase))
            {
                return isReturnLeg
                    ? new[] { (int)TripPriceType.Return, (int)TripPriceType.Round }
                    : new[] { (int)TripPriceType.Go, (int)TripPriceType.Round };
            }

            if (type.Equals("Return", StringComparison.OrdinalIgnoreCase))
                return new[] { (int)TripPriceType.Return, (int)TripPriceType.Round };

            // default Go
            return new[] { (int)TripPriceType.Go, (int)TripPriceType.Round };
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// SESSION (DETAILS) //////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private const string SelSessionKey = "BK_DETAILS_SELECTION";

        private record DetailsSelection(Guid TripId, string Type, int Seats, Guid? ReturnTripId, int ReturnSeats);

        private void SaveSelectionToSession(DetailsSelection sel)
        {
            HttpContext.Session.SetString(SelSessionKey, JsonSerializer.Serialize(sel));
        }

        private DetailsSelection? ReadSelectionFromSession()
        {
            var s = HttpContext.Session.GetString(SelSessionKey);
            if (string.IsNullOrWhiteSpace(s)) return null;

            try { return JsonSerializer.Deserialize<DetailsSelection>(s); }
            catch { return null; }
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// NORMALIZATION //////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static string NormalizeName(string? s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return "";
            // collapse spaces
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(" ", parts);
        }

        private static string NormalizePhone(string? s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return "";
            // digits only
            var digits = new string(s.Where(char.IsDigit).ToArray());
            return digits;
        }

        private static string NormalizePlace(string? s)
        {
            s = (s ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return "";
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(" ", parts);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// CONTROLLER (PAGES) //////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Index(string? from, string? to, string? date, int? seats, string? type, string? returnDate, int? returnSeats)
        {
            var cairoToday = CairoToday();
            var tomorrowIso = cairoToday.AddDays(0).ToString("yyyy-MM-dd");

            // Cities dropdown
            var availableCities = await GetAllAvailableCities();
            if (availableCities.Count == 0)
                availableCities = new List<string> { "Asyut", "Hurgada", "Cairo", "Luxor", "Aswan" };

            var defaultFrom = availableCities.FirstOrDefault(c => c.ToLower().Contains("asyut")) ?? availableCities.FirstOrDefault();
            var defaultTo = availableCities.FirstOrDefault(c => c.ToLower().Contains("hurgada") || c.ToLower().Contains("hurghada"))
                            ?? (availableCities.Count > 1 ? availableCities[1] : availableCities.FirstOrDefault());

            from = string.IsNullOrWhiteSpace(from) ? defaultFrom : from.Trim();
            to = string.IsNullOrWhiteSpace(to) ? defaultTo : to.Trim();

            if (!string.IsNullOrWhiteSpace(from) &&
                !string.IsNullOrWhiteSpace(to) &&
                from.Equals(to, StringComparison.OrdinalIgnoreCase))
            {
                to = availableCities.FirstOrDefault(c => !c.Equals(from, StringComparison.OrdinalIgnoreCase)) ?? to;
            }

            date = (date ?? "").Trim();
            type = string.IsNullOrWhiteSpace(type) ? "Go" : type.Trim();
            returnDate = (returnDate ?? "").Trim();

            var goDateIso = string.IsNullOrWhiteSpace(date) ? tomorrowIso : date;

            var goSeats = (seats.HasValue && seats.Value > 0) ? seats.Value : 1;
            var retSeats = (returnSeats.HasValue && returnSeats.Value > 0) ? returnSeats.Value : 1;

            string defaultReturnDateIso;
            if (DateTime.TryParseExact(goDateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var goParsed))
                defaultReturnDateIso = goParsed.AddDays(3).ToString("yyyy-MM-dd");
            else
                defaultReturnDateIso = cairoToday.AddDays(3).ToString("yyyy-MM-dd");

            var vm = new BookingIndexModel
            {
                FromCity = from ?? "",
                ToCity = to ?? "",
                Date = goDateIso,
                SeatsCount = goSeats,
                SelectedType = type,
                ReturnDate = string.IsNullOrWhiteSpace(returnDate) ? defaultReturnDateIso : returnDate,
                ReturnSeatsCount = retSeats,
                HasSearched = false,
                AvailableCities = availableCities,
                Results = new List<TripSearchRowModel>(),
                ReturnResults = new List<TripSearchRowModel>()
            };

            vm.WeekShortcuts = BuildWeekShortcuts(cairoToday, vm.Date);

            // If user didn't search yet (no date in query string)
            if (string.IsNullOrWhiteSpace(date))
                return View(V_Index, vm);

            vm.HasSearched = true;

            if (!DateTime.TryParseExact(goDateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                vm.ErrorMessage = "Invalid date.";
                return View(V_Index, vm);
            }

            if (dateOnly.Date < cairoToday.Date)
            {
                vm.ErrorMessage = "Past dates are not allowed.";
                return View(V_Index, vm);
            }

            // Round Mode
            if (string.Equals(type, "Round", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure trips exist (auto plans) on both dates
                await _dailyWork.EnsureTripsForDateAsync(goDateIso, HttpContext.RequestAborted);
                await _dailyWork.EnsureTripsForDateAsync(vm.ReturnDate, HttpContext.RequestAborted);

                var goAllowed = AllowedPriceTypesForSearch("Round", isReturnLeg: false);
                var retAllowed = AllowedPriceTypesForSearch("Round", isReturnLeg: true);

                var (goRows, goErr) = await LoadTripsForSearchAsync(
                    dateIso: goDateIso,
                    from: from,
                    to: to,
                    allowedTypes: goAllowed,
                    neededSeats: goSeats,
                    allowRoundBothDirections: true,
                    leg: SeatLeg.Go,
                    ct: HttpContext.RequestAborted
                );

                if (!string.IsNullOrWhiteSpace(goErr))
                {
                    vm.ErrorMessage = goErr;
                    vm.Results = new List<TripSearchRowModel>();
                    vm.ReturnResults = new List<TripSearchRowModel>();
                    return View(V_Index, vm);
                }

                var (retRows, retErr) = await LoadTripsForSearchAsync(
                     dateIso: vm.ReturnDate,
                     from: to,
                     to: from,
                     allowedTypes: retAllowed,
                     neededSeats: retSeats,
                     allowRoundBothDirections: true,
                     leg: SeatLeg.Return,
                     ct: HttpContext.RequestAborted
                 );

                if (!string.IsNullOrWhiteSpace(retErr))
                {
                    vm.ErrorMessage = retErr;
                    vm.Results = new List<TripSearchRowModel>();
                    vm.ReturnResults = new List<TripSearchRowModel>();
                    return View(V_Index, vm);
                }

                vm.Results = goRows;
                vm.ReturnResults = retRows;
                return View(V_Index, vm);
            }

            // GO or RETURN Mode
            await _dailyWork.EnsureTripsForDateAsync(goDateIso, HttpContext.RequestAborted);

            var isReturnLegSearch = string.Equals(type, "Return", StringComparison.OrdinalIgnoreCase);
            var allowed = AllowedPriceTypesForSearch(type, isReturnLeg: isReturnLegSearch);

            var leg = string.Equals(type, "Return", StringComparison.OrdinalIgnoreCase)
                ? SeatLeg.Return
                : SeatLeg.Go;

            var (rows, err) = await LoadTripsForSearchAsync(
                dateIso: goDateIso,
                from: from,
                to: to,
                allowedTypes: allowed,
                neededSeats: goSeats,
                allowRoundBothDirections: true,
                leg: leg,
                ct: HttpContext.RequestAborted
            );

            if (!string.IsNullOrWhiteSpace(err))
            {
                vm.ErrorMessage = err;
                vm.Results = new List<TripSearchRowModel>();
                return View(V_Index, vm);
            }

            vm.Results = rows;
            return View(V_Index, vm);
        }

        [HttpGet]
        public async Task<IActionResult> TecSup()
        {
            var row = await _db.TechnicalSupports
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAtUnix)
                .FirstOrDefaultAsync();

            row ??= new TechnicalSupport
            {
                CompanyPhone = "—",
                ComplaintsPhone = "—",
                UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            return View("Views/Booking/TecSup.cshtml", row);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var trip = await _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus).ThenInclude(b => b!.Seats)
                .FirstOrDefaultAsync(t => t.Id == id && t.IsArchivedInt == 0);

            if (trip == null) return RedirectToAction(nameof(Index));

            var sel = ReadSelectionFromSession();

            if (sel == null || sel.TripId != id)
            {
                return RedirectToAction(nameof(Index), new
                {
                    date = trip.DepartDate,
                    from = trip.FromCity,
                    to = trip.ToCity,
                    type = "Go"
                });
            }

            TripPriceType mode =
                sel.Type.Equals("Return", StringComparison.OrdinalIgnoreCase) ? TripPriceType.Return :
                sel.Type.Equals("Round", StringComparison.OrdinalIgnoreCase) ? TripPriceType.Round :
                TripPriceType.Go;

            Guid? returnTripId = sel.ReturnTripId;
            if (mode == TripPriceType.Round && (!returnTripId.HasValue || returnTripId.Value == Guid.Empty))
            {
                return RedirectToAction(nameof(Index), new
                {
                    date = trip.DepartDate,
                    from = trip.FromCity,
                    to = trip.ToCity,
                    type = "Round"
                });
            }

            int reqMain = Math.Max(1, sel.Seats);
            int reqRet = Math.Max(1, sel.ReturnSeats);

            var vm = await BuildDetailsVM(trip, mode, returnTripId, reqMain, reqRet, HttpContext.RequestAborted);
            return View(V_Details, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Pick(Guid tripId, string? type, int? seats, Guid? returnTripId, int? returnSeats)
        {
            type = (type ?? "Go").Trim();

            int safeSeats = Math.Max(1, seats ?? 1);
            int safeRetSeats = Math.Max(1, returnSeats ?? 1);

            if (type.Equals("Round", StringComparison.OrdinalIgnoreCase))
            {
                if (!returnTripId.HasValue || returnTripId.Value == Guid.Empty)
                {
                    return RedirectToAction("Index", new { type = "Round" });
                }
            }
            else
            {
                returnTripId = null;
                safeRetSeats = 1;
            }

            var sel = new DetailsSelection(
                TripId: tripId,
                Type: type,
                Seats: safeSeats,
                ReturnTripId: returnTripId,
                ReturnSeats: safeRetSeats
            );

            SaveSelectionToSession(sel);

            return RedirectToAction(nameof(Details), new { id = tripId });
        }

        //--------------------------------------------------------------------------------------//
        // ///////////////////////////////////// GENERATE ID /////////////////////////////////////
        //--------------------------------------------------------------------------------------//

        private static readonly char[] Base32 = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        private static string NewTicketCode(int len = 8)
        {
            Span<byte> data = stackalloc byte[len];
            RandomNumberGenerator.Fill(data);

            Span<char> result = stackalloc char[len];

            for (int i = 0; i < len; i++)
            {
                result[i] = Base32[data[i] % Base32.Length];
            }

            return new string(result);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// CONTROLLER (CREATE) /////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingCreateInputModel input)
        {
            // Normalize early
            input.CustomerName = NormalizeName(input.CustomerName);
            input.Phone = NormalizePhone(input.Phone);
            input.DestinationPlaceName = NormalizePlace(input.DestinationPlaceName);
            input.ReturnDestinationPlaceName = NormalizePlace(input.ReturnDestinationPlaceName);

            // Load trip (main)
            var trip = await _db.Trips
                .Include(t => t.Bus).ThenInclude(b => b!.Seats)
                .FirstOrDefaultAsync(t => t.Id == input.TripId && t.IsArchivedInt == 0);

            if (trip == null)
                return RedirectToAction(nameof(Index));

            // Get username / company
            var userId = GetCurrentUserId();
            var username = await _db.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.Username)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized();

            input.CompanyFrom = username.Trim();

            // Basic required fields
            if (string.IsNullOrWhiteSpace(input.CustomerName) || string.IsNullOrWhiteSpace(input.Phone))
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Name and Phone are required.");

            if (string.IsNullOrWhiteSpace(input.DestinationPlaceName))
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Access location required.");

            // Seats parse
            var mainSeats = ParseSeatsCsv(input.SeatsMainCsv);
            var returnSeats = ParseSeatsCsv(input.SeatsReturnCsv);

            // Validate booking type + enforce Round requirements
            Trip? returnTrip = null;

            if (input.BookingType == 1) // Go
            {
                input.ReturnTripId = null;
                returnSeats.Clear();
            }
            else if (input.BookingType == 2) // Return (single)
            {
                input.ReturnTripId = null;
                returnSeats.Clear();
            }
            else // Round (3)
            {
                if (string.IsNullOrWhiteSpace(input.ReturnDestinationPlaceName))
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input,
                        "The return arrival location is required for (outbound and return).");

                if (!input.ReturnTripId.HasValue || input.ReturnTripId.Value == Guid.Empty)
                    return await Fail(trip, TripPriceType.Round, null, input, "Return trip is missing (select from Round page).");

                if (input.ReturnTripId.Value == trip.Id)
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Round must be 2 different trips (Go + Return).");

                // Load return trip
                returnTrip = await _db.Trips
                    .Include(t => t.Bus).ThenInclude(b => b!.Seats)
                    .FirstOrDefaultAsync(t => t.Id == input.ReturnTripId.Value && t.IsArchivedInt == 0);

                if (returnTrip == null)
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Return trip not found.");

                if (returnTrip.Bus == null)
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Return trip has no bus.");
            }

            if (trip.Bus == null)
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Trip has no bus.");

            // VALIDATE SEATS (MAIN LEG)
            var mainLeg = (input.BookingType == 2) ? SeatLeg.Return : SeatLeg.Go;

            var allSelectableMain = GetAllSelectablePassengerSeatCodes(trip.Bus);
            if (mainSeats.Any() && !mainSeats.All(s => allSelectableMain.Contains(s)))
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Invalid seat selection.");

            var unavailableMain = await GetUnavailableSeatsForTrip(trip.Id, mainLeg, HttpContext.RequestAborted);
            if (mainSeats.Any(s => unavailableMain.Contains(s)))
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Some seats are already booked.");

            var availableMain = allSelectableMain.Where(s => !unavailableMain.Contains(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int requestedMain = Math.Max(1, input.RequiredMainSeats);
            int minRequiredMain = Math.Min(requestedMain, availableMain.Count);

            if (mainSeats.Count < minRequiredMain)
            {
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input,
                    $"At least {minRequiredMain} seats must be selected for this flight. Currently available: {availableMain.Count}.");
            }

            // VALIDATE SEATS (RETURN LEG)
            if (input.BookingType == 3 && returnTrip != null)
            {
                var allSelectableRet = GetAllSelectablePassengerSeatCodes(returnTrip.Bus!);
                if (returnSeats.Any() && !returnSeats.All(s => allSelectableRet.Contains(s)))
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Invalid RETURN seat selection.");

                var unavailableRet = await GetUnavailableSeatsForTrip(returnTrip.Id, SeatLeg.Return, HttpContext.RequestAborted);
                if (returnSeats.Any(s => unavailableRet.Contains(s)))
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Some RETURN seats are already booked.");

                var availableRet = allSelectableRet.Where(s => !unavailableRet.Contains(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int requestedRet = Math.Max(1, input.RequiredReturnSeats);
                int minRequiredRet = Math.Min(requestedRet, availableRet.Count);

                if (returnSeats.Count < minRequiredRet)
                {
                    return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input,
                        $"{minRequiredRet} must select (at least) a seat for the return flight. Currently available: {availableRet.Count}.");
                }
            }

            // TRANSACTION (avoid races)
            using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

            try
            {
                // Re-check seat availability INSIDE transaction (important)
                var unavailableMain2 = await GetUnavailableSeatsForTrip(trip.Id, mainLeg, HttpContext.RequestAborted);
                if (mainSeats.Any(s => unavailableMain2.Contains(s)))
                    return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input, "Some seats were just booked by another user. Please reselect seats.");

                if (input.BookingType == 3 && returnTrip != null)
                {
                    var unavailableRet2 = await GetUnavailableSeatsForTrip(returnTrip.Id, SeatLeg.Return, HttpContext.RequestAborted);
                    if (returnSeats.Any(s => unavailableRet2.Contains(s)))
                        return await Fail(trip, TripPriceType.Round, input.ReturnTripId, input, "Some RETURN seats were just booked by another user. Please reselect seats.");
                }

                // Upsert Customer
                bool customerExists = await _db.Customers.AsNoTracking()
                    .AnyAsync(c => c.FullName == input.CustomerName && c.Phone == input.Phone);

                if (!customerExists)
                {
                    _db.Customers.Add(new Customer
                    {
                        FullName = input.CustomerName,
                        Phone = input.Phone,
                        CreatedAtUnix = UtcNowUnix()
                    });

                    try { await _db.SaveChangesAsync(); }
                    catch (DbUpdateException) { /* ignore unique race */ }
                }

                // Upsert destination place(s)
                var (destId, destName) = await UpsertDestinationPlaceAsync(
                    tripId: trip.Id,
                    placeName: input.DestinationPlaceName,
                    type: TripPlaceType.Dropoff,
                    ct: HttpContext.RequestAborted);

                Guid? retDestId = null;
                string? retDestName = null;

                if (input.BookingType == 3 && returnTrip != null)
                {
                    var r = await UpsertDestinationPlaceAsync(
                        tripId: returnTrip.Id,
                        placeName: input.ReturnDestinationPlaceName,
                        type: TripPlaceType.Dropoff,
                        ct: HttpContext.RequestAborted);

                    retDestId = r.placeId;
                    retDestName = r.placeName;
                }

                // Pricing
                decimal total;
                if (input.BookingType == 1) // Go
                    total = mainSeats.Count * trip.SeatPriceGo;
                else if (input.BookingType == 2) // Return single
                    total = mainSeats.Count * trip.SeatPriceReturn;
                else // Round
                    total = (mainSeats.Count * trip.SeatPriceGo) + (returnSeats.Count * (returnTrip?.SeatPriceReturn ?? 0m));

                var booking = new Booking
                {
                    TripId = trip.Id,
                    CustomerName = input.CustomerName.Trim(),
                    Phone = input.Phone.Trim(),
                    CompanyFrom = input.CompanyFrom.Trim(),

                    SeatsText = NormalizeSeatsCsv(mainSeats),
                    BookingTypeInt = input.BookingType,

                    ReturnTripId = input.BookingType == 3 ? input.ReturnTripId : null,
                    SeatsReturnText = input.BookingType == 3 ? NormalizeSeatsCsv(returnSeats) : null,

                    ReturnDateTime = (input.BookingType == 3 && returnTrip != null)
                        ? $"{returnTrip.DepartDate} {returnTrip.DepartTime}"
                        : null,

                    DestinationPlaceId = destId,
                    DestinationPlaceName = destName,

                    ReturnDestinationPlaceId = input.BookingType == 3 ? retDestId : null,
                    ReturnDestinationPlaceName = input.BookingType == 3 ? retDestName : null,

                    TotalAmount = total,
                    PaidAmount = 0m,
                    StatusInt = 0,
                    CreatedAtUnix = UtcNowUnix(),
                    CreatedByUserId = userId,
                    IsCanceledInt = 0,
                    Notes = string.IsNullOrWhiteSpace(input.Description) ? "لا توجد ملاحظات": input.Description,
                };

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                // ============================
                // ✅ NEW: Create unique Booking Code (booking_codes)
                // ============================
                string ticketCode = "";
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    ticketCode = NewTicketCode(8);

                    _db.BookingCodes.Add(new NoufirTours.Data.BookingCode
                    {
                        BookingId = booking.Id,
                        Code = ticketCode,
                        CreatedAtUnix = UtcNowUnix()
                    });

                    try
                    {
                        await _db.SaveChangesAsync(); // may throw if Code unique conflict
                        break; // success
                    }
                    catch (DbUpdateException)
                    {
                        // collision on unique Code -> retry with new code
                        _db.ChangeTracker.Clear();

                        // important: re-attach booking to keep transaction flow safe
                        _db.Attach(booking);
                        continue;
                    }
                }

                if (string.IsNullOrWhiteSpace(ticketCode))
                    return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input,
                        "Could not generate ticket code. Please try again.");

                await WriteAuditAsync("create", "booking", booking.Id, new
                {
                    bookingId = booking.Id,
                    tripId = booking.TripId,
                    bookingTypeInt = booking.BookingTypeInt,
                    customerName = booking.CustomerName,
                    phone = booking.Phone,
                    companyFrom = booking.CompanyFrom,
                    seatsMain = booking.SeatsText,
                    returnTripId = booking.ReturnTripId,
                    seatsReturn = booking.SeatsReturnText,
                    destination = booking.DestinationPlaceName,
                    returnDestination = booking.ReturnDestinationPlaceName,
                    total = booking.TotalAmount,
                    ticketCode = ticketCode
                });

                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return RedirectToAction(nameof(Ticket), new { id = booking.Id });
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                return await Fail(trip, (TripPriceType)input.BookingType, input.ReturnTripId, input,
                    $"Unexpected error while saving booking. {ex.Message}");
            }
        }

        private async Task<(List<TripSearchRowModel> Rows, string? ErrorMessage)> LoadTripsForSearchAsync(string dateIso, string? from, string? to, int[] allowedTypes, int neededSeats, bool allowRoundBothDirections, SeatLeg leg, CancellationToken ct)
        {
            dateIso = (dateIso ?? "").Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();

            allowedTypes ??= Array.Empty<int>();
            neededSeats = Math.Max(1, neededSeats);

            if (string.IsNullOrWhiteSpace(dateIso))
                return (new List<TripSearchRowModel>(), "Invalid date.");

            var q = _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus)
                .Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.DepartDate == dateIso &&
                    (
                        t.IsActiveInt == 1
                        || (t.IsActiveInt == 0 && t.TripOriginInt == (int)TripOrigin.AutoPlan)
                    )
                );

            if (allowedTypes.Length > 0)
                q = q.Where(t => allowedTypes.Contains(t.PriceTypeInt));

            // Route filter
            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                var f = from ?? "";
                var tt = to ?? "";

                if (!allowRoundBothDirections)
                {
                    if (!string.IsNullOrWhiteSpace(f))
                        q = q.Where(t => EF.Functions.Like(t.FromCity ?? "", $"%{f}%"));
                    if (!string.IsNullOrWhiteSpace(tt))
                        q = q.Where(t => EF.Functions.Like(t.ToCity ?? "", $"%{tt}%"));
                }
                else
                {
                    // Always allow Round both directions (عرض Round في الاتجاهين)
                    q = q.Where(t =>
                        (
                            (string.IsNullOrWhiteSpace(f) || EF.Functions.Like(t.FromCity ?? "", $"%{f}%")) &&
                            (string.IsNullOrWhiteSpace(tt) || EF.Functions.Like(t.ToCity ?? "", $"%{tt}%"))
                        )
                        ||
                        (
                            t.PriceTypeInt == (int)TripPriceType.Round &&
                            (string.IsNullOrWhiteSpace(tt) || EF.Functions.Like(t.FromCity ?? "", $"%{tt}%")) &&
                            (string.IsNullOrWhiteSpace(f) || EF.Functions.Like(t.ToCity ?? "", $"%{f}%"))
                        )
                    );
                }
            }

            var trips = await q
                .OrderBy(t => t.DepartTime)
                .ThenBy(t => t.Id)
                .Take(600)
                .Select(t => new
                {
                    t.Id,
                    t.TripName,
                    t.DepartDate,
                    t.DepartTime,
                    t.FromCity,
                    t.ToCity,
                    t.PriceTypeInt,
                    t.IsActiveInt,
                    t.TripOriginInt,
                    SeatsTotal = (t.Bus != null && t.Bus.SeatsCount.HasValue) ? t.Bus.SeatsCount.Value : 0,
                    t.SeatPriceGo,
                    t.SeatPriceReturn,
                })
                .ToListAsync(ct);

            var now = CairoNow();
            trips = trips
                .Where(t => !IsTripClosedForBooking(t.DepartDate, t.DepartTime, now))
                .ToList();

            if (trips.Count == 0)
                return (new List<TripSearchRowModel>(), "No Trips are available today.");

            var tripIds = trips.Select(x => x.Id).ToList();

            // Load bookings touching any of these trips (TripId OR ReturnTripId)
            var bookings = await _db.Bookings.AsNoTracking()
                .Where(b => b.IsCanceledInt == 0 &&
                            (tripIds.Contains(b.TripId) ||
                             (b.ReturnTripId.HasValue && tripIds.Contains(b.ReturnTripId.Value))))
                .Select(b => new
                {
                    b.TripId,
                    b.ReturnTripId,
                    b.BookingTypeInt,
                    b.SeatsText,
                    b.SeatsReturnText
                })
                .ToListAsync(ct);

            // booked seats per tripId FOR THIS LEG ONLY
            var bookedMap = new Dictionary<Guid, HashSet<string>>();

            void AddSeats(Guid tripId, string? csv)
            {
                if (!bookedMap.TryGetValue(tripId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bookedMap[tripId] = set;
                }

                foreach (var s in ParseSeatsCsv(csv))
                    set.Add(s);
            }

            foreach (var b in bookings)
            {
                // BookingType 1 = Go only => SeatsText on TripId
                if (b.BookingTypeInt == 1)
                {
                    if (leg == SeatLeg.Go && tripIds.Contains(b.TripId))
                        AddSeats(b.TripId, b.SeatsText);

                    continue;
                }

                // BookingType 2 = Return only => SeatsText on TripId but used as Return leg
                if (b.BookingTypeInt == 2)
                {
                    if (leg == SeatLeg.Return && tripIds.Contains(b.TripId))
                        AddSeats(b.TripId, b.SeatsText);

                    continue;
                }

                // BookingType 3 = Round:
                if (b.BookingTypeInt == 3)
                {
                    if (leg == SeatLeg.Go && tripIds.Contains(b.TripId))
                        AddSeats(b.TripId, b.SeatsText);

                    if (leg == SeatLeg.Return && b.ReturnTripId.HasValue && tripIds.Contains(b.ReturnTripId.Value))
                        AddSeats(b.ReturnTripId.Value, b.SeatsReturnText);
                }
            }

            // Build rows (with display flip for Round reverse)
            var rowsAll = trips.Select(t =>
            {
                var booked = bookedMap.TryGetValue(t.Id, out var set) ? set.Count : 0;
                var avail = Math.Max(0, t.SeatsTotal - booked);

                var outFrom = t.FromCity ?? "";
                var outTo = t.ToCity ?? "";

                // Flip display if Round matched reverse direction relative to search
                if (t.PriceTypeInt == (int)TripPriceType.Round &&
                    !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                {
                    bool matchedReverse =
                        (t.FromCity ?? "").Contains(to, StringComparison.OrdinalIgnoreCase) &&
                        (t.ToCity ?? "").Contains(from, StringComparison.OrdinalIgnoreCase);

                    if (matchedReverse)
                    {
                        outFrom = t.ToCity ?? "";
                        outTo = t.FromCity ?? "";
                    }
                }

                return new
                {
                    TripId = t.Id,
                    t.TripName,
                    t.DepartDate,
                    DepartTime = (t.DepartTime ?? "").Trim(),
                    FromCity = (outFrom ?? "").Trim(),
                    ToCity = (outTo ?? "").Trim(),
                    SeatsTotal = t.SeatsTotal,
                    SeatsBooked = booked,
                    Avail = avail,
                    IsActive = (t.IsActiveInt == 1),
                    PriceTypeInt = t.PriceTypeInt,
                    t.SeatPriceGo,
                    t.SeatPriceReturn,
                    Origin = t.TripOriginInt
                };
            }).ToList();

            var rowsAvail = rowsAll
                .Where(r => r.Avail > 0 )
                .OrderBy(r => r.Avail)
                .ThenBy(r => r.DepartTime)
                .ThenBy(r => r.TripId)
                .ToList();

            if (rowsAvail.Count == 0)
                return (new List<TripSearchRowModel>(), "No seats available.");

            // check total availability for this leg
            var totalAvail = rowsAvail.Sum(r => r.Avail);
            if (neededSeats > totalAvail)
            {
                return (new List<TripSearchRowModel>(),
                    $"The number of seats requested ({neededSeats}) is greater than the total number of seats available ({totalAvail}). There are not enough seats.");
            }

            // single best trip?
            var singleBest = rowsAvail
                .Where(r => r.Avail >= neededSeats)
                .OrderBy(r => r.Avail)
                .ThenBy(r => r.DepartTime)
                .ThenBy(r => r.TripId)
                .FirstOrDefault();

            bool isMultiTripCase = (singleBest == null);

            // helper: activate ONLY AutoPlan inactive
            async Task ActivateAutoPlanTripsAsync(IEnumerable<Guid> idsToActivate)
            {
                var ids = idsToActivate?.Distinct().ToList() ?? new List<Guid>();
                if (ids.Count == 0) return;

                var tracked = await _db.Trips
                    .Where(t =>
                        ids.Contains(t.Id) &&
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == dateIso &&
                        t.IsActiveInt == 0 &&
                        t.TripOriginInt == (int)TripOrigin.AutoPlan)
                    .ToListAsync(ct);

                foreach (var t in tracked)
                    t.IsActiveInt = 1;

                if (tracked.Count > 0)
                    await _db.SaveChangesAsync(ct);
            }

            if (!isMultiTripCase)
            {
                // de-dup by route+time
                string KeyOf(dynamic r) =>
                    $"{(r.FromCity ?? "").ToString().Trim().ToLowerInvariant()}|" +
                    $"{(r.ToCity ?? "").ToString().Trim().ToLowerInvariant()}|" +
                    $"{(r.DepartTime ?? "").ToString().Trim().ToLowerInvariant()}";

                var deduped = rowsAvail
                 .Where(r => r.Avail >= neededSeats)
                 .GroupBy(r => KeyOf(r))
                 .Select(g => g
                     .OrderBy(x => x.Avail)
                     .ThenBy(x => x.IsActive ? 0 : 1)
                     .ThenBy(x => x.TripId)
                     .First())
                 .OrderBy(r => r.Avail)
                 .ThenBy(r => r.DepartTime)
                 .ThenBy(r => r.TripId)
                 .ToList();

                // activate only if the best is inactive AND AutoPlan
                if (singleBest != null && !singleBest.IsActive)
                    await ActivateAutoPlanTripsAsync(new[] { singleBest.TripId });

                rowsAvail = deduped;
            }
            else
            {
                // multi trips case
                var sum = 0;
                var chosenIds = new List<Guid>();

                foreach (var r in rowsAvail)
                {
                    if (sum >= neededSeats) break;
                    chosenIds.Add(r.TripId);
                    sum += r.Avail;
                }

                // activate ONLY AutoPlan inactive among chosen
                var toActivate = rowsAll
                    .Where(r => chosenIds.Contains(r.TripId) && !r.IsActive && r.Origin == (int)TripOrigin.AutoPlan)
                    .Select(r => r.TripId)
                    .Distinct()
                    .ToList();

                if (toActivate.Count > 0)
                    await ActivateAutoPlanTripsAsync(toActivate);

                rowsAvail = rowsAvail.Where(r => chosenIds.Contains(r.TripId)).ToList();
            }

            // Build final VM list
            var final = rowsAvail.Select(r => new TripSearchRowModel
            {
                TripId = r.TripId,
                TripName = r.TripName ?? "",
                DepartDate = r.DepartDate ?? "",
                DepartTime = r.DepartTime ?? "",
                FromCity = r.FromCity ?? "",
                ToCity = r.ToCity ?? "",
                SeatsTotal = r.SeatsTotal,
                SeatsBooked = r.SeatsBooked,
                PriceType = (TripPriceType)r.PriceTypeInt,
                SeatPriceGo = r.SeatPriceGo,
                SeatPriceReturn = r.SeatPriceReturn,
            }).ToList();

            return (final, null);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// CUSTOMER (SUGGEST) /////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> CustomerSuggest(string? q)
        {
            q = (q ?? "").Trim();
            if (q.Length < 1) return Json(Array.Empty<object>());

            var qPhone = NormalizePhone(q);

            var items = await _db.Customers.AsNoTracking()
                .Where(c =>
                    EF.Functions.Like(c.FullName, $"%{q}%") ||
                    (!string.IsNullOrWhiteSpace(qPhone) && EF.Functions.Like(c.Phone, $"%{qPhone}%")))
                .OrderBy(c => c.FullName)
                .Take(10)
                .Select(c => new { name = c.FullName, phone = c.Phone })
                .ToListAsync();

            return Json(items);
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////////// TICKET / QR //////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static string FormatEgyptMobile(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "—";

            // digits only
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("20")) digits = "0" + digits.Substring(2);

            if (digits.Length != 11 || !digits.StartsWith("01")) return phone.Trim();

            return $"{digits.Substring(0, 4)} {digits.Substring(4, 3)} {digits.Substring(7, 4)}";
        }

        [HttpGet]
        public async Task<IActionResult> TicketQr(Guid id)
        {
            var exists = await _db.Bookings.AsNoTracking().AnyAsync(b => b.Id == id);
            if (!exists) return NotFound();

            var qrText = id.ToString();

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qrData).GetGraphic(12);

            return File(png, "image/png");
        }

        [HttpGet]
        public async Task<IActionResult> Ticket(Guid id)
        {
            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.CodeInfo)
                .Include(b => b.Trip).ThenInclude(t => t.Bus).ThenInclude(bus => bus!.Seats)
                .Include(b => b.ReturnTrip).ThenInclude(t => t!.Bus).ThenInclude(bus => bus!.Seats)
                .FirstOrDefaultAsync(b => b.Id == id);

            var phoneCompany = await _db.TechnicalSupports.AsNoTracking()
                .FirstOrDefaultAsync();

            if (booking == null) return RedirectToAction("Index");

            var createdAt = CairoFromUnix(booking.CreatedAtUnix).ToString("yyyy/MM/dd HH:mm");

            // seats lists
            var mainSeats = ParseSeatsCsv(booking.SeatsText).ToList();
            var retSeats = ParseSeatsCsv(booking.SeatsReturnText).ToList();

            // seat positions table
            var positions = new List<SeatPosModel>();

            if (booking.Trip?.Bus != null)
            {
                var map = booking.Trip.Bus.Seats
                    .Where(s => !string.IsNullOrWhiteSpace(s.SeatCode))
                    .ToDictionary(s => s.SeatCode!, s => (s.X, s.Y), StringComparer.OrdinalIgnoreCase);

                foreach (var s in mainSeats)
                    if (map.TryGetValue(s, out var p))
                        positions.Add(new SeatPosModel { Leg = "MAIN", SeatCode = s, X = p.X, Y = p.Y });
            }

            if (booking.ReturnTrip?.Bus != null)
            {
                var map = booking.ReturnTrip.Bus.Seats
                    .Where(s => !string.IsNullOrWhiteSpace(s.SeatCode))
                    .ToDictionary(s => s.SeatCode!, s => (s.X, s.Y), StringComparer.OrdinalIgnoreCase);

                foreach (var s in retSeats)
                    if (map.TryGetValue(s, out var p))
                        positions.Add(new SeatPosModel { Leg = "RETURN", SeatCode = s, X = p.X, Y = p.Y });
            }

            string typeLabel = booking.BookingTypeInt == 1 ? "Go"
                            : booking.BookingTypeInt == 2 ? "Return"
                            : "Round";

            // pricing
            decimal mainSeatPrice =
                booking.BookingTypeInt == 2 ? (booking.Trip?.SeatPriceReturn ?? 0m) : (booking.Trip?.SeatPriceGo ?? 0m);

            decimal mainAmount = mainSeats.Count * mainSeatPrice;

            decimal retSeatPrice = booking.ReturnTrip?.SeatPriceReturn ?? 0m;
            decimal retAmount = retSeats.Count * retSeatPrice;

            var total = booking.TotalAmount;

            // Route fix
            var mainFromCity = booking.Trip?.FromCity ?? "";
            var mainToCity = booking.Trip?.ToCity ?? "";

            if (booking.BookingTypeInt == 2)
            {
                var tmp = mainFromCity;
                mainFromCity = mainToCity;
                mainToCity = tmp;
            }

            var returnFromCity = booking.ReturnTrip?.FromCity ?? "";
            var returnToCity = booking.ReturnTrip?.ToCity ?? "";

            if (booking.BookingTypeInt == 3)
            {
                var tmp = returnFromCity;
                returnFromCity = returnToCity;
                returnToCity = tmp;
            }

            // time fix
            var mainTripTime = booking.Trip?.DepartTime ?? "";
            var returnTripTime = booking.ReturnTrip?.DepartTime ?? "";

            if (booking.BookingTypeInt == 2)
            {
                mainTripTime = "12:00 PM";
            }

            if (booking.BookingTypeInt == 3)
            {
                returnTripTime = "12:00 PM";
            }

            var vm = new BookingTicketModel
            {
                BookingId = booking.Id,

                CustomerName = booking.CustomerName,
                Phone = booking.Phone,
                CompanyFrom = booking.CompanyFrom,
                phoneCompany = FormatEgyptMobile(phoneCompany!.CompanyPhone),
                BookingType = booking.BookingTypeInt,
                BookingTypeLabel = typeLabel,

                CreatedAtText = createdAt,

                MainTripId = booking.TripId,
                MainTripName = booking.Trip?.TripName ?? "",
                MainFromCity = mainFromCity,
                MainToCity = mainToCity,
                MainTripDate = (booking.Trip?.DepartDate ?? "").Replace("-", "/"),
                MainTripTime = mainTripTime,
                MainBusName = booking.Trip?.Bus?.BusNumber ?? "-",
                MainSeatsCsv = string.Join(",", mainSeats),
                MainSeatsCount = mainSeats.Count,
                MainSeatPrice = mainSeatPrice,
                MainAmount = mainAmount,

                ReturnTripId = booking.ReturnTripId,
                ReturnTripName = booking.ReturnTrip?.TripName ?? "",
                ReturnFromCity = returnFromCity,
                ReturnToCity = returnToCity,
                ReturnTripDate = (booking.ReturnTrip?.DepartDate ?? "").Replace("-", "/"),
                ReturnTripTime = returnTripTime,
                ReturnBusName = booking.ReturnTrip?.Bus?.BusNumber ?? "-",
                ReturnSeatsCsv = string.Join(",", retSeats),
                ReturnSeatsCount = retSeats.Count,
                ReturnSeatPrice = retSeatPrice,
                ReturnAmount = retAmount,

                // show destination place(s)
                DestinationPlaceName = booking.DestinationPlaceName,
                ReturnDestinationPlaceName = booking.ReturnDestinationPlaceName,

                TotalAmount = total,
                SeatsPositions = positions,

                TicketCode = booking.CodeInfo?.Code,

                Description = string.IsNullOrWhiteSpace(booking.Notes) ? "لا توجد ملاحظات" : booking.Notes,
            };

            return View(V_Ticket, vm);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// TRIP PLACES (UPSERT) ///////////////////////////////
        //---------------------------------------------------------------------------------------//

        private async Task<(Guid? placeId, string placeName)> UpsertDestinationPlaceAsync(Guid tripId, string placeName, TripPlaceType type, CancellationToken ct)
        {
            placeName = NormalizePlace(placeName);
            if (string.IsNullOrWhiteSpace(placeName))
                return (null, "");

            var existing = await _db.TripPlaces
                .AsNoTracking()
                .Where(p => p.TripId == tripId && p.IsActiveInt == 1)
                .FirstOrDefaultAsync(p => (p.PlaceName ?? "").ToLower() == placeName.ToLower(), ct);

            if (existing != null)
                return (existing.Id, existing.PlaceName);

            var lastSort = await _db.TripPlaces
                .AsNoTracking()
                .Where(p => p.TripId == tripId)
                .Select(p => (int?)p.SortOrder)
                .MaxAsync(ct);

            var item = new TripPlace
            {
                TripId = tripId,
                PlaceName = placeName,
                PlaceTypeInt = (int)type,
                SortOrder = (lastSort ?? 0) + 1,
                IsActiveInt = 1
            };

            _db.TripPlaces.Add(item);
            await _db.SaveChangesAsync(ct);

            return (item.Id, item.PlaceName);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// DETAILS (VM BUILDER) ///////////////////////////////
        //---------------------------------------------------------------------------------------//

        private async Task<BookingDetailsModel> BuildDetailsVM(Trip trip, TripPriceType mode, Guid? returnTripId, int requiredMainSeats, int requiredReturnSeats, CancellationToken ct)
        {
            // Suggestions must be loaded BEFORE any early return
            var mainSug = await LoadTripDestinationSuggestions(trip.Id, ct);

            Trip? retTrip = null;
            List<string> retSug = new();

            var bus = trip.Bus;
            if (bus == null)
            {
                return new BookingDetailsModel
                {
                    BookingMode = mode,
                    TripId = trip.Id,
                    TripName = trip.TripName,
                    DepartDate = trip.DepartDate,
                    DepartTime = trip.DepartTime,
                    FromCity = trip.FromCity ?? "",
                    ToCity = trip.ToCity ?? "",
                    ErrorMessage = "This trip has no bus assigned.",
                    RequiredMainSeats = Math.Max(1, requiredMainSeats),
                    RequiredReturnSeats = Math.Max(1, requiredReturnSeats),

                    MainDestinationSuggestions = mainSug,
                    ReturnDestinationSuggestions = retSug,
                };
            }

            var mainLeg = (mode == TripPriceType.Return) ? SeatLeg.Return : SeatLeg.Go;

            var unavailableMain = await GetUnavailableSeatsForTrip(trip.Id, mainLeg, ct);
            var gridMain = BuildGrid(bus.LayoutWidth, bus.LayoutHeight, bus.Seats);

            // =========================
            // NEW: Clamp required main seats to actual available selectable seats
            // =========================
            var selectableMainCodes = bus.Seats
                .Where(s =>
                    (s.ElementType ?? "Seat").Equals("Seat", StringComparison.OrdinalIgnoreCase) &&
                    (s.Role ?? "Passenger").Equals("Passenger", StringComparison.OrdinalIgnoreCase) &&
                    s.IsActiveInt == 1 &&
                    !string.IsNullOrWhiteSpace(s.SeatCode))
                .Select(s => s.SeatCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var availableMainCount = selectableMainCodes.Count(code => !unavailableMain.Contains(code));

            // Keep old rule "minimum 1" when there are seats; if trip has 0 available then value becomes 0
            var safeRequiredMainSeats = availableMainCount > 0
                ? Math.Max(1, Math.Min(requiredMainSeats, availableMainCount))
                : 0;

            var mainDoors = bus.Seats
                .Where(s => string.Equals(s.ElementType, "Door", StringComparison.OrdinalIgnoreCase))
                .Select(d => new SeatCellModel
                {
                    X = d.X,
                    Y = d.Y,
                    ElementType = "Door",
                    HasDoor = true,
                    DoorSide = d.DoorSide,
                    DoorOffset = d.DoorOffset
                })
                .ToList();

            List<SeatCellModel> gridReturn = new();
            HashSet<string> unavailableReturn = new(StringComparer.OrdinalIgnoreCase);

            int retW = 0, retH = 0;
            Guid? retBusId = null;
            string retBusName = "-";

            // default return clamp keeps old behavior unless round trip exists
            var safeRequiredReturnSeats = Math.Max(1, requiredReturnSeats);

            if (mode == TripPriceType.Round)
            {
                retTrip = await _db.Trips
                    .AsNoTracking()
                    .Include(t => t.Bus).ThenInclude(b => b!.Seats)
                    .FirstOrDefaultAsync(t => t.Id == returnTripId!.Value && t.IsArchivedInt == 0, ct);

                if (retTrip == null)
                {
                    return new BookingDetailsModel
                    {
                        BookingMode = mode,
                        TripId = trip.Id,
                        TripName = trip.TripName,
                        DepartDate = trip.DepartDate,
                        DepartTime = trip.DepartTime,
                        FromCity = trip.FromCity ?? "",
                        ToCity = trip.ToCity ?? "",
                        PickupPlace = trip.PickupPlace,
                        DropoffPlace = trip.DropoffPlace,
                        BusId = bus.Id,
                        BusName = bus.BusNumber,
                        LayoutW = bus.LayoutWidth,
                        LayoutH = bus.LayoutHeight,
                        GridMain = gridMain,
                        UnavailableMain = unavailableMain,
                        MainDoors = mainDoors,
                        ErrorMessage = "Return trip not found.",
                        RequiredMainSeats = safeRequiredMainSeats,
                        RequiredReturnSeats = safeRequiredReturnSeats,

                        MainDestinationSuggestions = mainSug,
                        ReturnDestinationSuggestions = retSug,
                    };
                }

                if (retTrip.Bus == null)
                {
                    return new BookingDetailsModel
                    {
                        BookingMode = mode,
                        TripId = trip.Id,
                        TripName = trip.TripName,
                        DepartDate = trip.DepartDate,
                        DepartTime = trip.DepartTime,
                        FromCity = trip.FromCity ?? "",
                        ToCity = trip.ToCity ?? "",
                        PickupPlace = trip.PickupPlace,
                        DropoffPlace = trip.DropoffPlace,
                        BusId = bus.Id,
                        BusName = bus.BusNumber,
                        LayoutW = bus.LayoutWidth,
                        LayoutH = bus.LayoutHeight,
                        GridMain = gridMain,
                        UnavailableMain = unavailableMain,
                        MainDoors = mainDoors,
                        ErrorMessage = "Return trip has no bus assigned.",
                        RequiredMainSeats = safeRequiredMainSeats,
                        RequiredReturnSeats = safeRequiredReturnSeats,

                        MainDestinationSuggestions = mainSug,
                        ReturnDestinationSuggestions = retSug,
                    };
                }

                // Load return suggestions only if round + retTrip exists
                retSug = await LoadTripDestinationSuggestions(retTrip.Id, ct);

                retW = retTrip.Bus.LayoutWidth;
                retH = retTrip.Bus.LayoutHeight;
                retBusId = retTrip.Bus.Id;
                retBusName = retTrip.Bus.BusNumber;

                unavailableReturn = await GetUnavailableSeatsForTrip(retTrip.Id, SeatLeg.Return, ct);
                gridReturn = BuildGrid(retW, retH, retTrip.Bus.Seats);

                // =========================
                // NEW: Clamp required return seats to actual available selectable seats
                // =========================
                var selectableReturnCodes = retTrip.Bus.Seats
                    .Where(s =>
                        (s.ElementType ?? "Seat").Equals("Seat", StringComparison.OrdinalIgnoreCase) &&
                        (s.Role ?? "Passenger").Equals("Passenger", StringComparison.OrdinalIgnoreCase) &&
                        s.IsActiveInt == 1 &&
                        !string.IsNullOrWhiteSpace(s.SeatCode))
                    .Select(s => s.SeatCode!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var availableReturnCount = selectableReturnCodes.Count(code => !unavailableReturn.Contains(code));

                safeRequiredReturnSeats = availableReturnCount > 0
                    ? Math.Max(1, Math.Min(requiredReturnSeats, availableReturnCount))
                    : 0;
            }

            int bookingTypeInt =
                mode == TripPriceType.Go ? 1 :
                mode == TripPriceType.Return ? 2 : 3;

            return new BookingDetailsModel
            {
                BookingMode = mode,

                RequiredMainSeats = safeRequiredMainSeats,
                RequiredReturnSeats = safeRequiredReturnSeats,

                TripId = trip.Id,
                TripName = trip.TripName,
                DepartDate = trip.DepartDate,
                DepartTime = trip.DepartTime,
                FromCity = trip.FromCity ?? "",
                ToCity = trip.ToCity ?? "",
                PickupPlace = trip.PickupPlace,
                DropoffPlace = trip.DropoffPlace,

                BusId = bus.Id,
                BusName = bus.BusNumber,
                LayoutW = bus.LayoutWidth,
                LayoutH = bus.LayoutHeight,

                SeatPriceGo = trip.SeatPriceGo,
                SeatPriceReturn = trip.SeatPriceReturn,

                GridMain = gridMain,
                UnavailableMain = unavailableMain,
                MainDoors = mainDoors,

                ReturnTripId = retTrip?.Id,
                ReturnTripName = retTrip?.TripName ?? "",
                ReturnDepartDate = retTrip?.DepartDate ?? "",
                ReturnDepartTime = retTrip?.DepartTime ?? "",
                ReturnFromCity = retTrip?.FromCity ?? "",
                ReturnToCity = retTrip?.ToCity ?? "",

                ReturnBusId = retBusId,
                ReturnBusName = retBusName,
                ReturnLayoutW = retW,
                ReturnLayoutH = retH,

                GridReturn = gridReturn,
                UnavailableReturn = unavailableReturn,

                // add suggestions to VM
                MainDestinationSuggestions = mainSug,
                ReturnDestinationSuggestions = retSug,

                Input = new BookingCreateInputModel
                {
                    TripId = trip.Id,
                    BookingType = bookingTypeInt,
                    ReturnTripId = retTrip?.Id,
                    RequiredMainSeats = safeRequiredMainSeats,
                    RequiredReturnSeats = safeRequiredReturnSeats,

                    // ensure required fields exist in input model
                    DestinationPlaceName = "",
                    ReturnDestinationPlaceName = ""
                },
            };
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// GRID (BUS LAYOUT) //////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static List<SeatCellModel> BuildGrid(int w, int h, IEnumerable<BusSeat> seats)
        {
            // Group by cell
            var byCell = seats
                .GroupBy(s => (s.X, s.Y))
                .ToDictionary(g => g.Key, g => g.ToList());

            var grid = new List<SeatCellModel>(w * h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byCell.TryGetValue((x, y), out var cellItems);
                    cellItems ??= new List<BusSeat>();

                    // pick "base" element to show inside grid (Seat/WC/Aisle/etc) and keep Door as overlay
                    var door = cellItems.FirstOrDefault(i => string.Equals(i.ElementType, "Door", StringComparison.OrdinalIgnoreCase));

                    // Prefer Seat/WC/Aisle as base (anything not Door)
                    var baseItem = cellItems.FirstOrDefault(i => !string.Equals(i.ElementType, "Door", StringComparison.OrdinalIgnoreCase));

                    if (baseItem == null)
                    {
                        // No base element, default to Aisle but still allow door overlay
                        grid.Add(new SeatCellModel
                        {
                            X = x,
                            Y = y,
                            ElementType = "Aisle",
                            SeatCode = null,
                            IsSelectable = false,
                            IsActive = false,
                            Role = null,

                            HasDoor = door != null,
                            DoorSide = door?.DoorSide,
                            DoorOffset = door?.DoorOffset
                        });
                        continue;
                    }

                    var type = (baseItem.ElementType ?? "Seat").Trim();
                    var role = (baseItem.Role ?? "Passenger").Trim();
                    var isActive = baseItem.IsActiveInt == 1;

                    var isSeat = string.Equals(type, "Seat", StringComparison.OrdinalIgnoreCase);

                    var selectable =
                        isSeat
                        && isActive
                        && role.Equals("Passenger", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(baseItem.SeatCode);

                    grid.Add(new SeatCellModel
                    {
                        X = x,
                        Y = y,
                        ElementType = type,
                        SeatCode = baseItem.SeatCode,
                        Role = role,
                        IsActive = isActive,
                        IsSelectable = selectable,

                        HasDoor = door != null,
                        DoorSide = door?.DoorSide,
                        DoorOffset = door?.DoorOffset
                    });
                }
            }

            return grid;
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// BOOKINGS (UNAVAILABLE) /////////////////////////////
        //---------------------------------------------------------------------------------------//

        private async Task<HashSet<string>> GetUnavailableSeatsForTrip(Guid tripId, SeatLeg leg, CancellationToken ct)
        {
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.IsCanceledInt == 0 &&
                            (b.TripId == tripId || (b.ReturnTripId.HasValue && b.ReturnTripId.Value == tripId)))
                .Select(b => new
                {
                    b.TripId,
                    b.ReturnTripId,
                    b.BookingTypeInt,
                    b.SeatsText,
                    b.SeatsReturnText
                })
                .ToListAsync(ct);

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in bookings)
            {
                // BookingType 1 = Go only
                if (b.BookingTypeInt == 1)
                {
                    if (leg == SeatLeg.Go && b.TripId == tripId)
                    {
                        foreach (var s in ParseSeatsCsv(b.SeatsText))
                            set.Add(s);
                    }
                    continue;
                }

                // BookingType 2 = Return only
                if (b.BookingTypeInt == 2)
                {
                    if (leg == SeatLeg.Return && b.TripId == tripId)
                    {
                        foreach (var s in ParseSeatsCsv(b.SeatsText))
                            set.Add(s);
                    }
                    continue;
                }

                // BookingType 3 = Round (Go + Return)
                if (b.BookingTypeInt == 3)
                {
                    // Go leg seats
                    if (leg == SeatLeg.Go && b.TripId == tripId)
                    {
                        foreach (var s in ParseSeatsCsv(b.SeatsText))
                            set.Add(s);
                    }

                    // Return leg seats (stored in SeatsReturnText on ReturnTripId)
                    if (leg == SeatLeg.Return && b.ReturnTripId.HasValue && b.ReturnTripId.Value == tripId)
                    {
                        foreach (var s in ParseSeatsCsv(b.SeatsReturnText))
                            set.Add(s);
                    }
                }
            }

            return set;
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// FAIL (RENDER DETAILS) //////////////////////////////
        //---------------------------------------------------------------------------------------//
        
        private async Task<IActionResult> Fail(Trip trip, TripPriceType mode, Guid? returnTripId, BookingCreateInputModel input, string msg)
        {
            var vm = await BuildDetailsVM(trip, mode, returnTripId, input.RequiredMainSeats, input.RequiredReturnSeats, HttpContext.RequestAborted);
            vm.Input = input;
            vm.ErrorMessage = msg;
            return View(V_Details, vm);
        }
    }
}