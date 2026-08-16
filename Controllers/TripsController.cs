using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using NoufirTours.Models.Trips.Accounts;
using NoufirTours.Models.Trips.Buses;
using NoufirTours.Models.Trips.Drivers;
using NoufirTours.Models.Trips.Trips;
using NoufirTours.Services;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace NoufirTours.Controllers
{
    [RequireUserId]
    [Authorize(Roles = "admin")]
    [Authorize]
    public class TripsController : Controller
    {

        private readonly DBContext _db;
        private readonly ILocationService _locationService;
        private readonly DataHasher _passwordHasher;
        private readonly IDailyWork _dailyWork;
        private const int DefaultLicenseValidityYears = 3;
        // Keep it centralized (your folder style)

        private const string V_TripssList = "Views/Trips/Trips/Trips.cshtml";
        private const string V_TripSettingAuto = "Views/Trips/Trips/AutoTripsSettings.cshtml";
        private const string V_TripSettingAutoInner = "Views/Trips/Trips/Partials/_AutoTripsSettingsInner.cshtml";
        private const string V_TripSettingAutoTemplateModal = "Views/Trips/Trips/Partials/_AutoTripTemplateModalBody.cshtml";
        private const string V_TripDetails = "Views/Trips/Trips/TripDetails.cshtml";
        private const string V_CreateNewTrip = "Views/Trips/Trips/CreateNewTrip.cshtml";
        private const string V_EditTrip = "Views/Trips/Trips/EditTrip.cshtml";
        private const string V_BusDetailsModalBody = "Views/Trips/Trips/Partials/_BusDetailsModalBody.cshtml";
        private const string V_DriverDetailsModalBody = "Views/Trips/Trips/Partials/_DriverDetailsModalBody.cshtml";

        private const string V_BusesList = "Views/Trips/Buses/Buses.cshtml";
        private const string V_BusDetails = "Views/Trips/Buses/BusDetails.cshtml";
        private const string V_CreateNewBus = "Views/Trips/Buses/CreateNewBus.cshtml";
        private const string V_EditBus = "Views/Trips/Buses/EditBus.cshtml";

        private const string V_DriversList = "Views/Trips/Drivers/Drivers.cshtml";
        private const string V_DriverDetails = "Views/Trips/Drivers/DriverDetails.cshtml";
        private const string V_CreateNewDriver = "Views/Trips/Drivers/CreateNewDriver.cshtml";
        private const string V_EditDriver = "Views/Trips/Drivers/EditDriver.cshtml";

        private const string V_AdminUsersList = "Views/Trips/Accounts/Accounts.cshtml";
        private const string V_AdminUserCreate = "Views/Trips/Accounts/UserCreate.cshtml";
        private const string V_AdminUserDetails = "Views/Trips/Accounts/UserDetails.cshtml";
        private const string V_UserFinanceModal = "Views/Trips/Accounts/Partials/_UserFinanceModal.cshtml";
        private const string V_UserPasswordModal = "Views/Trips/Accounts/Partials/_UserPasswordModal.cshtml";

        private const string V_SupportTech = "Views/Trips/Supports/TechnicalSupport.cshtml";

        public TripsController(DBContext db, ILocationService locationService, DataHasher passwordHasher, IDailyWork dailyWork)
        {
            _db = db;
            _locationService = locationService;
            _passwordHasher = passwordHasher;
            _dailyWork = dailyWork;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ReverseGeocode(decimal lat, decimal lng, CancellationToken ct)
        {
            var address = await _locationService.ReverseGeocodeAsync(lat, lng, ct);
            return Json(new { address });
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////////// Time ZONE ////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static readonly TimeZoneInfo CairoTz = GetCairoTimeZone();

        private static TimeZoneInfo GetCairoTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        }

        private static DateTime CairoNow() =>
            TimeZoneInfo.ConvertTime(DateTime.UtcNow, CairoTz);

        private static bool TryParseTripDepartAt(string? departDate, string? departTime, out DateTime departAtCairoLocal)
        {
            departAtCairoLocal = default;

            if (string.IsNullOrWhiteSpace(departDate) || string.IsNullOrWhiteSpace(departTime))
                return false;

            // DB format: yyyy-MM-dd + HH:mm
            if (!DateTime.TryParseExact(
                    $"{departDate} {departTime}",
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedLocal))
                return false;

            departAtCairoLocal = DateTime.SpecifyKind(parsedLocal, DateTimeKind.Unspecified);
            return true;
        }

        private static long UtcUnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Unix seconds -> Cairo DateTime
        private static DateTime? UnixToCairoDateTime(long? unix)
        {
            if (!unix.HasValue) return null;
            var utc = DateTimeOffset.FromUnixTimeSeconds(unix.Value).UtcDateTime;
            return TimeZoneInfo.ConvertTimeFromUtc(utc, CairoTz);
        }

        // Unix seconds -> Cairo Date (DateTime.Date)
        private static DateTime? UnixToCairoDate(long? unix)
        {
            var dt = UnixToCairoDateTime(unix);
            return dt?.Date;
        }

        private static DateTime UnixToCairo(long unix)
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            return TimeZoneInfo.ConvertTimeFromUtc(utc, CairoTz);
        }

        private static long ToUnixStartCairo(DateTime dateLocal)
        {
            var unspecified = DateTime.SpecifyKind(dateLocal.Date, DateTimeKind.Unspecified);
            var offset = new DateTimeOffset(unspecified, CairoTz.GetUtcOffset(unspecified));
            return offset.ToUnixTimeSeconds();
        }

        private static long ToUnixEndCairo(DateTime dateLocal)
        {
            var end = dateLocal.Date.AddDays(1).AddSeconds(-1);
            var unspecified = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
            var offset = new DateTimeOffset(unspecified, CairoTz.GetUtcOffset(unspecified));
            return offset.ToUnixTimeSeconds();
        }

        //--------------------------------------------------------------------------------------//
        ////////////////////////////////////// AUDIT LOG ////////////////////////////////////////
        //--------------------------------------------------------------------------------------//

        private Guid GetCurrentUserId()
        {
            var idStr = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(idStr))
                idStr = HttpContext?.Session?.GetString("UserID");
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        private static string? AuditJson(object? obj, int maxLen = 4000)
        {
            if (obj == null) return null;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return json.Length <= maxLen ? json : json.Substring(0, maxLen);
            }
            catch
            {
                return obj.ToString();
            }
        }

        private async Task WriteAuditAsync(string action, string entity, string? entityId = null, string? details = null, CancellationToken ct = default)
        {
            var uid = GetCurrentUserId();
            if (uid == Guid.Empty) return;

            try
            {
                await _db.Set<NoufirTours.Data.AuditLog>().AddAsync(new NoufirTours.Data.AuditLog
                {
                    UserId = uid,
                    Action = (action ?? "").Trim(),
                    Entity = (entity ?? "").Trim(),
                    EntityId = entityId,
                    Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
                    CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }, ct);

            }
            catch
            {
            }
        }

        private Task AuditAsync(string action, string entity, Guid? entityId = null, object? detailsObj = null)
        {
            var details = detailsObj == null ? null : AuditJson(detailsObj);
            return WriteAuditAsync(action, entity, entityId.ToString(), details);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////////// TRIPS //////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Trips(string? q, bool includeArchived = true, string? filterType = null, string? filterValue = null)
        {
            ViewBag.Q = q ?? "";
            ViewBag.IncludeArchived = includeArchived;
            ViewBag.FilterType = filterType ?? "";
            ViewBag.FilterValue = filterValue ?? "";

            var query = _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus)
                .Include(t => t.Driver)
                .AsQueryable();

            // Filter by archived
            if (!includeArchived)
                query = query.Where(t => t.IsArchivedInt == 0);

            // Apply status filters
            if (!string.IsNullOrWhiteSpace(filterType) && !string.IsNullOrWhiteSpace(filterValue))
            {
                switch (filterType.ToLower())
                {
                    case "status":
                        switch (filterValue.ToLower())
                        {
                            case "active":
                                query = query.Where(t => t.IsActiveInt == 1 && t.IsArchivedInt == 0);
                                break;
                            case "inactive":
                                query = query.Where(t => t.IsActiveInt == 0 && t.IsArchivedInt == 0);
                                break;
                            case "archived":
                                query = query.Where(t => t.IsArchivedInt == 1);
                                break;
                        }
                        break;

                    case "triptype":
                        switch (filterValue.ToLower())
                        {
                            case "go":
                                query = query.Where(t => t.PriceTypeInt == (int)TripPriceType.Go);
                                break;
                            case "return":
                                query = query.Where(t => t.PriceTypeInt == (int)TripPriceType.Return);
                                break;
                            case "round":
                                query = query.Where(t => t.PriceTypeInt == (int)TripPriceType.Round);
                                break;
                        }
                        break;
                }
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim();

                query = query.Where(t =>
                    (t.TripName != null && t.TripName.Contains(s)) ||
                    (t.DepartDate != null && t.DepartDate.Contains(s)) ||
                    (t.DepartTime != null && t.DepartTime.Contains(s)) ||
                    (t.FromCity != null && t.FromCity.Contains(s)) ||
                    (t.ToCity != null && t.ToCity.Contains(s)) ||
                    (t.PickupPlace != null && t.PickupPlace.Contains(s)) ||
                    (t.DropoffPlace != null && t.DropoffPlace.Contains(s)) ||
                    (t.Notes != null && t.Notes.Contains(s)) ||
                    (t.DriverName != null && t.DriverName.Contains(s)) ||
                    (t.DriverPhone != null && t.DriverPhone.Contains(s)) ||
                    (t.Bus != null && t.Bus.BusNumber.Contains(s)) ||
                    (t.Driver != null && t.Driver.FullName.Contains(s))
                );
            }

            var list = await query
                .OrderByDescending(t => t.DepartDate)
                .ThenByDescending(t => t.DepartTime)
                .Select(t => new TripListItemModel
                {
                    Id = t.Id,
                    TripName = t.TripName,
                    DepartDate = t.DepartDate,
                    DepartTime = t.DepartTime,
                    FromCity = t.FromCity,
                    ToCity = t.ToCity,
                    PickupPlace = t.PickupPlace,
                    DropoffPlace = t.DropoffPlace,

                    // Price Type
                    PriceType = (TripPriceType)t.PriceTypeInt,

                    // Prices
                    SeatPriceGo = t.SeatPriceGo,
                    SeatPriceReturn = t.SeatPriceReturn,

                    BusId = t.BusId,
                    BusNumber = t.Bus != null ? t.Bus.BusNumber : null,
                    DriverId = t.DriverId,
                    DriverFullName = t.Driver != null ? t.Driver.FullName : null,
                    DriverName = t.DriverName,
                    DriverPhone = t.DriverPhone,
                    IsArchived = (t.IsArchivedInt == 1),
                    IsActive = (t.IsActiveInt == 1)
                })
                .ToListAsync();

            return View(V_TripssList, list);
        }

        [HttpGet]
        public async Task<IActionResult> AutoTripsSettings(string? checkDate)
        {
            var vm = await BuildPlannerVM(checkDate);
            return View(V_TripSettingAuto, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearTripsNotBooking(CancellationToken ct)
        {
            await _dailyWork.CleanupFutureUnbookedTripsAsync(CairoNow().Date.ToString("yyyy-MM-dd"), ct);
            return RedirectToAction(nameof(Trips));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoTripsSettings(AutoTripsPlannerModel vm)
        {
            var plan = await _db.AutoTripPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == vm.PlanId);

            if (plan == null)
                plan = await EnsureDefaultPlan();

            vm.Name = (vm.Name ?? "Default Plan").Trim();
            vm.Notes = vm.Notes?.Trim();
            vm.SpecificDate = string.IsNullOrWhiteSpace(vm.SpecificDate) ? null : vm.SpecificDate.Trim();

            if (vm.ScheduleType == (int)AutoPlanScheduleType.SpecificDate)
            {
                if (string.IsNullOrWhiteSpace(vm.SpecificDate))
                    ModelState.AddModelError(nameof(vm.SpecificDate), "Please select a specific date.");
            }

            if (!ModelState.IsValid)
            {
                var rebuilt = await BuildPlannerVM(vm.CheckDate);

                rebuilt.Name = vm.Name;
                rebuilt.Notes = vm.Notes;
                rebuilt.IsEnabled = vm.IsEnabled;
                rebuilt.ScheduleType = vm.ScheduleType;
                rebuilt.SpecificDate = vm.SpecificDate;
                rebuilt.ActivationMode = vm.ActivationMode;

                return View(V_TripSettingAuto, rebuilt);
            }

            // Save Plan header
            plan.Name = vm.Name;
            plan.Notes = vm.Notes;
            plan.IsEnabledInt = vm.IsEnabled ? 1 : 0;
            plan.isDone = false;
            plan.ScheduleTypeInt = vm.ScheduleType;
            plan.ActivationModeInt = vm.ActivationMode;

            plan.SpecificDate = (vm.ScheduleType == (int)AutoPlanScheduleType.Daily)
                ? null
                : vm.SpecificDate;

            plan.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Update items from Templates
            if (vm.Templates != null && vm.Templates.Count > 0)
            {
                foreach (var row in vm.Templates)
                {
                    var item = plan.Items.FirstOrDefault(x => x.Id == row.Id && row.Id != Guid.Empty)
                               ?? plan.Items.FirstOrDefault(x => x.OrderNo == row.OrderNo)
                               ?? new AutoTripPlanItem { PlanId = plan.Id, OrderNo = row.OrderNo };

                    if (item.Id == Guid.Empty) plan.Items.Add(item);

                    item.OrderNo = row.OrderNo;
                    item.IsEnabledInt = row.IsEnabled ? 1 : 0;
                }
            }

            // VALIDATE Active + NotArchived for any Enabled item
            // Fetch allowed Active IDs once (fast)
            var activeBusIds = await _db.Buses.AsNoTracking()
                .Where(b => b.IsActiveInt == 1 && b.IsArchivedInt == 0)
                .Select(b => b.Id)
                .ToHashSetAsync();

            var activeDriverIds = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsActiveInt == 1 && d.IsArchivedInt == 0)
                .Select(d => d.Id)
                .ToHashSetAsync();

            var warnings = new List<string>();

            foreach (var it in plan.Items)
            {
                if (it.IsEnabledInt != 1) continue;

                // must have bus + driver
                if (!it.BusId.HasValue || !it.DriverId.HasValue)
                {
                    it.IsEnabledInt = 0;
                    warnings.Add($"Template #{it.OrderNo} was disabled: missing Bus/Driver.");
                    continue;
                }

                // bus must be active and not archived
                if (!activeBusIds.Contains(it.BusId.Value))
                {
                    it.IsEnabledInt = 0;
                    warnings.Add($"Template #{it.OrderNo} was disabled: selected Bus is not Active or is Archived.");
                    continue;
                }

                // driver must be active and not archived
                if (!activeDriverIds.Contains(it.DriverId.Value))
                {
                    it.IsEnabledInt = 0;
                    warnings.Add($"Template #{it.OrderNo} was disabled: selected Driver is not Active or is Archived.");
                    continue;
                }
            }

            // save + show warning in UI after redirect
            if (warnings.Count > 0)
                TempData["Warning"] = string.Join(" | ", warnings);

            await AuditAsync("update_setting", "auto_trip_plan", plan.Id, new
            {
                vm.PlanId,
                vm.Name,
                vm.IsEnabled,
                vm.ScheduleType,
                vm.SpecificDate,
                vm.ActivationMode,
                warnings = (TempData["Warning"]?.ToString())
            });
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(AutoTripsSettings), new { checkDate = vm.CheckDate });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> AutoTripsSettingsPartial(string? checkDate)
        {
            var vm = await BuildPlannerVM(checkDate);
            return PartialView(V_TripSettingAutoInner, vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> AutoTripTemplateModal(Guid planId, int orderNo, string? checkDate)
        {
            var vm = await BuildModalVM(planId, orderNo, checkDate);
            return PartialView(V_TripSettingAutoTemplateModal, vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> AutoTripTemplateData(Guid planId, int orderNo, string? checkDate, CancellationToken ct)
        {
            var plan = await _db.Set<AutoTripPlan>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == planId, ct);

            if (plan == null)
                return NotFound(new { message = "Plan not found" });

            var item = await _db.Set<AutoTripPlanItem>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PlanId == planId && x.OrderNo == orderNo, ct);

            // Defaults
            const string DEFAULT_DEPART_TIME = "05:00";
            const string DEFAULT_FROM = "Asyut";
            const string DEFAULT_TO = "Hurgada";
            const string DEFAULT_PICKUP_TEXT = "First El-Helaly at Go Bus Company";
            const decimal DEFAULT_PICKUP_LAT = 27.180824m;
            const decimal DEFAULT_PICKUP_LON = 31.189725m;
            const decimal DEFAULT_PRICE_GO = 250m;
            const decimal DEFAULT_PRICE_RETURN = 250m;
            const decimal DEFAULT_PRICE_ROUND = 400m;

            static string OrDefault(string? s, string def) => string.IsNullOrWhiteSpace(s) ? def : s.Trim();
            static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            static decimal OrDefaultIfZero(decimal v, decimal def) => v == 0m ? def : v;

            Guid currentBusId = item?.BusId ?? Guid.Empty;
            Guid currentDriverId = item?.DriverId ?? Guid.Empty;

            // Build template JSON with PriceType
            object template;
            if (item == null)
            {
                template = new
                {
                    id = Guid.Empty,
                    orderNo,
                    isEnabled = true,
                    autoEveryDay = (plan.ScheduleTypeInt == (int)AutoPlanScheduleType.Daily),

                    tripName = $"Trip-{orderNo}",
                    departTime = DEFAULT_DEPART_TIME,

                    // Price Type
                    priceType = (int)TripPriceType.Round,

                    fromCity = DEFAULT_FROM,
                    toCity = DEFAULT_TO,

                    pickupPlace = DEFAULT_PICKUP_TEXT,
                    pickupLat = DEFAULT_PICKUP_LAT,
                    pickupLon = DEFAULT_PICKUP_LON,

                    dropoffPlace = (string?)null,
                    notes = (string?)null,

                    seatPriceGo = DEFAULT_PRICE_GO,
                    seatPriceReturn = DEFAULT_PRICE_RETURN,
                    seatPriceRound = DEFAULT_PRICE_ROUND,

                    busId = Guid.Empty,
                    driverId = Guid.Empty
                };
            }
            else
            {
                template = new
                {
                    id = item.Id,
                    orderNo = item.OrderNo,

                    isEnabled = (item.IsEnabledInt == 1),
                    autoEveryDay = (plan.ScheduleTypeInt == (int)AutoPlanScheduleType.Daily),

                    tripName = OrDefault(item.TripName, $"Trip-{orderNo}"),
                    departTime = OrDefault(item.DepartTime, DEFAULT_DEPART_TIME),

                    // Price Type
                    priceType = item.PriceTypeInt,

                    fromCity = OrDefault(item.FromCity, DEFAULT_FROM),
                    toCity = OrDefault(item.ToCity, DEFAULT_TO),

                    pickupPlace = OrDefault(item.PickupPlace, DEFAULT_PICKUP_TEXT),
                    pickupLat = OrDefaultIfZero(item.PickupLat, DEFAULT_PICKUP_LAT),
                    pickupLon = OrDefaultIfZero(item.PickupLon, DEFAULT_PICKUP_LON),

                    dropoffPlace = TrimOrNull(item.DropoffPlace),
                    notes = TrimOrNull(item.Notes),

                    seatPriceGo = (item.SeatPriceGo <= 0 ? DEFAULT_PRICE_GO : item.SeatPriceGo),
                    seatPriceReturn = (item.SeatPriceReturn <= 0 ? DEFAULT_PRICE_RETURN : item.SeatPriceReturn),

                    busId = item.BusId,
                    driverId = item.DriverId
                };
            }

            // Used in same plan
            var usedBusIdsInPlan = await _db.Set<AutoTripPlanItem>()
                .AsNoTracking()
                .Where(x => x.PlanId == planId && x.IsEnabledInt == 1 && x.OrderNo != orderNo)
                .Where(x => x.BusId.HasValue)
                .Select(x => x.BusId!.Value)
                .Distinct()
                .ToListAsync(ct);

            var usedDriverIdsInPlan = await _db.Set<AutoTripPlanItem>()
                .AsNoTracking()
                .Where(x => x.PlanId == planId && x.IsEnabledInt == 1 && x.OrderNo != orderNo)
                .Where(x => x.DriverId.HasValue)
                .Select(x => x.DriverId!.Value)
                .Distinct()
                .ToListAsync(ct);

            // Used in actual trips on selected date
            var dateIso = (checkDate ?? "").Trim();
            List<Guid> usedBusIdsInTrips = new();
            List<Guid> usedDriverIdsInTrips = new();

            if (!string.IsNullOrWhiteSpace(dateIso))
            {
                usedBusIdsInTrips = await _db.Trips
                    .AsNoTracking()
                    .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso)
                    .Where(t => t.BusId.HasValue)
                    .Select(t => t.BusId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                usedDriverIdsInTrips = await _db.Trips
                    .AsNoTracking()
                    .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso)
                    .Where(t => t.DriverId.HasValue)
                    .Select(t => t.DriverId!.Value)
                    .Distinct()
                    .ToListAsync(ct);
            }

            var usedBus = new HashSet<Guid>(usedBusIdsInPlan);
            foreach (var x in usedBusIdsInTrips) usedBus.Add(x);

            var usedDriver = new HashSet<Guid>(usedDriverIdsInPlan);
            foreach (var x in usedDriverIdsInTrips) usedDriver.Add(x);

            // Build buses/drivers lists
            var buses = await _db.Set<Bus>()
                .AsNoTracking()
                .Where(b => b.IsActiveInt == 1 && b.IsArchivedInt == 0)
                .OrderBy(b => b.BusNumber)
                .Select(b => new
                {
                    id = b.Id,
                    busNumber = b.BusNumber
                })
                .ToListAsync(ct);

            var drivers = await _db.Set<Driver>()
                .AsNoTracking()
                .Where(d => d.IsActiveInt == 1 && d.IsArchivedInt == 0)
                .OrderBy(d => d.FullName)
                .Select(d => new
                {
                    id = d.Id,
                    fullName = d.FullName
                })
                .ToListAsync(ct);

            var busesOut = buses
                .Where(b => !usedBus.Contains(b.id) || b.id == currentBusId)
                .Select(b => new
                {
                    value = b.id.ToString(),
                    text = b.busNumber,
                    disabled = false
                })
                .ToList();

            var driversOut = drivers
                .Where(d => !usedDriver.Contains(d.id) || d.id == currentDriverId)
                .Select(d => new
                {
                    value = d.id.ToString(),
                    text = d.fullName,
                    disabled = false
                })
                .ToList();

            return Json(new
            {
                plan = new
                {
                    id = plan.Id,
                    name = plan.Name,
                    scheduleTypeInt = plan.ScheduleTypeInt,
                    specificDate = plan.SpecificDate,
                    activationModeInt = plan.ActivationModeInt,
                    isEnabled = (plan.IsEnabledInt == 1)
                },
                template,
                buses = busesOut,
                drivers = driversOut,

                meta = new
                {
                    checkDate = dateIso,
                    usedBusInPlan = usedBusIdsInPlan.Select(x => x.ToString()).ToList(),
                    usedDriverInPlan = usedDriverIdsInPlan.Select(x => x.ToString()).ToList(),
                    usedBusInTrips = usedBusIdsInTrips.Select(x => x.ToString()).ToList(),
                    usedDriverInTrips = usedDriverIdsInTrips.Select(x => x.ToString()).ToList()
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveTemplateOrder(Guid planId, int orderNo, string direction, string? checkDate)
        {
            direction = (direction ?? "").Trim().ToLowerInvariant();
            checkDate = string.IsNullOrWhiteSpace(checkDate) ? null : checkDate.Trim();

            var plan = await _db.AutoTripPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
                plan = await EnsureDefaultPlan();

            var current = plan.Items.FirstOrDefault(x => x.OrderNo == orderNo);
            if (current == null)
            {
                var planner0 = await BuildPlannerVM(checkDate);
                return PartialView(V_TripSettingAutoInner, planner0);
            }

            int targetOrder = direction == "up" ? (orderNo - 1) : (orderNo + 1);
            if (targetOrder < 1)
            {
                var planner1 = await BuildPlannerVM(checkDate);
                return PartialView(V_TripSettingAutoInner, planner1);
            }

            var target = plan.Items.FirstOrDefault(x => x.OrderNo == targetOrder);
            if (target == null)
            {
                var planner2 = await BuildPlannerVM(checkDate);
                return PartialView(V_TripSettingAutoInner, planner2);
            }

            // pick a temporary order that's guaranteed free for THIS plan
            int tempOrder = (plan.Items.Count == 0) ? 1 : (plan.Items.Max(x => x.OrderNo) + 1);

            using var tx = await _db.Database.BeginTransactionAsync();

            // move current to temp
            current.OrderNo = tempOrder;
            await _db.SaveChangesAsync();

            // move target into current's old place
            target.OrderNo = orderNo;
            await _db.SaveChangesAsync();

            // move current into target place
            current.OrderNo = targetOrder;

            plan.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            var planner = await BuildPlannerVM(checkDate);
            return PartialView(V_TripSettingAutoInner, planner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAutoTripTemplateModal(AutoTripTemplateModalModel vm)
        {
            NormalizeModal(vm);

            if (string.IsNullOrWhiteSpace(vm.TripName))
                ModelState.AddModelError(nameof(vm.TripName), "Trip name is required.");

            if (string.IsNullOrWhiteSpace(vm.DepartTime))
                ModelState.AddModelError(nameof(vm.DepartTime), "Depart time is required.");

            if (!string.IsNullOrWhiteSpace(vm.DepartTime))
            {
                if (!DateTime.TryParseExact(vm.DepartTime, "HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    ModelState.AddModelError(nameof(vm.DepartTime), "Time must be HH:mm.");
            }

            string? warning = null;
            List<string> warningDetails = new();

            // Price validation and auto-disable logic
            bool hasPriceError = false;
            List<string> priceErrors = new();
            List<string> priceFields = new();

            switch (vm.PriceType)
            {
                case TripPriceType.Go:
                    if (!vm.SeatPriceGo.HasValue || vm.SeatPriceGo <= 0)
                    {
                        priceErrors.Add("Go price is required");
                        priceFields.Add("Seat Price Go");
                        hasPriceError = true;
                    }
                    else
                    {
                        // Clear unused prices
                        vm.SeatPriceReturn = null;
                        vm.SeatPriceRound = null;
                    }
                    break;

                case TripPriceType.Return:
                    if (!vm.SeatPriceReturn.HasValue || vm.SeatPriceReturn <= 0)
                    {
                        priceErrors.Add("Return price is required");
                        priceFields.Add("Seat Price Return");
                        hasPriceError = true;
                    }
                    else
                    {
                        // Clear unused prices
                        vm.SeatPriceGo = null;
                        vm.SeatPriceRound = null;
                    }
                    break;

                case TripPriceType.Round:
                    if (!vm.SeatPriceGo.HasValue || vm.SeatPriceGo <= 0)
                    {
                        priceErrors.Add("Go price is required");
                        priceFields.Add("Seat Price Go");
                    }

                    if (!vm.SeatPriceReturn.HasValue || vm.SeatPriceReturn <= 0)
                    {
                        priceErrors.Add("Return price is required");
                        priceFields.Add("Seat Price Return");
                    }

                    if (priceErrors.Count > 0)
                        hasPriceError = true;
                    break;
            }

            // Auto-disable if price validation fails
            if (hasPriceError && vm.IsEnabled)
            {
                vm.IsEnabled = false;
                warning = $"Template was auto-disabled because: {string.Join(", ", priceErrors)}.";
                warningDetails.AddRange(priceFields.Select(f => $"Missing: {f}"));
            }

            // Add model errors for UI feedback
            foreach (var error in priceErrors)
            {
                if (error.Contains("Go price"))
                    ModelState.AddModelError(nameof(vm.SeatPriceGo), error);
                else if (error.Contains("Return price"))
                    ModelState.AddModelError(nameof(vm.SeatPriceReturn), error);
                else if (error.Contains("Round trip price"))
                    ModelState.AddModelError(nameof(vm.SeatPriceRound), error);
            }

            // Check for missing Bus/Driver/Coordinates
            List<string> missingParts = new();
            if (vm.IsEnabled)
            {
                var missingBus = !vm.BusId.HasValue;
                var missingDriver = !vm.DriverId.HasValue;
                var missingCoords = (vm.PickupLat == null || vm.PickupLon == null);

                if (missingBus) missingParts.Add("Bus");
                if (missingDriver) missingParts.Add("Driver");
                if (missingCoords) missingParts.Add("Pickup Location");

                if (missingBus || missingDriver || missingCoords)
                {
                    vm.IsEnabled = false;
                    warning = $"Template was auto-disabled because it is missing: {string.Join(", ", missingParts)}.";
                    warningDetails.AddRange(missingParts.Select(p => $"Missing: {p}"));

                    // Add specific model errors for each missing field
                    if (missingBus)
                        ModelState.AddModelError(nameof(vm.BusId), "Bus selection is required when template is enabled");

                    if (missingDriver)
                        ModelState.AddModelError(nameof(vm.DriverId), "Driver selection is required when template is enabled");

                    if (missingCoords)
                        ModelState.AddModelError(nameof(vm.PickupLat), "Pickup location coordinates are required when template is enabled");
                }
            }

            // Store warning details in TempData or ViewBag for display
            if (warningDetails.Count > 0)
            {
                ViewBag.WarningDetails = warningDetails;
            }

            if (!ModelState.IsValid)
            {
                var rebuilt0 = await BuildModalVM(vm.PlanId, vm.OrderNo, vm.CheckDate, vm);
                Response.StatusCode = 400;
                if (!string.IsNullOrWhiteSpace(warning))
                    Response.Headers["X-AutoTrip-Warning"] = warning;

                return PartialView(V_TripSettingAutoTemplateModal, rebuilt0);
            }

            var plan = await _db.AutoTripPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == vm.PlanId);

            if (plan == null)
                plan = await EnsureDefaultPlan();

            // Find existing entity safely
            AutoTripPlanItem? entity = null;

            if (vm.ItemId.HasValue && vm.ItemId.Value != Guid.Empty)
                entity = plan.Items.FirstOrDefault(x => x.Id == vm.ItemId.Value);

            entity ??= plan.Items.FirstOrDefault(x => x.OrderNo == vm.OrderNo);

            var currentId = entity?.Id ?? (vm.ItemId ?? Guid.Empty);

            // Only check conflicts if template is enabled
            if (vm.IsEnabled)
            {
                var otherEnabled = plan.Items
                    .Where(x => x.Id != currentId)
                    .Where(x => x.IsEnabledInt == 1)
                    .ToList();

                if (vm.BusId.HasValue)
                {
                    var busConflict = otherEnabled.FirstOrDefault(x => x.BusId == vm.BusId);
                    if (busConflict != null)
                    {
                        ModelState.AddModelError(nameof(vm.BusId),
                            $"This bus is already used in another enabled template (Slot #{busConflict.TripName}).");
                    }
                }

                if (vm.DriverId.HasValue)
                {
                    var driverConflict = otherEnabled.FirstOrDefault(x => x.DriverId == vm.DriverId);
                    if (driverConflict != null)
                    {
                        ModelState.AddModelError(nameof(vm.DriverId),
                            $"This driver is already used in another enabled template (Slot #{driverConflict.TripName}).");
                    }
                }

                var dateIso = (vm.CheckDate ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(dateIso))
                {
                    var tripsQ = _db.Trips.AsNoTracking()
                        .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso);

                    if (vm.BusId.HasValue)
                    {
                        var usedInTrip = await tripsQ.AnyAsync(t => t.BusId == vm.BusId);
                        if (usedInTrip)
                            ModelState.AddModelError(nameof(vm.BusId),
                                "This bus is already assigned to an existing trip on the selected date.");
                    }

                    if (vm.DriverId.HasValue)
                    {
                        var usedInTrip = await tripsQ.AnyAsync(t => t.DriverId == vm.DriverId);
                        if (usedInTrip)
                            ModelState.AddModelError(nameof(vm.DriverId),
                                "This driver is already assigned to an existing trip on the selected date.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                warning = "Cannot save: Bus/Driver is already used in another template/trip.";
                Response.Headers["X-AutoTrip-Warning"] = warning;

                var rebuilt1 = await BuildModalVM(vm.PlanId, vm.OrderNo, vm.CheckDate, vm);
                Response.StatusCode = 400;

                return PartialView(V_TripSettingAutoTemplateModal, rebuilt1);
            }

            bool isNew = (entity == null);

            if (isNew)
            {
                entity = new AutoTripPlanItem
                {
                    Id = Guid.NewGuid(),
                    PlanId = plan.Id,
                    OrderNo = vm.OrderNo
                };

                plan.Items.Add(entity);
                _db.Entry(entity).State = EntityState.Added;
            }
            else
            {
                _db.Entry(entity).State = EntityState.Modified;
            }

            // Assign values
            entity!.OrderNo = vm.OrderNo;
            entity.IsEnabledInt = vm.IsEnabled ? 1 : 0;

            entity.TripName = vm.TripName.Trim();
            entity.DepartTime = vm.DepartTime.Trim();

            // Price Type
            entity.PriceTypeInt = (int)vm.PriceType;

            entity.FromCity = vm.FromCity;
            entity.ToCity = vm.ToCity;

            entity.PickupPlace = vm.PickupPlace;
            entity.PickupLat = vm.PickupLat ?? 0m;
            entity.PickupLon = vm.PickupLon ?? 0m;

            entity.DropoffPlace = vm.DropoffPlace;
            entity.Notes = vm.Notes;

            entity.SeatPriceGo = vm.SeatPriceGo ?? 0;
            entity.SeatPriceReturn = vm.SeatPriceReturn ?? 0;

            entity.BusId = vm.BusId;
            entity.DriverId = vm.DriverId;

            plan.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                Response.StatusCode = 409;
                return Content("Concurrency: template was modified/deleted by another operation. Refresh and try again.");
            }

            if (!string.IsNullOrWhiteSpace(warning))
                Response.Headers["X-AutoTrip-Warning"] = warning;

            var planner = await BuildPlannerVM(vm.CheckDate);
            return PartialView(V_TripSettingAutoInner, planner);
        }

        [HttpGet]
        public async Task<IActionResult> TripDetails(Guid id)
        {
            var trip = await _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.Seats)
                .Include(t => t.Driver)
                    .ThenInclude(d => d!.Phones)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null) return NotFound();

            var vm = new TripDetailsViewModel
            {
                Id = trip.Id,
                TripName = trip.TripName,
                DepartDate = trip.DepartDate,
                DepartTime = trip.DepartTime,
                FromCity = trip.FromCity,
                ToCity = trip.ToCity,
                PickupPlace = trip.PickupPlace,
                PickupLat = trip.PickupLat,
                PickupLon = trip.PickupLon,
                DropoffPlace = trip.DropoffPlace,
                Notes = trip.Notes,

                // Price Type
                PriceType = (TripPriceType)trip.PriceTypeInt,

                // Prices
                SeatPriceGo = trip.SeatPriceGo,
                SeatPriceReturn = trip.SeatPriceReturn,

                IsArchived = trip.IsArchivedInt == 1,
                IsActive = trip.IsActiveInt == 1,
                BusId = trip.BusId,
                DriverId = trip.DriverId,

                DriverNameOverride = trip.DriverName,
                DriverPhoneOverride = trip.DriverPhone
            };

            if (trip.Bus != null)
            {
                var seatsTotal = trip.Bus.Seats?.Count ?? 0;
                var seatsActive = trip.Bus.Seats?.Count(s => s.IsActiveInt == 1) ?? 0;

                vm.Bus = new BusDetailsTrip
                {
                    Id = trip.Bus.Id,
                    BusNumber = trip.Bus.BusNumber,
                    ChassisNumber = trip.Bus.ChassisNumber,
                    PlateNumber = trip.Bus.PlateNumber,
                    Manufacturer = trip.Bus.Manufacturer,
                    ModelName = trip.Bus.ModelName,
                    ModelYear = trip.Bus.ModelYear,
                    BusType = trip.Bus.BusType,
                    SeatsCount = trip.Bus.SeatsCount,
                    Color = trip.Bus.Color,
                    Specs = trip.Bus.Specs,
                    Notes = trip.Bus.Notes,
                    IsActive = trip.Bus.IsActiveInt == 1,
                    IsArchived = trip.Bus.IsArchivedInt == 1,
                    LayoutWidth = trip.Bus.LayoutWidth,
                    LayoutHeight = trip.Bus.LayoutHeight,
                    SeatsTotal = seatsTotal,
                    SeatsActive = seatsActive
                };
            }

            if (trip.Driver != null)
            {
                vm.Driver = new DriverDetailsTrip
                {
                    Id = trip.Driver.Id,
                    FullName = trip.Driver.FullName,
                    NationalId = trip.Driver.NationalId,
                    Address = trip.Driver.Address,
                    LicenseNumber = trip.Driver.LicenseNumber,
                    LicenseExpiryAtUnix = trip.Driver.LicenseExpiryAtUnix,
                    JoinedAtUnix = trip.Driver.JoinedAtUnix,
                    Notes = trip.Driver.Notes,
                    IsActive = trip.Driver.IsActiveInt == 1,
                    IsArchived = trip.Driver.IsArchivedInt == 1,
                    Phones = trip.Driver.Phones
                        .OrderByDescending(p => p.IsPrimaryInt == 1)
                        .Select(p => new DriverPhoneTrip
                        {
                            PhoneNumber = p.PhoneNumber,
                            IsPrimary = p.IsPrimaryInt == 1
                        })
                        .ToList()
                };
            }

            return View(V_TripDetails, vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> BusDetailsModal(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return PartialView(V_BusDetailsModalBody, new BusModalDetailsModel
                {
                    ErrorMessage = "Please select a bus first."
                });
            }

            var bus = await _db.Buses
                .AsNoTracking()
                .Include(b => b.Seats)
                .FirstOrDefaultAsync(b => b.Id == id.Value);

            if (bus == null)
            {
                return PartialView(V_BusDetailsModalBody, new BusModalDetailsModel
                {
                    ErrorMessage = "Bus not found."
                });
            }

            var vm = new BusModalDetailsModel
            {
                Id = bus.Id,
                BusNumber = bus.BusNumber,
                PlateNumber = bus.PlateNumber,
                ChassisNumber = bus.ChassisNumber,
                Manufacturer = bus.Manufacturer,
                ModelName = bus.ModelName,
                ModelYear = bus.ModelYear,
                BusType = bus.BusType,
                Color = bus.Color,
                LayoutWidth = bus.LayoutWidth,
                LayoutHeight = bus.LayoutHeight,
                SeatsCount = bus.SeatsCount ?? 0,
                SeatsActive = bus.Seats.Count(s => s.IsActiveInt == 1),
                Specs = bus.Specs,
                Notes = bus.Notes,
                IsActive = bus.IsActiveInt == 1,
                IsArchived = bus.IsArchivedInt == 1,
                Seats = bus.Seats
                    .OrderBy(s => s.Y).ThenBy(s => s.X)
                    .Select(s => new BusSeatModalRow
                    {
                        ElementType = s.ElementType,
                        SeatCode = s.SeatCode,
                        X = s.X,
                        Y = s.Y,
                        IsActive = s.IsActiveInt == 1,
                        Role = s.Role,
                        Label = s.Label,
                        DoorSide = s.DoorSide,
                        DoorOffset = s.DoorOffset
                    })
                    .ToList()
            };

            return PartialView(V_BusDetailsModalBody, vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DriverDetailsModal(Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                return PartialView(V_DriverDetailsModalBody, new DriverModalDetailsModel
                {
                    ErrorMessage = "Please select a driver first."
                });
            }

            var d = await _db.Drivers
                .AsNoTracking()
                .Include(x => x.Phones)
                .FirstOrDefaultAsync(x => x.Id == id.Value);

            if (d == null)
            {
                return PartialView(V_DriverDetailsModalBody, new DriverModalDetailsModel
                {
                    ErrorMessage = "Driver not found."
                });
            }

            var vm = new DriverModalDetailsModel
            {
                Id = d.Id,
                FullName = d.FullName,
                NationalId = d.NationalId,
                Address = d.Address,
                LicenseNumber = d.LicenseNumber,
                Notes = d.Notes,
                IsActive = d.IsActiveInt == 1,
                IsArchived = d.IsArchivedInt == 1,

                JoinedAt = UnixToCairoDateTime(d.JoinedAtUnix) ?? CairoNow(),

                LicenseExpiryDate = UnixToCairoDate(d.LicenseExpiryAtUnix),

                ArchivedAt = UnixToCairoDateTime(d.ArchivedAtUnix),

                Phones = (d.Phones ?? new List<DriverPhone>())
                    .OrderByDescending(p => p.IsPrimaryInt)
                    .ThenBy(p => p.Id)
                    .Select(p => new DriverPhoneModal
                    {
                        PhoneNumber = p.PhoneNumber,
                        IsPrimary = p.IsPrimaryInt == 1
                    })
                    .ToList()
            };

            return PartialView(V_DriverDetailsModalBody, vm);
        }

        [HttpGet]
        public async Task<IActionResult> CreateNewTrip(string? departDate)
        {
            // Date default: tomorrow
            var date = string.IsNullOrWhiteSpace(departDate)
                ? DateTime.Today.AddDays(1)
                : DateTime.TryParse(departDate, out var parsed)
                    ? parsed
                    : DateTime.Today.AddDays(1);

            var depDateStr = date.ToString("yyyy-MM-dd");

            // Default Pickup coordinates
            const decimal DEFAULT_PICKUP_LAT = 27.180824m;
            const decimal DEFAULT_PICKUP_LNG = 31.189725m;

            // Default TripName (unique per day)
            var defaultTripName = await GenerateUniqueTripNameForDate(depDateStr);

            var vm = new TripCreateEditModel
            {
                TripName = defaultTripName,

                DepartDate = depDateStr,
                DepartTime = "05:00",

                FromCity = "Asyut",
                ToCity = "Hurgada",

                PickupPlace = "First El-Helaly at Go Bus Company",
                PickupLat = DEFAULT_PICKUP_LAT,
                PickupLon = DEFAULT_PICKUP_LNG,

                DropoffPlace = null,
                Notes = null,

                // Price Type - Round as default
                PriceType = TripPriceType.Round,

                // Default prices
                SeatPriceGo = 250,
                SeatPriceReturn = 250,

                BusId = null,
                DriverId = null,

                DriverName = null,
                DriverPhone = null,
                DriverUserId = null
            };

            await FillTripLookups(vm);

            // Pick first available bus/driver if not set
            var tripsOnDate = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr);

            var usedBusIds = await tripsOnDate
                .Where(t => t.BusId != null)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsOnDate
                .Where(t => t.DriverId != null)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            var firstBusId = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1 && !usedBusIds.Contains(b.Id))
                .OrderBy(b => b.BusNumber)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();

            var firstDriverId = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1 && !usedDriverIds.Contains(d.Id))
                .OrderBy(d => d.FullName)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            vm.BusId ??= firstBusId != Guid.Empty ? firstBusId : null;
            vm.DriverId ??= firstDriverId != Guid.Empty ? firstDriverId : null;

            return View(V_CreateNewTrip, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNewTrip(TripCreateEditModel vm)
        {
            NormalizeTripVM(vm);

            // Defaults
            vm.DepartDate = string.IsNullOrWhiteSpace(vm.DepartDate)
                ? DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")
                : vm.DepartDate.Trim();

            vm.DepartTime = string.IsNullOrWhiteSpace(vm.DepartTime) ? "05:00" : vm.DepartTime.Trim();

            vm.FromCity = string.IsNullOrWhiteSpace(vm.FromCity) ? "Asyut" : vm.FromCity.Trim();
            vm.ToCity = string.IsNullOrWhiteSpace(vm.ToCity) ? "Hurgada" : vm.ToCity.Trim();
            vm.PickupPlace = string.IsNullOrWhiteSpace(vm.PickupPlace)
                ? "First El-Helaly at Go Bus Company"
                : vm.PickupPlace.Trim();

            // Price validation based on type
            switch (vm.PriceType)
            {
                case TripPriceType.Go:
                    if (!vm.SeatPriceGo.HasValue || vm.SeatPriceGo <= 0)
                        ModelState.AddModelError(nameof(vm.SeatPriceGo), "Go price is required and must be greater than 0");

                    // Clear unused prices
                    vm.SeatPriceReturn = null;
                    break;

                case TripPriceType.Return:
                    if (!vm.SeatPriceReturn.HasValue || vm.SeatPriceReturn <= 0)
                        ModelState.AddModelError(nameof(vm.SeatPriceReturn), "Return price is required and must be greater than 0");

                    // Clear unused prices
                    vm.SeatPriceGo = null;
                    break;

                case TripPriceType.Round:
                    if (!vm.SeatPriceGo.HasValue || vm.SeatPriceGo <= 0)
                        ModelState.AddModelError(nameof(vm.SeatPriceGo), "Go price is required for round trip");

                    if (!vm.SeatPriceReturn.HasValue || vm.SeatPriceReturn <= 0)
                        ModelState.AddModelError(nameof(vm.SeatPriceReturn), "Return price is required for round trip");
                    break;
            }

            // Build departAt
            if (!TryBuildDepartAt(vm.DepartDate, vm.DepartTime, out var departAt))
                ModelState.AddModelError(nameof(vm.DepartDate), "Invalid departure date/time.");

            // TripName required
            if (string.IsNullOrWhiteSpace(vm.TripName))
                ModelState.AddModelError(nameof(vm.TripName), "Trip name is required.");
            else
                vm.TripName = vm.TripName.Trim();

            // Require pickup coords
            if (vm.PickupLat == null || vm.PickupLon == null)
                ModelState.AddModelError(nameof(vm.PickupLat), "Pickup location (Lat/Lon) is required.");

            // Your existing rules
            ValidateTripBusinessRules(ModelState, vm, departAt);

            var depDateStr = vm.DepartDate!;

            // Auto pick bus/driver
            var tripsOnDate = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr);

            var usedBusIds = await tripsOnDate
                .Where(t => t.BusId != null)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsOnDate
                .Where(t => t.DriverId != null)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            if (!vm.BusId.HasValue)
            {
                vm.BusId = await _db.Buses.AsNoTracking()
                    .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1 && !usedBusIds.Contains(b.Id))
                    .OrderBy(b => b.BusNumber)
                    .Select(b => b.Id)
                    .FirstOrDefaultAsync();
            }

            if (!vm.DriverId.HasValue)
            {
                vm.DriverId = await _db.Drivers.AsNoTracking()
                    .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1 && !usedDriverIds.Contains(d.Id))
                    .OrderBy(d => d.FullName)
                    .Select(d => d.Id)
                    .FirstOrDefaultAsync();
            }

            // Must exist and be Active + NotArchived
            if (!vm.BusId.HasValue || vm.BusId == Guid.Empty)
                ModelState.AddModelError(nameof(vm.BusId), "No available buses for the selected date.");
            else
            {
                var busOk = await _db.Buses.AsNoTracking()
                    .AnyAsync(b => b.Id == vm.BusId.Value && b.IsArchivedInt == 0 && b.IsActiveInt == 1);

                if (!busOk)
                    ModelState.AddModelError(nameof(vm.BusId), "Selected bus is not Active or is archived.");
            }

            if (!vm.DriverId.HasValue || vm.DriverId == Guid.Empty)
                ModelState.AddModelError(nameof(vm.DriverId), "No available drivers for the selected date.");
            else
            {
                var driverOk = await _db.Drivers.AsNoTracking()
                    .AnyAsync(d => d.Id == vm.DriverId.Value && d.IsArchivedInt == 0 && d.IsActiveInt == 1);

                if (!driverOk)
                    ModelState.AddModelError(nameof(vm.DriverId), "Selected driver is not Active or is archived.");
            }

            // Conflicts (bus/driver not duplicated same day)
            if (vm.BusId.HasValue && vm.BusId != Guid.Empty)
            {
                var busBusy = await _db.Trips.AsNoTracking()
                    .AnyAsync(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr && t.BusId == vm.BusId.Value);

                if (busBusy)
                    ModelState.AddModelError(nameof(vm.BusId), "This bus is already assigned to another trip on the selected date.");
            }

            if (vm.DriverId.HasValue && vm.DriverId != Guid.Empty)
            {
                var driverBusy = await _db.Trips.AsNoTracking()
                    .AnyAsync(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr && t.DriverId == vm.DriverId.Value);

                if (driverBusy)
                    ModelState.AddModelError(nameof(vm.DriverId), "This driver is already assigned to another trip on the selected date.");
            }

            // TripName unique per day (server-side)
            if (!string.IsNullOrWhiteSpace(vm.TripName))
            {
                var nameExists = await _db.Trips.AsNoTracking().AnyAsync(t =>
                    t.IsArchivedInt == 0 &&
                    t.DepartDate == depDateStr &&
                    t.TripName == vm.TripName);

                if (nameExists)
                {
                    // suggest a new one (or just error)
                    vm.TripName = await GenerateUniqueTripNameForDate(depDateStr);
                    ModelState.AddModelError(nameof(vm.TripName), $"Trip name already used for this date. Suggested: {vm.TripName}");
                }
            }

            if (!ModelState.IsValid)
            {
                await FillTripLookups(vm);
                return View(V_CreateNewTrip, vm);
            }

            // Save
            var trip = new Trip
            {
                TripName = vm.TripName!.Trim(),

                DepartDate = vm.DepartDate!,
                DepartTime = vm.DepartTime!,
                FromCity = vm.FromCity,
                ToCity = vm.ToCity,

                PickupPlace = vm.PickupPlace,
                PickupLat = vm.PickupLat,
                PickupLon = vm.PickupLon,

                DropoffPlace = vm.DropoffPlace,
                Notes = vm.Notes,

                // Prices based on type
                SeatPriceGo = vm.SeatPriceGo.HasValue ? vm.SeatPriceGo.Value : 0,
                SeatPriceReturn = vm.SeatPriceReturn.HasValue ? vm.SeatPriceReturn.Value : 0,

                // Price Type
                PriceTypeInt = (int)vm.PriceType,

                BusId = vm.BusId,
                DriverId = vm.DriverId,

                DriverName = vm.DriverName,
                DriverPhone = vm.DriverPhone,
                DriverUserId = vm.DriverUserId,

                IsArchivedInt = 0,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

                TripOriginInt = (int)TripOrigin.Manual,
                AutoPlanId = null,
                AutoPlanItemId = null,
            };

            try
            {
                await _db.Trips.AddAsync(trip);
                await _db.SaveChangesAsync();
                await AuditAsync("create", "trip", trip.Id, new
                {
                    trip.TripName,
                    trip.DepartDate,
                    trip.DepartTime,
                    trip.FromCity,
                    trip.ToCity,
                    trip.PickupPlace,
                    trip.PickupLat,
                    trip.PickupLon,
                    trip.BusId,
                    trip.DriverId,
                    PriceType = vm.PriceType.ToString(),
                    trip.SeatPriceGo,
                    trip.SeatPriceReturn,
                });
                await _db.SaveChangesAsync();

                TempData["Success"] = "Trip created successfully.";
                return RedirectToAction(nameof(Trips));
            }
            catch (DbUpdateException)
            {
                // concurrency / unique index hit
                ModelState.AddModelError("", "Database error occurred while saving trip. Please try again.");

                await FillTripLookups(vm);
                return View(V_CreateNewTrip, vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditTrip(Guid id)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound();

            var (any, go, ret) = await GetTripBookingStatsAsync(id);

            var model = new TripCreateEditModel
            {
                Id = trip.Id,
                TripName = trip.TripName,
                DepartDate = trip.DepartDate,
                DepartTime = trip.DepartTime,
                FromCity = trip.FromCity,
                ToCity = trip.ToCity,
                PickupPlace = trip.PickupPlace,
                PickupLat = trip.PickupLat,
                PickupLon = trip.PickupLon,
                DropoffPlace = trip.DropoffPlace,
                Notes = trip.Notes,
                SeatPriceGo = trip.SeatPriceGo,
                SeatPriceReturn = trip.SeatPriceReturn,
                BusId = trip.BusId,
                DriverId = trip.DriverId,
                DriverName = trip.DriverName,
                DriverPhone = trip.DriverPhone,
                DriverUserId = trip.DriverUserId,
                IsArchivedInt = trip.IsArchivedInt,

                PriceType = (TripPriceType)trip.PriceTypeInt,

                HasAnyBookings = any,
                HasGoBookings = go,
                HasReturnBookings = ret,
            };

            if (trip.IsArchivedInt == 1)
            {
                TempData["Err"] = "This trip is archived and cannot be edited.";
                return RedirectToAction(nameof(TripDetails), new { id = trip.Id });
            }

            ComputeEditLocks(model);

            ViewBag.AllowedPriceTypes = GetAllowedPriceTypesForEdit(model.PriceType, any, go, ret);

            await FillBusesDriversDropdownsForEdit(
                departDate: trip.DepartDate,
                currentTripId: trip.Id,
                currentBusId: model.BusId,
                currentDriverId: model.DriverId
            );

            return View(V_EditTrip, model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrip(TripCreateEditModel model)
        {
            if (!model.Id.HasValue) return BadRequest();

            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == model.Id.Value);
            if (trip == null) return NotFound();

            if (trip.IsArchivedInt == 1)
            {
                TempData["Err"] = "This trip is archived and cannot be edited.";
                return RedirectToAction(nameof(TripDetails), new { id = trip.Id });
            }

            // Refresh booking stats from DB (DO NOT trust posted flags)
            var (any, go, ret) = await GetTripBookingStatsAsync(trip.Id);

            model.HasAnyBookings = any;
            model.HasGoBookings = go;
            model.HasReturnBookings = ret;

            ComputeEditLocks(model);

            // Date/Time always locked
            model.DepartDate = trip.DepartDate;
            model.DepartTime = trip.DepartTime;

            if (!model.BusId.HasValue || model.BusId == Guid.Empty)
                model.BusId = trip.BusId;

            if (!model.DriverId.HasValue || model.DriverId == Guid.Empty)
                model.DriverId = trip.DriverId;

            await FillBusesDriversDropdownsForEdit(
                departDate: trip.DepartDate,
                currentTripId: trip.Id,
                currentBusId: model.BusId,
                currentDriverId: model.DriverId
            );

            var currentType = (TripPriceType)trip.PriceTypeInt;
            var requestedType = model.PriceType;

            var allowedTypes = GetAllowedPriceTypesForEdit(currentType, any, go, ret);
            ViewBag.AllowedPriceTypes = allowedTypes;

            if (!ModelState.IsValid)
                return View(V_EditTrip, model);

            if (!allowedTypes.Contains(requestedType))
            {
                ModelState.AddModelError(nameof(model.PriceType), "Cannot change trip type because there are bookings.");
                return View(V_EditTrip, model);
            }

            if (!any)
            {
                // No bookings => allow editing everything except date/time
                trip.TripName = model.TripName!;
                trip.FromCity = model.FromCity;
                trip.ToCity = model.ToCity;

                trip.PickupPlace = model.PickupPlace;
                trip.PickupLat = model.PickupLat;
                trip.PickupLon = model.PickupLon;

                trip.DropoffPlace = model.DropoffPlace;
                trip.Notes = model.Notes;

                trip.BusId = model.BusId;
                trip.DriverId = model.DriverId;

                trip.DriverName = model.DriverName;
                trip.DriverPhone = model.DriverPhone;
                trip.DriverUserId = model.DriverUserId;

                trip.PriceTypeInt = (int)requestedType;

                switch (requestedType)
                {
                    case TripPriceType.Go:
                        trip.SeatPriceGo = model.SeatPriceGo ?? 0m;
                        trip.SeatPriceReturn = 0m;
                        break;

                    case TripPriceType.Return:
                        trip.SeatPriceGo = 0m;
                        trip.SeatPriceReturn = model.SeatPriceReturn ?? 0m;
                        break;

                    case TripPriceType.Round:
                        trip.SeatPriceGo = model.SeatPriceGo ?? 0m;
                        trip.SeatPriceReturn = model.SeatPriceReturn ?? 0m;
                        break;
                }
            }
            else
            {
                // ============================
                // WITH BOOKINGS
                // ============================

                // 1) Round -> Go
                if (currentType == TripPriceType.Round && requestedType == TripPriceType.Go)
                {
                    // Allowed only when Go bookings exist and no Return bookings
                    trip.PriceTypeInt = (int)TripPriceType.Go;

                    // Keep Go route as-is
                    // Go price usually already used, so only update if your business allows
                    if (!go)
                        trip.SeatPriceGo = model.SeatPriceGo ?? trip.SeatPriceGo;

                    // Return side removed logically
                    trip.SeatPriceReturn = 0m;
                }
                // 2) Round -> Return
                else if (currentType == TripPriceType.Round && requestedType == TripPriceType.Return)
                {
                    // Allowed only when Return bookings exist and no Go bookings
                    trip.PriceTypeInt = (int)TripPriceType.Return;

                    // Keep current route as-is because this trip already represents that stored leg
                    trip.SeatPriceGo = 0m;

                    if (!ret)
                        trip.SeatPriceReturn = model.SeatPriceReturn ?? trip.SeatPriceReturn;
                }
                // 3) Go -> Round
                else if (currentType == TripPriceType.Go && requestedType == TripPriceType.Round)
                {
                    // IMPORTANT:
                    // Do NOT change route
                    // Only allow adding/editing Return price
                    trip.PriceTypeInt = (int)TripPriceType.Round;

                    // Keep current go price unless no go bookings
                    if (!go)
                        trip.SeatPriceGo = model.SeatPriceGo ?? trip.SeatPriceGo;

                    trip.SeatPriceReturn = model.SeatPriceReturn ?? trip.SeatPriceReturn;
                }
                // 4) Return -> Round
                else if (currentType == TripPriceType.Return && requestedType == TripPriceType.Round)
                {
                    // IMPORTANT:
                    // Convert this trip to Round by adding GO leg.
                    // Route must be reversed so GO becomes the opposite direction of current RETURN route.
                    trip.PriceTypeInt = (int)TripPriceType.Round;

                    // Reverse route
                    (trip.FromCity, trip.ToCity) = (trip.ToCity, trip.FromCity);

                    // Reverse pickup/dropoff text
                    (trip.PickupPlace, trip.DropoffPlace) = (trip.DropoffPlace, trip.PickupPlace);

                    // If you also store dropoff coordinates and want full reverse, do this too:
                    (trip.PickupLat, trip.DropoffLat) = (trip.DropoffLat, trip.PickupLat);
                    (trip.PickupLon, trip.DropoffLon) = (trip.DropoffLon, trip.PickupLon);

                    // New GO price should be editable
                    trip.SeatPriceGo = model.SeatPriceGo ?? trip.SeatPriceGo;

                    // Existing RETURN price remains, but allow edit only if no return bookings
                    if (!ret)
                        trip.SeatPriceReturn = model.SeatPriceReturn ?? trip.SeatPriceReturn;
                }
                // 5) Same type, price edit only
                else
                {
                    trip.PriceTypeInt = (int)requestedType;

                    if (!go)
                        trip.SeatPriceGo = model.SeatPriceGo ?? trip.SeatPriceGo;

                    if (!ret)
                        trip.SeatPriceReturn = model.SeatPriceReturn ?? trip.SeatPriceReturn;
                }

                // Core fields / bus / driver remain locked when bookings exist
            }

            await AuditAsync("edit", "trip", trip.Id, new
            {
                trip.TripName,
                trip.DepartDate,
                trip.DepartTime,
                trip.FromCity,
                trip.ToCity,
                trip.PickupPlace,
                trip.PickupLat,
                trip.PickupLon,
                trip.DropoffPlace,
                trip.DropoffLat,
                trip.DropoffLon,
                trip.BusId,
                trip.DriverId,
                trip.SeatPriceGo,
                trip.SeatPriceReturn,
                PriceType = ((TripPriceType)trip.PriceTypeInt).ToString(),
                trip.DriverName,
                trip.DriverPhone,
                trip.DriverUserId
            });

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(TripDetails), new { id = trip.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActiveTrip(Guid id)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound();

            // Activate
            if (trip.IsActiveInt != 1)
            {
                if (!TryGetTripDepartDateTime(trip, out var departAt))
                {
                    TempData["Error"] = "Cannot activate: trip date/time is invalid.";
                    return RedirectToAction(nameof(TripDetails), new { id });
                }

                if (DateTime.Now >= departAt)
                {
                    TempData["Error"] = "Cannot activate: this trip has already departed (past date/time).";
                    return RedirectToAction(nameof(TripDetails), new { id });
                }

                trip.IsActiveInt = 1;

                await AuditAsync("activate", "trip", id);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Trip set to Active successfully.";
            return RedirectToAction(nameof(TripDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InactiveTrip(Guid id)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound();

            // Deactivate
            if (trip.IsActiveInt != 0)
            {
                trip.IsActiveInt = 0;

                await AuditAsync("deactivate", "trip", id);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Trip set to Inactive successfully.";
            return RedirectToAction(nameof(TripDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrip(Guid id)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound();

            var hasBookings = await _db.Bookings
                .AsNoTracking()
                .AnyAsync(b => b.TripId == id);

            if (hasBookings)
            {
                if (trip.IsActiveInt != 0)
                {
                    trip.IsActiveInt = 0;

                    await AuditAsync("deactivate_instead_of_delete", "trip", id, new { reason = "has_bookings" });
                    await _db.SaveChangesAsync();
                }

                TempData["Success"] = "Trip has bookings, so it was set to Inactive instead of being deleted.";
                return RedirectToAction(nameof(TripDetails), new { id });
            }

            _db.Trips.Remove(trip);

            await AuditAsync("delete", "trip", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Trip deleted permanently.";
            return RedirectToAction(nameof(Trips));
        }

        //--------------------------------------------------------------------------------------//
        ///////////////////////////////// TRIPS HELPER FUNCTIONS /////////////////////////////////
        //--------------------------------------------------------------------------------------//

        private async Task<string> GenerateUniqueTripNameForDate(string depDateStr)
        {
            // Trip-(count+1) based on current count (non archived)
            var count = await _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr)
                .CountAsync();

            var baseNumber = count + 1;

            // ensure uniqueness even if gaps exist or concurrency
            for (int i = 0; i < 2000; i++)
            {
                var name = $"Trip-{baseNumber + i}";
                var exists = await _db.Trips.AsNoTracking()
                    .AnyAsync(t => t.IsArchivedInt == 0 && t.DepartDate == depDateStr && t.TripName == name);

                if (!exists) return name;
            }

            // fallback
            return $"Trip-{baseNumber}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        private void NormalizeModal(AutoTripTemplateModalModel vm)
        {
            vm.TripName = vm.TripName?.Trim() ?? "";
            vm.DepartTime = vm.DepartTime?.Trim() ?? "";
            vm.FromCity = vm.FromCity?.Trim();
            vm.ToCity = vm.ToCity?.Trim();
            vm.PickupPlace = vm.PickupPlace?.Trim();
            vm.DropoffPlace = vm.DropoffPlace?.Trim();
            vm.Notes = vm.Notes?.Trim();
        }

        private async Task<AutoTripPlan> EnsureDefaultPlan()
        {
            var plan = await _db.AutoTripPlans.Include(p => p.Items).FirstOrDefaultAsync();
            if (plan != null) return plan;

            plan = new AutoTripPlan
            {
                Name = "Default Plan",
                IsEnabledInt = 1,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ScheduleTypeInt = (int)AutoPlanScheduleType.Daily,
                ActivationModeInt = (int)AutoPlanActivationMode.ParallelAllActive
            };
            _db.AutoTripPlans.Add(plan);
            await _db.SaveChangesAsync();
            return plan;
        }

        private async Task<AutoTripsPlannerModel> BuildPlannerVM(string? checkDate)
        {
            checkDate = string.IsNullOrWhiteSpace(checkDate) ? null : checkDate.Trim();

            // Defaults
            const string DEFAULT_DEPART_TIME = "05:00";
            const string DEFAULT_FROM = "Asyut";
            const string DEFAULT_TO = "Hurgada";
            const string DEFAULT_PICKUP_TEXT = "First El-Helaly at Go Bus Company";
            const decimal DEFAULT_PICKUP_LAT = 27.180824m;
            const decimal DEFAULT_PICKUP_LON = 31.189725m;

            const decimal DEFAULT_PRICE_GO = 250m;
            const decimal DEFAULT_PRICE_RETURN = 250m;
            const decimal DEFAULT_PRICE_ROUND = 400m;

            var plan = await _db.AutoTripPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync();

            if (plan == null)
                plan = await EnsureDefaultPlan();

            var (availBusIds, availDriverIds) = await GetAvailableBusDriverIds(checkDate);

            int busesCount = availBusIds.Count;
            int driversCount = availDriverIds.Count;

            int possible = Math.Min(busesCount, driversCount);
            string limiting = busesCount <= driversCount ? "Bus" : "Driver";

            var items = (plan.Items ?? new List<AutoTripPlanItem>())
                .OrderBy(x => x.OrderNo)
                .ToList();

            int existingMaxOrder = items.Count == 0 ? 0 : items.Max(x => x.OrderNo);

            int displaySlots = checkDate != null
                ? Math.Max(possible, 0)
                : Math.Max(possible, existingMaxOrder);

            if (displaySlots <= 0) displaySlots = 1;

            // helper: normalize strings
            static string? TrimOrNull(string? s)
                => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

            // helper: 0 means "not set" for decimals in your entity
            static decimal? NullIfZero(decimal v)
                => v == 0m ? (decimal?)null : v;

            var vm = new AutoTripsPlannerModel
            {
                PlanId = plan.Id,
                Name = string.IsNullOrWhiteSpace(plan.Name) ? "Default Plan" : plan.Name,
                Notes = plan.Notes,
                IsEnabled = plan.IsEnabledInt == 1,

                ScheduleType = plan.ScheduleTypeInt,
                SpecificDate = plan.SpecificDate,
                ActivationMode = plan.ActivationModeInt,

                CheckDate = checkDate,

                AvailableBusesCount = busesCount,
                AvailableDriversCount = driversCount,
                PossibleTripsCount = possible,
                LimitingResource = limiting,

                DisplaySlots = displaySlots,
                Templates = new List<AutoTripTemplateRowModel>()
            };

            var byOrder = items.ToDictionary(x => x.OrderNo, x => x);

            vm.Templates = Enumerable.Range(1, displaySlots)
                .Select(n =>
                {
                    // Existing item from DB
                    if (byOrder.TryGetValue(n, out var it))
                    {
                        // strings
                        var tripName = TrimOrNull(it.TripName) ?? $"Trip-{n}";
                        var departTime = TrimOrNull(it.DepartTime) ?? DEFAULT_DEPART_TIME;

                        var fromCity = TrimOrNull(it.FromCity) ?? DEFAULT_FROM;
                        var toCity = TrimOrNull(it.ToCity) ?? DEFAULT_TO;

                        var pickupPlace = TrimOrNull(it.PickupPlace) ?? DEFAULT_PICKUP_TEXT;

                        // coords: treat 0 as not set
                        var lat = NullIfZero(it.PickupLat) ?? DEFAULT_PICKUP_LAT;
                        var lon = NullIfZero(it.PickupLon) ?? DEFAULT_PICKUP_LON;

                        // prices: treat 0 or negative as not set
                        var priceGo = (it.SeatPriceGo <= 0) ? DEFAULT_PRICE_GO : it.SeatPriceGo;
                        var priceReturn = (it.SeatPriceReturn <= 0) ? DEFAULT_PRICE_RETURN : it.SeatPriceReturn;
                        return new AutoTripTemplateRowModel
                        {
                            Id = it.Id,
                            OrderNo = it.OrderNo,
                            IsEnabled = it.IsEnabledInt == 1,

                            TripName = tripName,
                            DepartTime = departTime,

                            FromCity = fromCity,
                            ToCity = toCity,

                            PickupPlace = pickupPlace,
                            PickupLat = lat,
                            PickupLon = lon,

                            DropoffPlace = TrimOrNull(it.DropoffPlace), // keep optional
                            Notes = TrimOrNull(it.Notes),               // keep optional

                            SeatPriceGo = priceGo,
                            SeatPriceReturn = priceReturn,

                            BusId = it.BusId,
                            DriverId = it.DriverId
                        };
                    }

                    // New slot (not in DB) => defaults
                    return new AutoTripTemplateRowModel
                    {
                        Id = Guid.Empty,
                        OrderNo = n,

                        IsEnabled = false, // safest

                        TripName = $"Trip-{n}",
                        DepartTime = DEFAULT_DEPART_TIME,

                        FromCity = DEFAULT_FROM,
                        ToCity = DEFAULT_TO,

                        PickupPlace = DEFAULT_PICKUP_TEXT,
                        PickupLat = DEFAULT_PICKUP_LAT,
                        PickupLon = DEFAULT_PICKUP_LON,

                        DropoffPlace = null,
                        Notes = null,

                        SeatPriceGo = DEFAULT_PRICE_GO,
                        SeatPriceReturn = DEFAULT_PRICE_RETURN,
                        SeatPriceRound = DEFAULT_PRICE_ROUND,

                        BusId = null,
                        DriverId = null
                    };
                })
                .ToList();

            // availability check for selected date
            if (checkDate != null)
                ApplyAvailabilityCheck(vm, availBusIds, availDriverIds);

            return vm;
        }

        private void ApplyAvailabilityCheck(AutoTripsPlannerModel vm, HashSet<Guid> availBusIds, HashSet<Guid> availDriverIds)
        {
            var usedBus = new HashSet<Guid>();
            var usedDriver = new HashSet<Guid>();

            var considered = (vm.Templates ?? new List<AutoTripTemplateRowModel>())
                .OrderBy(x => x.OrderNo)
                .Where(x => x.OrderNo <= vm.DisplaySlots)
                .ToList();

            foreach (var t in considered)
            {
                if (!t.IsEnabled)
                {
                    t.IsAvailableForDate = true;
                    t.UnavailableReason = null;
                    continue;
                }

                if (!t.BusId.HasValue || !t.DriverId.HasValue)
                {
                    t.IsAvailableForDate = false;
                    t.UnavailableReason = "Missing bus/driver";
                    t.AutoEveryDay = false;
                    continue;
                }

                var busId = t.BusId.Value;
                var driverId = t.DriverId.Value;

                if (!availBusIds.Contains(busId))
                {
                    t.IsAvailableForDate = false;
                    t.UnavailableReason = "Bus not available (inactive or used)";
                    t.AutoEveryDay = false;
                    continue;
                }

                if (!availDriverIds.Contains(driverId))
                {
                    t.IsAvailableForDate = false;
                    t.UnavailableReason = "Driver not available (inactive or used)";
                    t.AutoEveryDay = false;
                    continue;
                }

                if (usedBus.Contains(busId))
                {
                    t.IsAvailableForDate = false;
                    t.UnavailableReason = "Bus duplicated inside selection";
                    t.AutoEveryDay = false;
                    continue;
                }

                if (usedDriver.Contains(driverId))
                {
                    t.IsAvailableForDate = false;
                    t.UnavailableReason = "Driver duplicated inside selection";
                    t.AutoEveryDay = false;
                    continue;
                }

                usedBus.Add(busId);
                usedDriver.Add(driverId);

                t.IsAvailableForDate = true;
                t.UnavailableReason = null;
            }
        }

        private async Task<(bool any, bool go, bool ret)> GetTripBookingStatsAsync(Guid tripId)
        {
            // IMPORTANT: adjust property names if your Booking entity differs.
            var q = _db.Bookings.AsNoTracking()
                .Where(b => b.IsCanceledInt == 0 && (b.TripId == tripId || b.ReturnTripId == tripId));

            var any = await q.AnyAsync();
            if (!any) return (false, false, false);

            var go = await q.AnyAsync(b => b.TripId == tripId);
            var ret = await q.AnyAsync(b => b.ReturnTripId == tripId);

            return (true, go, ret);
        }
        
        private void ComputeEditLocks(TripCreateEditModel vm)
        {
            // Date/time never editable
            vm.LockDepartDateTime = true;

            // Core fields / bus / driver
            vm.LockAllCoreFields = vm.HasAnyBookings;
            vm.CanEditCoreFields = !vm.HasAnyBookings;
            vm.CanEditBusDriver = !vm.HasAnyBookings;

            if (!vm.HasAnyBookings)
            {
                // No bookings => both prices editable
                vm.CanEditPriceGo = true;
                vm.CanEditPriceReturn = true;
                vm.CanEditPriceType = true;
                return;
            }

            // With bookings
            switch (vm.PriceType)
            {
                case TripPriceType.Go:
                    // Existing Go leg may already be booked, but when converting to Round
                    // we must allow adding/editing Return price
                    vm.CanEditPriceGo = !vm.HasGoBookings;
                    vm.CanEditPriceReturn = true;
                    break;

                case TripPriceType.Return:
                    // Existing Return leg may already be booked, but when converting to Round
                    // we must allow adding/editing Go price
                    vm.CanEditPriceGo = true;
                    vm.CanEditPriceReturn = !vm.HasReturnBookings;
                    break;

                case TripPriceType.Round:
                default:
                    // In round, only unbooked side is editable
                    vm.CanEditPriceGo = !vm.HasGoBookings;
                    vm.CanEditPriceReturn = !vm.HasReturnBookings;
                    break;
            }

            // Allowed values themselves are controlled elsewhere
            vm.CanEditPriceType = true;
        }

        private List<TripPriceType> GetAllowedPriceTypesForEdit(TripPriceType current, bool hasAnyBookings, bool hasGoBookings, bool hasReturnBookings)
        {
            if (!hasAnyBookings)
                return new List<TripPriceType>
                        {
                            TripPriceType.Go,
                            TripPriceType.Return,
                            TripPriceType.Round
                        };

            if (current == TripPriceType.Round)
            {
                if (hasGoBookings && !hasReturnBookings)
                    return new List<TripPriceType> { TripPriceType.Round, TripPriceType.Go };

                if (!hasGoBookings && hasReturnBookings)
                    return new List<TripPriceType> { TripPriceType.Round, TripPriceType.Return };

                return new List<TripPriceType> { TripPriceType.Round };
            }

            if (current == TripPriceType.Go)
                return new List<TripPriceType> { TripPriceType.Go, TripPriceType.Round };

            if (current == TripPriceType.Return)
                return new List<TripPriceType> { TripPriceType.Return, TripPriceType.Round };

            return new List<TripPriceType> { current };
        }

        private async Task FillBusesDriversDropdownsForEdit(string departDate, Guid currentTripId, Guid? currentBusId, Guid? currentDriverId)
        {
            // trips on same date (not archived) excluding current trip
            var tripsOnDate = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == departDate && t.Id != currentTripId);

            var usedBusIds = await tripsOnDate
                .Where(t => t.BusId != null)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsOnDate
                .Where(t => t.DriverId != null)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            // Available buses = active + not archived + NOT used that day (except current bus)
            var buses = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1)
                .OrderBy(b => b.BusNumber)
                .ToListAsync();

            var drivers = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1)
                .OrderBy(d => d.FullName)
                .ToListAsync();

            var busItems = buses
                .Where(b => !usedBusIds.Contains(b.Id) || (currentBusId.HasValue && b.Id == currentBusId.Value))
                .Select(b => new SelectListItem($"{b.BusNumber} - {b.PlateNumber}", b.Id.ToString(), currentBusId.HasValue && b.Id == currentBusId.Value))
                .ToList();

            var driverItems = drivers
                .Where(d => !usedDriverIds.Contains(d.Id) || (currentDriverId.HasValue && d.Id == currentDriverId.Value))
                .Select(d => new SelectListItem(d.FullName, d.Id.ToString(), currentDriverId.HasValue && d.Id == currentDriverId.Value))
                .ToList();

            ViewBag.AvailableBuses = busItems;
            ViewBag.AvailableDrivers = driverItems;
        }

        private async Task<(HashSet<Guid> busIds, HashSet<Guid> driverIds)> GetAvailableBusDriverIds(string? date)
        {
            var busIds = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1)
                .Select(b => b.Id)
                .ToHashSetAsync();

            var driverIds = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1)
                .Select(d => d.Id)
                .ToHashSetAsync();

            if (string.IsNullOrWhiteSpace(date))
                return (busIds, driverIds);

            var used = await _db.Trips.AsNoTracking()
                .Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.IsActiveInt == 1 &&
                    t.DepartDate == date)
                .Select(t => new { t.BusId, t.DriverId })
                .ToListAsync();

            foreach (var x in used)
            {
                if (x.BusId.HasValue) busIds.Remove(x.BusId.Value);
                if (x.DriverId.HasValue) driverIds.Remove(x.DriverId.Value);
            }

            return (busIds, driverIds);
        }

        private async Task<AutoTripTemplateModalModel> BuildModalVM(Guid planId, int orderNo, string? checkDate, AutoTripTemplateModalModel? existing = null)
        {
            var plan = await _db.AutoTripPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == planId);

            var item = await _db.AutoTripPlanItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PlanId == planId && x.OrderNo == orderNo);

            const string DEFAULT_DEPART_TIME = "05:00";
            const string DEFAULT_FROM = "Asyut";
            const string DEFAULT_TO = "Hurgada";
            const string DEFAULT_PICKUP_TEXT = "First El-Helaly at Go Bus Company";
            const decimal DEFAULT_PICKUP_LAT = 27.180824m;
            const decimal DEFAULT_PICKUP_LON = 31.189725m;
            const decimal DEFAULT_PRICE_GO = 250m;
            const decimal DEFAULT_PRICE_RETURN = 250m;

            var vm = existing ?? new AutoTripTemplateModalModel
            {
                PlanId = planId,
                OrderNo = orderNo,
                CheckDate = checkDate,
                IsEnabled = item?.IsEnabledInt == 1,
                AutoEveryDay = plan?.ScheduleTypeInt == (int)AutoPlanScheduleType.Daily,

                TripName = item?.TripName ?? $"Trip-{orderNo}",
                DepartTime = !string.IsNullOrWhiteSpace(item?.DepartTime) ? item.DepartTime : DEFAULT_DEPART_TIME,

                // Price Type
                PriceType = item != null ? (TripPriceType)item.PriceTypeInt : TripPriceType.Round,

                FromCity = !string.IsNullOrWhiteSpace(item?.FromCity) ? item.FromCity : DEFAULT_FROM,
                ToCity = !string.IsNullOrWhiteSpace(item?.ToCity) ? item.ToCity : DEFAULT_TO,

                PickupPlace = !string.IsNullOrWhiteSpace(item?.PickupPlace) ? item.PickupPlace : DEFAULT_PICKUP_TEXT,
                PickupLat = item?.PickupLat ?? DEFAULT_PICKUP_LAT,
                PickupLon = item?.PickupLon ?? DEFAULT_PICKUP_LON,

                DropoffPlace = item?.DropoffPlace,
                Notes = item?.Notes,

                SeatPriceGo = item?.SeatPriceGo ?? DEFAULT_PRICE_GO,
                SeatPriceReturn = item?.SeatPriceReturn ?? DEFAULT_PRICE_RETURN,

                BusId = item?.BusId,
                DriverId = item?.DriverId
            };

            // Build available buses and drivers lists
            var usedBusIdsInPlan = await _db.AutoTripPlanItems
                .AsNoTracking()
                .Where(x => x.PlanId == planId && x.IsEnabledInt == 1 && x.OrderNo != orderNo)
                .Where(x => x.BusId.HasValue)
                .Select(x => x.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIdsInPlan = await _db.AutoTripPlanItems
                .AsNoTracking()
                .Where(x => x.PlanId == planId && x.IsEnabledInt == 1 && x.OrderNo != orderNo)
                .Where(x => x.DriverId.HasValue)
                .Select(x => x.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            var dateIso = (checkDate ?? "").Trim();
            List<Guid> usedBusIdsInTrips = new();
            List<Guid> usedDriverIdsInTrips = new();

            if (!string.IsNullOrWhiteSpace(dateIso))
            {
                usedBusIdsInTrips = await _db.Trips
                    .AsNoTracking()
                    .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso)
                    .Where(t => t.BusId.HasValue)
                    .Select(t => t.BusId!.Value)
                    .Distinct()
                    .ToListAsync();

                usedDriverIdsInTrips = await _db.Trips
                    .AsNoTracking()
                    .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso)
                    .Where(t => t.DriverId.HasValue)
                    .Select(t => t.DriverId!.Value)
                    .Distinct()
                    .ToListAsync();
            }

            var usedBus = new HashSet<Guid>(usedBusIdsInPlan);
            foreach (var x in usedBusIdsInTrips) usedBus.Add(x);

            var usedDriver = new HashSet<Guid>(usedDriverIdsInPlan);
            foreach (var x in usedDriverIdsInTrips) usedDriver.Add(x);

            var buses = await _db.Buses
                .AsNoTracking()
                .Where(b => b.IsActiveInt == 1 && b.IsArchivedInt == 0)
                .OrderBy(b => b.BusNumber)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.BusNumber
                })
                .ToListAsync();

            var drivers = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.IsActiveInt == 1 && d.IsArchivedInt == 0)
                .OrderBy(d => d.FullName)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.FullName
                })
                .ToListAsync();

            // Filter available buses/drivers
            vm.Buses = buses
                .Where(b => !usedBus.Contains(Guid.Parse(b.Value)) || (vm.BusId.HasValue && vm.BusId.Value.ToString() == b.Value))
                .ToList();

            vm.Drivers = drivers
                .Where(d => !usedDriver.Contains(Guid.Parse(d.Value)) || (vm.DriverId.HasValue && vm.DriverId.Value.ToString() == d.Value))
                .ToList();

            return vm;
        }

        private static void NormalizeTripVM(TripCreateEditModel vm)
        {
            vm.DepartDate = vm.DepartDate?.Trim();
            vm.DepartTime = vm.DepartTime?.Trim();
            vm.FromCity = vm.FromCity?.Trim();
            vm.ToCity = vm.ToCity?.Trim();
            vm.PickupPlace = vm.PickupPlace?.Trim();
            vm.DropoffPlace = vm.DropoffPlace?.Trim();
            vm.Notes = vm.Notes?.Trim();
        }

        private static bool TryBuildDepartAt(string? departDate, string? departTime, out DateTime dt)
        {
            dt = default;
            if (string.IsNullOrWhiteSpace(departDate) || string.IsNullOrWhiteSpace(departTime))
                return false;

            return DateTime.TryParseExact(
                $"{departDate} {departTime}",
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dt
            );
        }

        private void ValidateTripBusinessRules(ModelStateDictionary ms, TripCreateEditModel vm, DateTime? departAt)
        {
            if (departAt == null)
                return;

            if (departAt.Value < DateTime.Today)
                ms.AddModelError(nameof(vm.DepartDate), "Departure date can't be in the past.");
        }

        private async Task ValidateTripConflicts(TripCreateEditModel vm, Guid? excludeTripId)
        {
            if (string.IsNullOrWhiteSpace(vm.DepartDate))
                return;

            var q = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == vm.DepartDate);

            if (excludeTripId.HasValue)
                q = q.Where(t => t.Id != excludeTripId.Value);

            // Bus conflict
            if (vm.BusId.HasValue)
            {
                var busBusy = await q.AnyAsync(t => t.BusId == vm.BusId.Value);
                if (busBusy)
                    ModelState.AddModelError(nameof(vm.BusId), "This bus is already assigned to another trip on the selected date.");
            }

            // Driver conflict
            if (vm.DriverId.HasValue)
            {
                var driverBusy = await q.AnyAsync(t => t.DriverId == vm.DriverId.Value);
                if (driverBusy)
                    ModelState.AddModelError(nameof(vm.DriverId), "This driver is already assigned to another trip on the selected date.");
            }
        }

        private async Task FillTripLookups(TripCreateEditModel vm)
        {
            var date = vm.DepartDate ?? DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

            // trips on same date (not archived)
            var tripsOnDate = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == date);

            var usedBusIds = await tripsOnDate
                .Where(t => t.BusId != null)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsOnDate
                .Where(t => t.DriverId != null)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            var availableBuses = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1 && !usedBusIds.Contains(b.Id))
                .OrderBy(b => b.BusNumber)
                .ToListAsync();

            var availableDrivers = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1 && !usedDriverIds.Contains(d.Id))
                .OrderBy(d => d.FullName)
                .ToListAsync();

            ViewBag.AvailableBuses = availableBuses.Select(b =>
                new SelectListItem($"{b.BusNumber} - {b.PlateNumber}", b.Id.ToString())
            ).ToList();

            ViewBag.AvailableDrivers = availableDrivers.Select(d =>
                new SelectListItem(d.FullName, d.Id.ToString())
            ).ToList();
        }

        private async Task FillBusesDriversDropdowns(Guid? busId = null, Guid? driverId = null)
        {
            var buses = await _db.Buses
                .AsNoTracking()
                .Where(b => b.IsArchivedInt == 0)
                .OrderBy(b => b.BusNumber)
                .Select(b => new { b.Id, b.BusNumber })
                .ToListAsync();

            var drivers = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.IsArchivedInt == 0)
                .OrderBy(d => d.FullName)
                .Select(d => new { d.Id, d.FullName })
                .ToListAsync();

            ViewBag.AvailableBuses = new SelectList(buses, "Id", "BusNumber", busId);
            ViewBag.AvailableDrivers = new SelectList(drivers, "Id", "FullName", driverId);
        }

        private bool TryGetTripDepartDateTime(NoufirTours.Data.Trip trip, out DateTime departAt)
        {
            departAt = default;

            // Expected: "yyyy-MM-dd" + "HH:mm"  (your DB format)
            var dtStr = $"{trip.DepartDate} {trip.DepartTime}";
            return DateTime.TryParseExact(
                dtStr,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out departAt
            );
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////////// BUSES //////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Buses(string? q, bool includeInactive = true)
        {
            q = (q ?? "").Trim();

            var query = _db.Buses
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
                query = query.Where(b => b.IsActiveInt == 1);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(b =>
                    (b.BusNumber != null && b.BusNumber.Contains(q)) ||
                    (b.PlateNumber != null && b.PlateNumber.Contains(q)) ||
                    (b.ChassisNumber != null && b.ChassisNumber.Contains(q)) ||
                    (b.ModelName != null && b.ModelName.Contains(q)) ||
                    (b.ModelYear != null && b.ModelYear.ToString()!.Contains(q))
                );
            }

            var list = await query
                .OrderByDescending(b => b.Id)
                .Select(b => new BusListItemModel
                {
                    Id = b.Id,
                    BusNumber = b.BusNumber,
                    PlateNumber = b.PlateNumber,
                    ChassisNumber = b.ChassisNumber,
                    ModelName = b.ModelName,
                    ModelYear = b.ModelYear,
                    SeatsCount = b.SeatsCount.HasValue ? b.SeatsCount.Value : 0,
                    IsActive = b.IsActiveInt == 1
                })
                .ToListAsync();

            ViewBag.Q = q;
            ViewBag.IncludeInactive = includeInactive;

            return View(V_BusesList, list);
        }

        [HttpGet]
        public async Task<IActionResult> BusDetails(Guid id)
        {
            var bus = await _db.Buses
                .AsNoTracking()
                .Include(b => b.Seats)
                .Include(b => b.Trips)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bus == null) return NotFound();

            var vm = new BusDetailsModel
            {
                Bus = new BusDetailsHeaderModel
                {
                    Id = bus.Id,
                    BusNumber = bus.BusNumber,
                    ChassisNumber = bus.ChassisNumber,
                    PlateNumber = bus.PlateNumber,

                    Manufacturer = bus.Manufacturer,
                    ModelName = bus.ModelName,
                    ModelYear = bus.ModelYear,
                    BusType = bus.BusType,

                    SeatsCount = bus.SeatsCount ?? 0,
                    Color = bus.Color,
                    Specs = bus.Specs,
                    Notes = bus.Notes,

                    IsActive = bus.IsActiveInt == 1,
                    IsArchived = bus.IsArchivedInt == 1,

                    ArchivedAtCairo = UnixToCairoDateTime(bus.ArchivedAtUnix)
                },
                LayoutWidth = bus.LayoutWidth,
                LayoutHeight = bus.LayoutHeight,

                Seats = bus.Seats
                    .OrderBy(s => s.Y).ThenBy(s => s.X)
                    .Select(s => new BusSeatModel
                    {
                        ElementType = s.ElementType,
                        SeatCode = s.SeatCode!,
                        X = s.X,
                        Y = s.Y,
                        IsActive = s.IsActiveInt == 1,
                        Role = s.Role!,
                        AssignedDriverId = s.AssignedDriverId,
                        Label = s.Label,
                        DoorSide = s.DoorSide,
                        DoorOffset = s.DoorOffset
                    })
                    .ToList(),

                // Trips for this bus
                Trips = (bus.Trips ?? new List<NoufirTours.Data.Trip>())
                    .OrderByDescending(t => t.DepartDate)
                    .ThenByDescending(t => t.DepartTime)
                    .Select(t => new BusTripSummaryModel
                    {
                        TripId = t.Id,
                        Title = string.IsNullOrWhiteSpace(t.TripName) ? $"Trip #{t.Id}" : t.TripName!,
                        DepartDate = t.DepartDate,
                        DepartTime = t.DepartTime,
                        IsArchived = t.IsArchivedInt == 1
                    })
                    .ToList()
            };
            return View(V_BusDetails, vm);
        }

        [HttpGet]
        public async Task<IActionResult> AvailabilityByDate(string date, Guid? excludeTripId = null)
        {
            // Normalize date ISO (yyyy-MM-dd)
            var dateIso = (date ?? "").Trim();
            DateTime dateConverting;
            if (string.IsNullOrWhiteSpace(dateIso))
                return BadRequest("date is required");

            // Allow parsing from browser date input
            if (!DateTime.TryParseExact(dateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _))
            {
                // fallback try parse
                if (!DateTime.TryParse(dateIso, out dateConverting))
                    return BadRequest("Invalid date format");
                dateIso = dateConverting.ToString("yyyy-MM-dd");
            }

            var payload = await BuildAvailabilityPayloadAsync(dateIso, excludeTripId);

            return Json(payload);
        }

        private async Task<object> BuildAvailabilityPayloadAsync(string dateIso, Guid? excludeTripId)
        {
            // Trips on this date (non-archived) => only thing that blocks bus/driver
            var tripsQ = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso);

            if (excludeTripId.HasValue && excludeTripId.Value != Guid.Empty)
                tripsQ = tripsQ.Where(t => t.Id != excludeTripId.Value);

            var usedBusIds = await tripsQ
                .Where(t => t.BusId.HasValue)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsQ
                .Where(t => t.DriverId.HasValue)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            // Available buses
            var buses = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1)
                .Where(b => !usedBusIds.Contains(b.Id))
                .OrderBy(b => b.BusNumber)
                .Select(b => new
                {
                    id = b.Id.ToString(),
                    text = (b.BusNumber ?? "Bus") + (string.IsNullOrWhiteSpace(b.PlateNumber) ? "" : $" • {b.PlateNumber}")
                })
                .ToListAsync();

            // Available drivers
            var drivers = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1)
                .Where(d => !usedDriverIds.Contains(d.Id))
                .OrderBy(d => d.FullName)
                .Select(d => new
                {
                    id = d.Id.ToString(),
                    text = (d.FullName ?? "Driver")
                })
                .ToListAsync();

            return new
            {
                date = dateIso,
                buses,
                drivers
            };
        }

        private async Task FillBusesDriversViewBagsAsync(string dateIso, Guid? excludeTripId = null)
        {
            var payload = await BuildAvailabilityPayloadAsync(dateIso, excludeTripId);

            // payload is anonymous; easiest re-query in same method style:
            // But to keep it simple + stable, just rebuild lists directly here:

            var tripsQ = _db.Trips.AsNoTracking()
                .Where(t => t.IsArchivedInt == 0 && t.DepartDate == dateIso);

            if (excludeTripId.HasValue && excludeTripId.Value != Guid.Empty)
                tripsQ = tripsQ.Where(t => t.Id != excludeTripId.Value);

            var usedBusIds = await tripsQ
                .Where(t => t.BusId.HasValue)
                .Select(t => t.BusId!.Value)
                .Distinct()
                .ToListAsync();

            var usedDriverIds = await tripsQ
                .Where(t => t.DriverId.HasValue)
                .Select(t => t.DriverId!.Value)
                .Distinct()
                .ToListAsync();

            var buses = await _db.Buses.AsNoTracking()
                .Where(b => b.IsArchivedInt == 0 && b.IsActiveInt == 1)
                .Where(b => !usedBusIds.Contains(b.Id))
                .OrderBy(b => b.BusNumber)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = (b.BusNumber ?? "Bus") + (string.IsNullOrWhiteSpace(b.PlateNumber) ? "" : $" • {b.PlateNumber}")
                })
                .ToListAsync();

            var drivers = await _db.Drivers.AsNoTracking()
                .Where(d => d.IsArchivedInt == 0 && d.IsActiveInt == 1)
                .Where(d => !usedDriverIds.Contains(d.Id))
                .OrderBy(d => d.FullName)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = (d.FullName ?? "Driver")
                })
                .ToListAsync();

            ViewBag.AvailableBuses = buses;
            ViewBag.AvailableDrivers = drivers;
        }

        [HttpGet]
        public IActionResult CreateNewBus()
        {
            var vm = new BusCreateEditMode
            {
                LayoutWidth = 4,
                LayoutHeight = 5,
                Color = "White",
                SeatsJson = """
                [
                {"type":"Seat","code":"0","x":0,"y":0,"isActive":true,"role":"Driver","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"2","x":0,"y":1,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"5","x":0,"y":2,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"8","x":0,"y":3,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"12","x":1,"y":4,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"1","x":3,"y":0,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"11","x":0,"y":4,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"13","x":2,"y":4,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"14","x":3,"y":4,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"9","x":1,"y":3,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"10","x":3,"y":3,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"7","x":3,"y":2,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"4","x":2,"y":1,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"3","x":1,"y":1,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Seat","code":"6","x":1,"y":2,"isActive":true,"role":"Passenger","label":null,"side":null,"offset":null},
                {"type":"Door","code":null,"x":3,"y":2,"isActive":null,"role":null,"label":"Bus Door","side":"R","offset":0.48636349764737213}]
                """
            };
            return View(V_CreateNewBus, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNewBus(BusCreateEditMode vm)
        {
            NormalizeBusVM(vm);

            var seats = ParseSeats(vm.SeatsJson, vm.LayoutWidth, vm.LayoutHeight);
            ValidateSeats(ModelState, vm, seats);

            await ValidateBusUniques(vm, excludeBusId: null);

            if (!ModelState.IsValid)
            {
                return View(V_CreateNewBus, vm);
            }

            var bus = new Bus
            {
                BusNumber = vm.BusNumber,
                ChassisNumber = vm.ChassisNumber,
                PlateNumber = vm.PlateNumber,
                Manufacturer = vm.Manufacturer,
                ModelName = vm.ModelName,
                ModelYear = vm.ModelYear,
                BusType = vm.BusType,
                SeatsCount = vm.SeatsCount,
                Color = vm.Color,
                Specs = vm.Specs,
                Notes = vm.Notes,
                LayoutWidth = vm.LayoutWidth,
                LayoutHeight = vm.LayoutHeight,
                IsActiveInt = 1,
                IsArchivedInt = 0
            };

            foreach (var it in seats)
            {
                var type = (it.Type ?? "Seat").Trim();

                bus.Seats.Add(new BusSeat
                {
                    ElementType = type,
                    SeatCode = type.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? it.Code : null,
                    X = it.X,
                    Y = it.Y,
                    IsActiveInt = type.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? ((it.IsActive ?? true) ? 1 : 0) : 1,
                    Role = type.Equals("Seat", StringComparison.OrdinalIgnoreCase)
                            ? (string.IsNullOrWhiteSpace(it.Role) ? "Passenger" : it.Role.Trim())
                            : null,
                    DoorSide = type.Equals("Door", StringComparison.OrdinalIgnoreCase) ? (it.Side ?? "R") : null,
                    DoorOffset = type.Equals("Door", StringComparison.OrdinalIgnoreCase) ? (it.Offset ?? 0.5) : null,
                    Label = !type.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? it.Label : null
                });
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Buses.Add(bus);
                await _db.SaveChangesAsync();
                await AuditAsync("create", "bus", bus.Id, new { bus.BusNumber, bus.PlateNumber, bus.ChassisNumber, bus.IsActiveInt });
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Bus created successfully.";
                return RedirectToAction(nameof(Buses));
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();

                ModelState.AddModelError("", "Save failed. Please ensure no duplicated Seat Codes or duplicated positions (X,Y).");
                return View(V_CreateNewBus, vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditBus(Guid id)
        {
            var bus = await _db.Buses
                .AsNoTracking()
                .Include(b => b.Seats)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bus == null) return NotFound();
            // BLOCK EDIT if Archived (Deleted)

            if (bus.IsArchivedInt == 1)
            {
                TempData["Error"] = "This driver is deleted, so editing is not allowed.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            var usedInTrips = await BusIsUsedInAnyTrip(id);
            var isArchived = bus.IsArchivedInt == 1;

            var layoutItems = bus.Seats
                .OrderBy(s => s.Y).ThenBy(s => s.X)
                .Select(s => new BusLayoutItemModel
                {
                    Type = s.ElementType,
                    Code = s.SeatCode,
                    X = s.X,
                    Y = s.Y,
                    IsActive = s.ElementType.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? (s.IsActiveInt == 1) : null,
                    Role = s.ElementType.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? (s.Role ?? "Passenger") : null,
                    Label = !s.ElementType.Equals("Seat", StringComparison.OrdinalIgnoreCase) ? (s.Label ?? s.ElementType) : null,
                    Side = s.ElementType.Equals("Door", StringComparison.OrdinalIgnoreCase) ? (s.DoorSide ?? "R") : null,
                    Offset = s.ElementType.Equals("Door", StringComparison.OrdinalIgnoreCase) ? (s.DoorOffset ?? 0.5) : null
                })
                .ToList();

            var vm = new BusCreateEditMode
            {
                Id = bus.Id,
                BusNumber = bus.BusNumber,
                ChassisNumber = bus.ChassisNumber,
                PlateNumber = bus.PlateNumber,
                Manufacturer = bus.Manufacturer,
                ModelName = bus.ModelName,
                ModelYear = bus.ModelYear,
                BusType = bus.BusType,
                SeatsCount = bus.SeatsCount,
                Color = bus.Color,
                Specs = bus.Specs,
                Notes = bus.Notes,
                LayoutWidth = bus.LayoutWidth,
                LayoutHeight = bus.LayoutHeight,
                SeatsJson = JsonSerializer.Serialize(layoutItems),

                // flags
                LockCoreFields = usedInTrips,
                LockAllFields = isArchived,
                IsArchived = isArchived
            };

            return View(V_EditBus, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBus(BusCreateEditMode vm)
        {
            if (vm.Id == null) return BadRequest();

            NormalizeBusVM(vm);

            var bus = await _db.Buses
                .Include(b => b.Seats)
                .FirstOrDefaultAsync(b => b.Id == vm.Id.Value);

            if (bus == null) return NotFound();

            if (bus.IsArchivedInt == 1)
            {
                TempData["Error"] = "This bus is deleted. Editing is not allowed.";
                return RedirectToAction(nameof(BusDetails), new { id = bus.Id });
            }

            var usedInTrips = await BusIsUsedInAnyTrip(bus.Id);

            // Parse items always (we will use it for allowed updates)
            var items = ParseLayoutItems(vm.SeatsJson, vm.LayoutWidth, vm.LayoutHeight);

            if (usedInTrips)
            {
                // Only allow Specs + Notes
                bus.Specs = vm.Specs;
                bus.Notes = vm.Notes;

                // Allowed seat changes: Role + IsActive for Seat items only, matched by (X,Y) and ElementType Seat
                var incomingSeatMap = items
                    .Where(i => (i.Type ?? "Seat").Equals("Seat", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(i => $"{i.X}:{i.Y}", i => i);

                foreach (var s in bus.Seats.Where(x => x.ElementType == "Seat"))
                {
                    var key = $"{s.X}:{s.Y}";
                    if (!incomingSeatMap.TryGetValue(key, out var incoming))
                        continue;

                    // Role allowed: Passenger/Driver/Assistant
                    var role = (incoming.Role ?? s.Role ?? "Passenger").Trim();
                    role = role.Equals("Driver", StringComparison.OrdinalIgnoreCase) ? "Driver"
                         : role.Equals("Assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant"
                         : "Passenger";

                    s.Role = role;

                    var active = incoming.IsActive ?? (s.IsActiveInt == 1);
                    s.IsActiveInt = active ? 1 : 0;
                }

                // Optional safety: Ensure <= 1 driver seat and <= 1 assistant seat
                var driverCount = bus.Seats.Count(x => x.ElementType == "Seat" && (x.Role ?? "") == "Driver");
                var assistantCount = bus.Seats.Count(x => x.ElementType == "Seat" && (x.Role ?? "") == "Assistant");
                if (driverCount > 1 || assistantCount > 1)
                {
                    ModelState.AddModelError(nameof(vm.SeatsJson), "Only one Driver seat and one Assistant seat are allowed.");
                    vm.LockCoreFields = true;
                    vm.LockAllFields = false;
                    vm.IsArchived = false;
                    return View(V_EditBus, vm);
                }
                await AuditAsync("edit", "bus", bus.Id, new { bus.BusNumber, bus.PlateNumber, bus.ChassisNumber, usedInTrips });
                await _db.SaveChangesAsync();

                TempData["Success"] = "Bus updated";
                return RedirectToAction(nameof(BusDetails), new { id = bus.Id });
            }

            // Not used in trips => Full edit allowed as before
            ValidateSeats(ModelState, vm, items);
            await ValidateBusUniques(vm, excludeBusId: bus.Id);

            if (!ModelState.IsValid)
            {
                vm.LockCoreFields = false;
                vm.LockAllFields = false;
                vm.IsArchived = false;
                return View(V_EditBus, vm);
            }

            // Update bus fields
            bus.BusNumber = vm.BusNumber;
            bus.ChassisNumber = vm.ChassisNumber;
            bus.PlateNumber = vm.PlateNumber;
            bus.Manufacturer = vm.Manufacturer;
            bus.ModelName = vm.ModelName;
            bus.ModelYear = vm.ModelYear;
            bus.BusType = vm.BusType;
            bus.SeatsCount = vm.SeatsCount;
            bus.Color = vm.Color;
            bus.Specs = vm.Specs;
            bus.Notes = vm.Notes;
            bus.LayoutWidth = vm.LayoutWidth;
            bus.LayoutHeight = vm.LayoutHeight;

            // Replace seats
            _db.BusSeats.RemoveRange(bus.Seats);
            bus.Seats.Clear();

            foreach (var it in items)
            {
                var type = (it.Type ?? "Seat").Trim();

                bool isSeat = type.Equals("Seat", StringComparison.OrdinalIgnoreCase);
                bool isDoor = type.Equals("Door", StringComparison.OrdinalIgnoreCase);

                bus.Seats.Add(new BusSeat
                {
                    ElementType = type,

                    SeatCode = isSeat ? (string.IsNullOrWhiteSpace(it.Code) ? null : it.Code.Trim()) : null,
                    X = it.X,
                    Y = it.Y,

                    IsActiveInt = isSeat ? ((it.IsActive ?? true) ? 1 : 0) : 1,
                    Role = isSeat ? (string.IsNullOrWhiteSpace(it.Role) ? "Passenger" : it.Role.Trim()) : null,

                    Label = !isSeat ? (string.IsNullOrWhiteSpace(it.Label) ? type : it.Label.Trim()) : null,

                    DoorSide = isDoor ? (string.IsNullOrWhiteSpace(it.Side) ? "R" : it.Side.Trim().ToUpperInvariant()) : null,
                    DoorOffset = isDoor ? Clamp01(it.Offset ?? 0.5) : null
                });
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await AuditAsync("edit", "bus", bus.Id, new { bus.BusNumber, bus.PlateNumber, bus.ChassisNumber, usedInTrips });
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Bus updated successfully.";
                return RedirectToAction(nameof(BusDetails), new { id = bus.Id });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Save failed. Some values violate unique constraints.");
                return View(V_EditBus, vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateBus(Guid id)
        {
            var b = await _db.Buses.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            if (b.IsArchivedInt == 1)
            {
                TempData["Error"] = "This bus is deleted. Restore it first.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            b.IsActiveInt = 1;
            await AuditAsync("activate", "bus", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Bus set to Active.";
            return RedirectToAction(nameof(BusDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateBus(Guid id)
        {
            var b = await _db.Buses.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            if (b.IsArchivedInt == 1)
            {
                TempData["Error"] = "This bus is deleted.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            b.IsActiveInt = 0;
            await AuditAsync("deactivate", "bus", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Bus set to Inactive.";
            return RedirectToAction(nameof(BusDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBus(Guid id)
        {
            var bus = await _db.Buses
                .Include(b => b.Seats)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bus == null) return NotFound();

            if (bus.IsArchivedInt == 1)
            {
                TempData["Error"] = "Bus is already deleted.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            var hasTrips = await _db.Trips.AsNoTracking().AnyAsync(t => t.BusId == id);

            if (hasTrips)
            {
                // Soft Delete = Deactivate + Archive
                bus.IsActiveInt = 0;
                bus.IsArchivedInt = 1;
                bus.ArchivedAtUnix = UtcUnixNow();
                bus.ArchivedByUserId = null;

                await AuditAsync("archive", "bus", id, new { reason = "has_trips" });
                await _db.SaveChangesAsync();

                TempData["Success"] = "Bus has trips, so it was deleted as Archived and set to Inactive.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            // Hard Delete (no trips)
            _db.BusSeats.RemoveRange(bus.Seats);
            _db.Buses.Remove(bus);
            await AuditAsync("delete", "bus", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Bus deleted successfully.";
            return RedirectToAction(nameof(Buses));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBus(Guid id)
        {
            var b = await _db.Buses.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            if (b.IsArchivedInt != 1)
            {
                TempData["Error"] = "Bus is not deleted.";
                return RedirectToAction(nameof(BusDetails), new { id });
            }

            b.IsArchivedInt = 0;
            b.ArchivedAtUnix = null;
            b.ArchivedByUserId = null;

            b.IsActiveInt = 1;

            await AuditAsync("restore", "bus", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Bus restored successfully.";
            return RedirectToAction(nameof(BusDetails), new { id });
        }

        //---------------------------------------------------------------------------------------//
        /////////////////////////////////// BUS HELPER FUNCTIONS //////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private static List<BusLayoutItemModel> ParseLayoutItems(string? json, int w, int h)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<BusLayoutItemModel>();

            List<BusLayoutItemModel>? arr;
            try
            {
                arr = JsonSerializer.Deserialize<List<BusLayoutItemModel>>(json);
            }
            catch
            {
                return new List<BusLayoutItemModel>();
            }

            arr ??= new List<BusLayoutItemModel>();

            foreach (var it in arr)
            {
                it.Type = (it.Type ?? "Seat").Trim();
                it.X = Math.Max(0, Math.Min(w - 1, it.X));
                it.Y = Math.Max(0, Math.Min(h - 1, it.Y));

                // Door side normalization
                if (it.Type.Equals("Door", StringComparison.OrdinalIgnoreCase))
                {
                    var s = (it.Side ?? "R").Trim().ToUpperInvariant();
                    it.Side = (s == "L" || s == "R" || s == "T" || s == "B") ? s : "R";
                    it.Offset = Clamp01(it.Offset ?? 0.5);
                    it.Label ??= "Bus Door";
                }

                if (it.Type.Equals("WC", StringComparison.OrdinalIgnoreCase))
                {
                    it.Label ??= "WC";
                }
            }

            return arr;
        }

        private static void NormalizeBusVM(BusCreateEditMode vm)
        {
            vm.BusNumber = (vm.BusNumber ?? "").Trim();
            vm.ChassisNumber = (vm.ChassisNumber ?? "").Trim();
            vm.PlateNumber = string.IsNullOrWhiteSpace(vm.PlateNumber) ? null : vm.PlateNumber.Trim();

            vm.Manufacturer = string.IsNullOrWhiteSpace(vm.Manufacturer) ? null : vm.Manufacturer.Trim();
            vm.ModelName = string.IsNullOrWhiteSpace(vm.ModelName) ? null : vm.ModelName.Trim();
            vm.BusType = string.IsNullOrWhiteSpace(vm.BusType) ? null : vm.BusType.Trim();
            vm.Color = string.IsNullOrWhiteSpace(vm.Color) ? null : vm.Color.Trim();
            vm.Specs = string.IsNullOrWhiteSpace(vm.Specs) ? null : vm.Specs.Trim();
            vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            vm.SeatsJson = string.IsNullOrWhiteSpace(vm.SeatsJson) ? "[]" : vm.SeatsJson.Trim();

            if (vm.LayoutWidth <= 0) vm.LayoutWidth = 3;
            if (vm.LayoutHeight <= 0) vm.LayoutHeight = 5;

            vm.LayoutWidth = Math.Clamp(vm.LayoutWidth, 1, 30);
            vm.LayoutHeight = Math.Clamp(vm.LayoutHeight, 1, 60);
        }

        private static List<BusLayoutItemModel> ParseSeats(string json, int w, int h)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<BusLayoutItemModel>>(json) ?? new List<BusLayoutItemModel>();

                foreach (var it in items)
                {
                    it.Type = string.IsNullOrWhiteSpace(it.Type) ? "Seat" : it.Type.Trim();

                    it.X = Math.Max(0, Math.Min(w - 1, it.X));
                    it.Y = Math.Max(0, Math.Min(h - 1, it.Y));

                    if (it.Type.Equals("Seat", StringComparison.OrdinalIgnoreCase))
                    {
                        it.Role = string.IsNullOrWhiteSpace(it.Role) ? "Passenger" : it.Role.Trim();
                        it.Code = (it.Code ?? "").Trim();
                        it.IsActive ??= true;
                    }
                    else
                    {
                        // Door / WC
                        it.Label = string.IsNullOrWhiteSpace(it.Label) ? it.Type : it.Label.Trim();
                        it.Side = string.IsNullOrWhiteSpace(it.Side) ? "R" : it.Side.Trim().ToUpperInvariant();
                        it.Offset ??= 0.5;
                    }
                }

                return items;
            }
            catch
            {
                return new List<BusLayoutItemModel>();
            }
        }

        private static void ValidateSeats(ModelStateDictionary modelState, BusCreateEditMode vm, List<BusLayoutItemModel> items)
        {
            var seats = items.Where(i => i.Type.Equals("Seat", StringComparison.OrdinalIgnoreCase)).ToList();

            if (seats.Count == 0)
                modelState.AddModelError(nameof(vm.SeatsJson), "Please add at least one seat.");

            // seat code required for Passenger seats only (Driver/Assistant allowed D/A auto)
            foreach (var s in seats)
            {
                var role = (s.Role ?? "Passenger").Trim();
                var code = (s.Code ?? "").Trim();

                if (!role.Equals("Driver", StringComparison.OrdinalIgnoreCase) &&
                    !role.Equals("Assistant", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(code))
                {
                    modelState.AddModelError(nameof(vm.SeatsJson), "Passenger seats must have seat code.");
                    break;
                }
            }

            // unique codes (ignore empty / D / A duplication handled by UI usually)
            var codes = seats.Select(s => (s.Code ?? "").Trim())
                             .Where(c => !string.IsNullOrWhiteSpace(c))
                             .ToList();

            if (codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count)
                modelState.AddModelError(nameof(vm.SeatsJson), "Seat codes must be unique.");

            // unique cell for ALL items (Seat/WC/Door) to match DB unique index
            var interior = items.Where(i => !i.Type.Equals("Door", StringComparison.OrdinalIgnoreCase)).ToList();
            if (interior.Select(i => $"{i.X}:{i.Y}").Distinct().Count() != interior.Count)
                modelState.AddModelError(nameof(vm.SeatsJson), "Two items cannot be in the same position.");


        }

        private async Task ValidateBusUniques(BusCreateEditMode vm, Guid? excludeBusId)
        {
            var q = _db.Buses.AsNoTracking().IgnoreQueryFilters();

            // BusNumber
            if (await q.AnyAsync(b => (excludeBusId == null || b.Id != excludeBusId.Value)
                                   && b.BusNumber == vm.BusNumber))
                ModelState.AddModelError(nameof(vm.BusNumber), "Bus Number already exists (including archived).");

            // ChassisNumber
            if (await q.AnyAsync(b => (excludeBusId == null || b.Id != excludeBusId.Value)
                                   && b.ChassisNumber == vm.ChassisNumber))
                ModelState.AddModelError(nameof(vm.ChassisNumber), "Chassis Number already exists (including archived).");

            // PlateNumber
            if (!string.IsNullOrWhiteSpace(vm.PlateNumber))
            {
                if (await q.AnyAsync(b => (excludeBusId == null || b.Id != excludeBusId.Value)
                                       && b.PlateNumber == vm.PlateNumber))
                    ModelState.AddModelError(nameof(vm.PlateNumber), "Plate Number already exists (including archived).");
            }
        }

        private async Task<bool> BusIsUsedInAnyTrip(Guid busId)
        {
            return await _db.Trips.AsNoTracking().AnyAsync(t => t.BusId == busId);
        }

        //---------------------------------------------------------------------------------------//
        ///////////////////////////////////////// DRIVERS /////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Drivers(string? q, bool includeArchived = true)
        {
            q = (q ?? "").Trim();

            var query = _db.Drivers
                .AsNoTracking()
                .Include(d => d.Phones)
                .AsQueryable();

            if (!includeArchived)
                query = query.Where(d => d.IsArchivedInt == 0);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(d =>
                    d.FullName.Contains(q) ||
                    d.NationalId.Contains(q) ||
                    (d.Address != null && d.Address.Contains(q)) ||
                    d.Phones.Any(p => p.PhoneNumber.Contains(q))
                );
            }

            var list = await query
                .OrderByDescending(d => d.Id)
                .Select(d => new DriverListItemModel
                {
                    Id = d.Id,
                    FullName = d.FullName,
                    NationalId = d.NationalId,
                    Address = d.Address,
                    IsActive = d.IsActiveInt == 1,
                    IsArchived = d.IsArchivedInt == 1,
                    PrimaryPhone = d.Phones
                        .OrderByDescending(p => p.IsPrimaryInt)
                        .ThenBy(p => p.Id)
                        .Select(p => p.PhoneNumber)
                        .FirstOrDefault() ?? ""
                })
                .ToListAsync();

            ViewBag.Q = q;
            ViewBag.IncludeArchived = includeArchived;

            return View(V_DriversList, list);
        }

        [HttpGet]
        public async Task<IActionResult> DriverDetails(Guid id)
        {
            var d = await _db.Drivers
                .AsNoTracking()
                .Include(x => x.Phones)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound();

            var tripsRaw = await _db.Trips
                .AsNoTracking()
                .Where(t => t.DriverId == id)
                .Select(t => new
                {
                    TripId = t.Id,
                    Title = (t.TripName ?? "Trip #" + t.Id),
                    t.DepartDate,
                    t.DepartTime
                })
                .ToListAsync();

            var nowCairo = CairoNow();

            var trips = new List<NoufirTours.Models.Trips.Drivers.DriverTripSummaryModel>();

            foreach (var t in tripsRaw)
            {
                DateTime? departAtCairo = null;

                if (TryParseTripDepartAt(t.DepartDate, t.DepartTime, out var parsedLocalCairo))
                {
                    // parsedLocalCairo kind Unspecified but represents Cairo local
                    departAtCairo = parsedLocalCairo;
                }

                bool isUpcoming = false;
                bool isPast = false;
                bool isToday = false;

                if (departAtCairo.HasValue)
                {
                    var dt = departAtCairo.Value;

                    isToday = dt.Date == nowCairo.Date;
                    isUpcoming = dt > nowCairo;
                    isPast = dt < nowCairo;
                }

                trips.Add(new NoufirTours.Models.Trips.Drivers.DriverTripSummaryModel
                {
                    TripId = t.TripId,
                    Title = t.Title,
                    DepartDate = t.DepartDate,
                    DepartTime = t.DepartTime,
                    DepartAtCairo = departAtCairo,
                    IsUpcoming = isUpcoming,
                    IsPast = isPast,
                    IsToday = isToday
                });
            }

            trips = trips
                .OrderByDescending(x => x.IsUpcoming)
                .ThenByDescending(x => x.IsToday)
                .ThenBy(x => x.DepartAtCairo ?? DateTime.MinValue)
                .ThenByDescending(x => x.TripId)
                .ToList();

            var vm = new DriverDetailsModel
            {
                Id = d.Id,
                FullName = d.FullName,
                NationalId = d.NationalId,
                Address = d.Address,
                LicenseNumber = d.LicenseNumber,

                // Cairo time conversion
                LicenseExpiryDate = UnixToCairoDate(d.LicenseExpiryAtUnix),
                JoinedAt = UnixToCairoDateTime(d.JoinedAtUnix) ?? CairoNow(),

                IsActive = d.IsActiveInt == 1,
                Notes = d.Notes,
                IsArchived = d.IsArchivedInt == 1,
                ArchivedAt = UnixToCairoDateTime(d.ArchivedAtUnix),

                Phones = d.Phones
                    .OrderByDescending(p => p.IsPrimaryInt)
                    .ThenBy(p => p.Id)
                    .Select(p => (p.PhoneNumber, p.IsPrimaryInt == 1))
                    .ToList(),

                Trips = trips
            };

            return View(V_DriverDetails, vm);
        }

        [HttpGet]
        public IActionResult CreateNewDriver()
        {
            var vm = new DriverCreateModel
            {
                JoinedAtDate = DateTime.UtcNow.Date,
                LicenseExpiryDate = DateTime.UtcNow.Date.AddYears(3),
                IsActive = true,
                Phones = new List<DriverPhoneInputModel>()
            };
            return View(V_CreateNewDriver, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNewDriver(DriverCreateModel vm)
        {
            vm.Phones ??= new List<DriverPhoneInputModel>();

            // Normalize
            vm.FullName = (vm.FullName ?? "").Trim();
            vm.NationalId = Regex.Replace((vm.NationalId ?? "").Trim(), @"\D", ""); // digits only
            vm.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();
            vm.LicenseNumber = string.IsNullOrWhiteSpace(vm.LicenseNumber) ? null : vm.LicenseNumber.Trim();
            vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            NormalizePhones(vm.Phones);

            // Validate (business + format)
            if (string.IsNullOrWhiteSpace(vm.FullName))
                ModelState.AddModelError(nameof(vm.FullName), "Full Name is required.");

            if (!Regex.IsMatch(vm.NationalId, @"^\d{14}$"))
                ModelState.AddModelError(nameof(vm.NationalId), "National ID must be exactly 14 digits.");

            if (!vm.Phones.Any())
                ModelState.AddModelError(nameof(vm.Phones), "At least one phone number is required.");

            // Egyptian phone: 01 + (0/1/2/5) + 8 digits
            var phoneRegex = new Regex(@"^01[0125][0-9]{8}$", RegexOptions.Compiled);

            for (int i = 0; i < vm.Phones.Count; i++)
            {
                var phone = (vm.Phones[i].PhoneNumber ?? "").Trim().Replace(" ", "");
                vm.Phones[i].PhoneNumber = phone;

                if (!phoneRegex.IsMatch(phone))
                    ModelState.AddModelError($"Phones[{i}].PhoneNumber", "Invalid phone number format.");
            }

            // Ensure exactly one primary
            if (vm.Phones.Count > 0 && vm.Phones.Count(p => p.IsPrimary) != 1)
                ModelState.AddModelError(nameof(vm.Phones), "Exactly one primary phone is required.");

            // Prevent duplicates inside form (extra safety)
            var dupInForm = vm.Phones
                .Where(p => !string.IsNullOrWhiteSpace(p.PhoneNumber))
                .GroupBy(p => p.PhoneNumber, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() > 1);

            if (dupInForm)
                ModelState.AddModelError(nameof(vm.Phones), "Duplicate phone numbers are not allowed.");

            // DB Uniques (include archived)
            await ValidateDriverUniquesForCreate(vm, excludeDriverId: null);

            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    Response.StatusCode = 422;

                return View(V_CreateNewDriver, vm);
            }

            // Build entity
            var driver = new Driver
            {
                FullName = vm.FullName,
                NationalId = vm.NationalId,
                Address = vm.Address,
                LicenseNumber = vm.LicenseNumber,
                Notes = vm.Notes,
                IsActiveInt = vm.IsActive ? 1 : 0,
                IsArchivedInt = 0
            };

            // Cairo local midnight -> UTC -> Unix
            var joinedLocal = DateTime.SpecifyKind(vm.JoinedAtDate.Date, DateTimeKind.Unspecified);
            var joinedUtc = TimeZoneInfo.ConvertTimeToUtc(joinedLocal, CairoTz);
            driver.JoinedAtUnix = new DateTimeOffset(joinedUtc).ToUnixTimeSeconds();

            if (vm.LicenseExpiryDate.HasValue)
            {
                var expLocal = DateTime.SpecifyKind(vm.LicenseExpiryDate.Value.Date, DateTimeKind.Unspecified);
                var expUtc = TimeZoneInfo.ConvertTimeToUtc(expLocal, CairoTz);
                driver.LicenseExpiryAtUnix = new DateTimeOffset(expUtc).ToUnixTimeSeconds();
            }
            else
            {
                driver.LicenseExpiryAtUnix = null;
            }

            foreach (var p in vm.Phones)
            {
                driver.Phones.Add(new DriverPhone
                {
                    PhoneNumber = p.PhoneNumber,
                    IsPrimaryInt = p.IsPrimary ? 1 : 0
                });
            }

            EnsureSinglePrimary(driver.Phones);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Drivers.Add(driver);
                await _db.SaveChangesAsync();

                await AuditAsync("create", "driver", driver.Id, new { driver.FullName, driver.NationalId, driver.IsActiveInt });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                TempData["Success"] = "Driver created successfully.";
                return RedirectToAction(nameof(DriverDetails), new { id = driver.Id });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();

                ModelState.AddModelError("", "Save failed. Please ensure National ID and phone numbers are unique (including archived).");
                return View(V_CreateNewDriver, vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditDriver(Guid id)
        {
            var d = await _db.Drivers
                .Include(x => x.Phones)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound();

            // BLOCK EDIT if Archived (Deleted)
            if (d.IsArchivedInt == 1)
            {
                TempData["Error"] = "This driver is deleted (Archived)";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }

            var usedInTrips = await DriverIsUsedInAnyTrip(id);

            var vm = new DriverEditModel
            {
                Id = d.Id,
                FullName = d.FullName,
                NationalId = d.NationalId,
                Address = d.Address,
                LicenseNumber = d.LicenseNumber,

                // Cairo dates
                LicenseExpiryDate = UnixToCairoDate(d.LicenseExpiryAtUnix),
                JoinedAtDate = UnixToCairoDate(d.JoinedAtUnix) ?? CairoNow().Date,

                IsActive = d.IsActiveInt == 1,
                Notes = d.Notes,

                IsArchived = d.IsArchivedInt == 1,
                ArchivedAt = UnixToCairoDateTime(d.ArchivedAtUnix),

                Phones = (d.Phones ?? new List<DriverPhone>())
                    .OrderByDescending(p => p.IsPrimaryInt)
                    .ThenBy(p => p.Id)
                    .Select(p => new DriverPhoneInputModel
                    {
                        Id = p.Id,
                        PhoneNumber = p.PhoneNumber,
                        IsPrimary = p.IsPrimaryInt == 1
                    })
                    .ToList(),

                LockCoreFields = usedInTrips
            };

            if (vm.Phones.Count == 0)
                vm.Phones.Add(new DriverPhoneInputModel { IsPrimary = true });

            if (vm.Phones.Count(p => p.IsPrimary) != 1 && vm.Phones.Count > 0)
            {
                foreach (var p in vm.Phones) p.IsPrimary = false;
                vm.Phones[0].IsPrimary = true;
            }

            return View(V_EditDriver, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDriver(Guid id, DriverEditModel vm)
        {
            if (id != vm.Id) return BadRequest();

            vm.Phones ??= new List<DriverPhoneInputModel>();

            // Load entity FIRST to validate status and use real DB values
            var d = await _db.Drivers
                .Include(x => x.Phones)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound();

            // BLOCK EDIT if Archived (Deleted)
            if (d.IsArchivedInt == 1)
            {
                TempData["Error"] = "This driver is deleted (Archived), so editing is not allowed.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }

            var usedInTrips = await DriverIsUsedInAnyTrip(id);
            vm.LockCoreFields = usedInTrips;

            // Normalize allowed inputs
            vm.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();
            vm.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();

            NormalizePhones(vm.Phones);

            // Validation allowed fields
            if (!vm.Phones.Any())
                ModelState.AddModelError("", "At least one phone number is required.");

            var phoneRegex = new Regex(@"^01[0125][0-9]{8}$", RegexOptions.Compiled);

            for (int i = 0; i < vm.Phones.Count; i++)
            {
                var phone = (vm.Phones[i].PhoneNumber ?? "").Trim().Replace(" ", "");
                vm.Phones[i].PhoneNumber = phone;

                if (!phoneRegex.IsMatch(phone))
                    ModelState.AddModelError($"Phones[{i}].PhoneNumber", "Invalid phone number format.");
            }

            if (vm.Phones.Count(p => p.IsPrimary) != 1)
                ModelState.AddModelError("", "Exactly one primary phone is required.");

            var dupInForm = vm.Phones
                .GroupBy(p => p.PhoneNumber, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() > 1);

            if (dupInForm)
                ModelState.AddModelError("", "Duplicate phone numbers are not allowed.");

            // Core fields rules
            if (usedInTrips)
            {
                // Force overwrite any posted core fields with DB values (extra protection)
                vm.FullName = d.FullName;
                vm.NationalId = d.NationalId;
                vm.LicenseNumber = d.LicenseNumber;
                vm.JoinedAtDate = UnixToCairoDate(d.JoinedAtUnix) ?? CairoNow().Date;
                vm.LicenseExpiryDate = UnixToCairoDate(d.LicenseExpiryAtUnix);
            }
            else
            {
                vm.FullName = (vm.FullName ?? "").Trim();
                vm.NationalId = Regex.Replace((vm.NationalId ?? "").Trim(), @"\D", "");

                if (string.IsNullOrWhiteSpace(vm.FullName))
                    ModelState.AddModelError(nameof(vm.FullName), "Full Name is required.");

                if (!Regex.IsMatch(vm.NationalId, @"^\d{14}$"))
                    ModelState.AddModelError(nameof(vm.NationalId), "National ID must be exactly 14 digits.");

                // National unique
                var nationalExists = await _db.Drivers.AnyAsync(x => x.NationalId == vm.NationalId && x.Id != id);
                if (nationalExists)
                    ModelState.AddModelError(nameof(vm.NationalId), "National ID already exists.");
            }

            // Phones uniqueness system-wide excluding this driver
            var newPhones = vm.Phones.Select(p => p.PhoneNumber).ToList();
            var conflicting = await _db.DriverPhones
                .AnyAsync(p => newPhones.Contains(p.PhoneNumber) && p.DriverId != id);

            if (conflicting)
                ModelState.AddModelError("", "One or more phone numbers already exist in the system.");

            if (!ModelState.IsValid)
                return View(V_EditDriver, vm);

            // APPLY UPDATES
            d.Address = vm.Address;
            d.Notes = vm.Notes;
            d.IsActiveInt = vm.IsActive ? 1 : 0;

            if (!usedInTrips)
            {
                d.FullName = vm.FullName;
                d.NationalId = vm.NationalId;
                d.LicenseNumber = string.IsNullOrWhiteSpace(vm.LicenseNumber) ? null : vm.LicenseNumber.Trim();

                // Save as UTC unix (treat selected date as Cairo local midnight)
                // Cairo local midnight -> UTC
                var joinedLocal = DateTime.SpecifyKind(vm.JoinedAtDate.Date, DateTimeKind.Unspecified);
                var joinedUtc = TimeZoneInfo.ConvertTimeToUtc(joinedLocal, CairoTz);
                d.JoinedAtUnix = new DateTimeOffset(joinedUtc).ToUnixTimeSeconds();

                if (vm.LicenseExpiryDate.HasValue)
                {
                    var expLocal = DateTime.SpecifyKind(vm.LicenseExpiryDate.Value.Date, DateTimeKind.Unspecified);
                    var expUtc = TimeZoneInfo.ConvertTimeToUtc(expLocal, CairoTz);
                    d.LicenseExpiryAtUnix = new DateTimeOffset(expUtc).ToUnixTimeSeconds();
                }
                else
                {
                    d.LicenseExpiryAtUnix = null;
                }
            }

            // Phones sync
            var incomingIds = vm.Phones.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();
            var toRemove = d.Phones.Where(p => !incomingIds.Contains(p.Id)).ToList();
            _db.DriverPhones.RemoveRange(toRemove);

            foreach (var p in vm.Phones)
            {
                if (p.Id.HasValue)
                {
                    var existing = d.Phones.First(x => x.Id == p.Id);
                    existing.PhoneNumber = p.PhoneNumber;
                    existing.IsPrimaryInt = p.IsPrimary ? 1 : 0;
                }
                else
                {
                    d.Phones.Add(new DriverPhone
                    {
                        PhoneNumber = p.PhoneNumber,
                        IsPrimaryInt = p.IsPrimary ? 1 : 0
                    });
                }
            }

            EnsureSinglePrimary(d.Phones);

            await AuditAsync("edit", "driver", d.Id, new { d.FullName, d.NationalId, d.IsActiveInt, usedInTrips });
            await _db.SaveChangesAsync();

            TempData["Success"] = usedInTrips
                ? "Driver updated (core fields locked because driver is used in trips)."
                : "Driver updated successfully.";

            return RedirectToAction(nameof(DriverDetails), new { id = d.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateDriver(Guid id)
        {
            var d = await _db.Drivers.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return NotFound();

            if (d.IsArchivedInt == 1)
            {
                TempData["Error"] = "This driver is deleted (archived). Restore it first.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }

            d.IsActiveInt = 1;

            await AuditAsync("activate", "driver", id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Driver set to Active.";
            return RedirectToAction(nameof(DriverDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateDriver(Guid id)
        {
            var d = await _db.Drivers.FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return NotFound();

            if (d.IsArchivedInt == 1)
            {
                TempData["Error"] = "This driver is deleted (archived). Restore it first.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }

            d.IsActiveInt = 0;
            await AuditAsync("deactivate", "driver", id);

            await _db.SaveChangesAsync();

            TempData["Success"] = "Driver set to Inactive.";
            return RedirectToAction(nameof(DriverDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDriver(Guid id)
        {
            // Load with phones (so hard delete removes related children safely)
            var d = await _db.Drivers
                .Include(x => x.Phones)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound();

            var hasAnyTrip = await _db.Trips.AsNoTracking().AnyAsync(t => t.DriverId == id);

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (hasAnyTrip)
                {
                    // Soft Delete = Archive
                    if (d.IsArchivedInt == 0)
                    {
                        d.IsArchivedInt = 1;
                        d.ArchivedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        d.IsActiveInt = 0; // always inactive when deleted

                        var uidStr = GetCurrentUserId();
                        d.ArchivedByUserId = uidStr;
                        await AuditAsync("archive", "driver", id, new { reason = "has_trips" });

                        await _db.SaveChangesAsync();
                        await tx.CommitAsync();

                        TempData["Success"] = "Driver has trips, so it was deleted as Archived and set to Inactive.";
                    }
                    else
                    {
                        // already archived
                        await tx.CommitAsync();
                        TempData["Success"] = "Driver is already deleted (Archived).";
                    }

                    return RedirectToAction(nameof(DriverDetails), new { id });
                }

                // No trips at all => Hard delete is safe
                // Remove phones first to avoid FK issues (if cascade not configured)
                if (d.Phones != null && d.Phones.Count > 0)
                    _db.DriverPhones.RemoveRange(d.Phones);

                _db.Drivers.Remove(d);
                await AuditAsync("delete", "driver", id);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Driver deleted permanently (no trips found).";
                return RedirectToAction(nameof(Drivers));
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();

                TempData["Error"] = "Delete failed due to database constraints. If this driver is linked anywhere, use Soft Delete instead.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Unexpected error occurred while deleting the driver.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreDriver(Guid id)
        {
            var b = await _db.Drivers.FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound();

            if (b.IsArchivedInt != 1)
            {
                TempData["Error"] = "Driver is not deleted.";
                return RedirectToAction(nameof(DriverDetails), new { id });
            }

            b.IsArchivedInt = 0;
            b.IsActiveInt = 1;
            b.ArchivedAtUnix = null;
            b.ArchivedByUserId = null;
            await AuditAsync("restore", "driver", id);

            await _db.SaveChangesAsync();

            TempData["Success"] = "Driver restored successfully.";
            return RedirectToAction(nameof(DriverDetails), new { id });
        }

        //--------------------------------------------------------------------------------------//
        /////////////////////////////// DRIVERS HELPER FUNCTIONS /////////////////////////////////
        //--------------------------------------------------------------------------------------//

        private static void NormalizePhones(List<DriverPhoneInputModel> phones)
        {
            // remove empty
            phones.RemoveAll(p => string.IsNullOrWhiteSpace(p.PhoneNumber));

            // trim + remove spaces
            foreach (var p in phones)
                p.PhoneNumber = (p.PhoneNumber ?? "").Trim().Replace(" ", "");

            // remove duplicates in form
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            phones.RemoveAll(p => !seen.Add(p.PhoneNumber));

            // ensure one primary
            if (phones.Count == 1)
                phones[0].IsPrimary = true;

            EnsureSinglePrimaryVm(phones);
        }

        private async Task<bool> DriverIsUsedInAnyTrip(Guid driverId)
        {
            return await _db.Trips.AsNoTracking().AnyAsync(t => t.DriverId == driverId);
        }

        private static void EnsureSinglePrimaryVm(List<DriverPhoneInputModel> phones)
        {
            if (phones.Count == 0) return;

            var prim = phones.Count(p => p.IsPrimary);
            if (prim == 1) return;

            // normalize: first is primary
            for (int i = 0; i < phones.Count; i++)
                phones[i].IsPrimary = (i == 0);
        }

        private async Task ValidateDriverUniquesForCreate(DriverCreateModel vm, Guid? excludeDriverId = null)
        {
            // IMPORTANT: include archived too
            var dq = _db.Drivers.AsNoTracking().IgnoreQueryFilters();
            var pq = _db.DriverPhones.AsNoTracking().IgnoreQueryFilters();

            // NationalId unique (including archived)
            if (!string.IsNullOrWhiteSpace(vm.NationalId))
            {
                var existsNational = await dq.AnyAsync(d =>
                    (excludeDriverId == null || d.Id != excludeDriverId.Value) &&
                    d.NationalId == vm.NationalId);

                if (existsNational)
                    ModelState.AddModelError(nameof(vm.NationalId), "National ID already exists (including archived).");
            }

            // Phones unique system-wide (including archived)
            var phones = (vm.Phones ?? new List<DriverPhoneInputModel>())
                .Select(p => (p.PhoneNumber ?? "").Trim().Replace(" ", ""))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (phones.Count > 0)
            {
                var existsPhone = await pq.AnyAsync(p => phones.Contains(p.PhoneNumber));
                if (existsPhone)
                    ModelState.AddModelError("", "One or more phone numbers already exist (including archived).");
            }
        }

        private static void EnsureSinglePrimary(ICollection<DriverPhone> phones)
        {
            if (phones == null || phones.Count == 0) return;

            var firstPrimary = phones.FirstOrDefault(p => p.IsPrimaryInt == 1);
            if (firstPrimary == null)
            {
                phones.First().IsPrimaryInt = 1;
                foreach (var p in phones.Skip(1))
                    p.IsPrimaryInt = 0;
                return;
            }

            var found = false;
            foreach (var p in phones)
            {
                if (p.IsPrimaryInt == 1)
                {
                    if (!found) found = true;
                    else p.IsPrimaryInt = 0;
                }
            }
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////////// ACCOUNTING ///////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Accounts(string? q, bool onlyStaffs = false, bool includeInactive = true)
        {
            q = (q ?? "").Trim();

            var usersQ = _db.Users
                .Where(u => u.UserID != GetCurrentUserId())
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
                usersQ = usersQ.Where(u => u.IsActiveInt == 1);

            if (onlyStaffs)
                usersQ = usersQ.Where(u => u.RoleText == "staff");

            if (!string.IsNullOrWhiteSpace(q))
            {
                usersQ = usersQ.Where(u =>
                    u.Username.Contains(q) ||
                    (u.FullName != null && u.FullName.Contains(q)) ||
                    (u.Phone != null && u.Phone.Contains(q))
                );
            }

            // base users
            var baseUsers = await usersQ
                .OrderByDescending(u => u.UserID)
                .Select(u => new { u.UserID, u.Username, u.FullName, u.Phone, u.RoleText, u.IsActiveInt })
                .ToListAsync();

            var userIds = baseUsers.Select(x => x.UserID).ToList();

            // last login from audit
            var lastLogins = await _db.Set<AuditLog>()
                .AsNoTracking()
                .Where(a => userIds.Contains(a.UserId) && a.Action == "login" && a.Entity == "user")
                .GroupBy(a => a.UserId)
                .Select(g => new { UserId = g.Key, LastUnix = g.Max(x => x.CreatedAtUnix) })
                .ToListAsync();

            var lastLoginMap = lastLogins.ToDictionary(x => x.UserId, x => x.LastUnix);

            DateTime? UnixToCairo(long? unix)
            {
                if (!unix.HasValue) return null;
                var utc = DateTimeOffset.FromUnixTimeSeconds(unix.Value).UtcDateTime;
                return TimeZoneInfo.ConvertTimeFromUtc(utc, CairoTz);
            }

            var dueRows = await _db.Bookings
                .AsNoTracking()
                .Where(b => userIds.Contains(b.CreatedByUserId!.Value) && b.IsCanceledInt == 0)
                .GroupBy(b => b.CreatedByUserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Due = g.Sum(x => (decimal?)(x.TotalAmount - x.PaidAmount)) ?? 0m
                })
                .ToListAsync();

            var dueMap = dueRows.ToDictionary(x => x.UserId, x => x.Due);

            var vm = new UsersListModel
            {
                Q = q,
                OnlyAdmins = onlyStaffs,
                IncludeInactive = includeInactive,
                Items = baseUsers.Select(u => new UserListItemModel
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    FullName = u.FullName,
                    Phone = u.Phone,
                    RoleText = u.RoleText,
                    IsActive = (u.IsActiveInt == 1),
                    LastLoginCairo = lastLoginMap.TryGetValue(u.UserID, out var lu) ? UnixToCairo(lu) : null,

                    TotalDue = dueMap.TryGetValue(u.UserID, out var due) ? due : 0m
                }).ToList()
            };

            return View(V_AdminUsersList, vm);
        }

        [HttpGet]
        public IActionResult UserCreate()
        {
            return View(V_AdminUserCreate, new UserCreateModel { RoleText = "staff", IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserCreate(UserCreateModel vm)
        {
            vm.Username = (vm.Username ?? "").Trim();
            vm.FullName = string.IsNullOrWhiteSpace(vm.FullName) ? null : vm.FullName.Trim();
            vm.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
            vm.RoleText = (vm.RoleText ?? "staff").Trim().ToLowerInvariant();

            if (vm.RoleText != "admin" && vm.RoleText != "staff" && vm.RoleText != "driver")
                ModelState.AddModelError(nameof(vm.RoleText), "Invalid role.");

            if (await _db.Users.AsNoTracking().AnyAsync(u => u.Username == vm.Username))
                ModelState.AddModelError(nameof(vm.Username), "Username already exists.");

            if (!ModelState.IsValid)
                return View(V_AdminUserCreate, vm);

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var user = new User
            {
                Username = vm.Username,
                FullName = vm.FullName,
                Phone = vm.Phone,
                RoleText = vm.RoleText,
                IsActiveInt = vm.IsActive ? 1 : 0,
                CreatedAtUnix = nowUnix,
                PasswordHash = _passwordHasher.HashData(vm.Password)
            };

            _db.Users.Add(user);

            await AuditAsync("create", "user", null, new
            {
                user.Username,
                user.RoleText,
                user.IsActiveInt
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "User created successfully.";
            return RedirectToAction(nameof(Accounts));
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(Guid id, DateTime? fromDate, DateTime? toDate, string? actionFilter)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
            if (u == null) return NotFound();

            long? fromUnix = null;
            long? toUnix = null;

            if (fromDate.HasValue) fromUnix = ToUnixStartCairo(fromDate.Value);
            if (toDate.HasValue) toUnix = ToUnixEndCairo(toDate.Value);

            // Audit query
            var auditQuery = _db.Set<AuditLog>()
                .AsNoTracking()
                .Where(a => a.UserId == id);

            if (fromUnix.HasValue)
                auditQuery = auditQuery.Where(a => a.CreatedAtUnix >= fromUnix.Value);

            if (toUnix.HasValue)
                auditQuery = auditQuery.Where(a => a.CreatedAtUnix <= toUnix.Value);

            if (!string.IsNullOrWhiteSpace(actionFilter))
                auditQuery = auditQuery.Where(a => a.Action == actionFilter);

            var recentAudit = await auditQuery
                .OrderByDescending(a => a.CreatedAtUnix)
                .Take(100)
                .ToListAsync();

            var lastLoginUnix = await _db.Set<AuditLog>()
                .AsNoTracking()
                .Where(a => a.UserId == id && a.Action == "login" && a.Entity == "user")
                .OrderByDescending(a => a.CreatedAtUnix)
                .Select(a => (long?)a.CreatedAtUnix)
                .FirstOrDefaultAsync();

            DateTime? lastLogin = lastLoginUnix.HasValue ? UnixToCairo(lastLoginUnix.Value) : null;

            var bookingsMoney = await _db.Bookings.AsNoTracking()
                .Where(b => b.CreatedByUserId == id)
                .Select(b => new { b.TotalAmount, b.PaidAmount, b.IsCanceledInt })
                .ToListAsync();

            var totalDue = bookingsMoney.Where(x => x.IsCanceledInt == 0).Sum(x => x.TotalAmount);
            var totalPaid = bookingsMoney.Where(x => x.IsCanceledInt == 0).Sum(x => x.PaidAmount);

            var totalAuditLogs = await _db.Set<AuditLog>().AsNoTracking().CountAsync(a => a.UserId == id);

            var tripsAsDriverUser = await _db.Trips.AsNoTracking().CountAsync(t => t.DriverUserId == id);
            var tripIds = await _db.Trips.AsNoTracking().Where(t => t.DriverUserId == id).Select(t => t.Id).ToListAsync();
            var bookingsOnThoseTrips = tripIds.Count == 0
                ? 0
                : await _db.Bookings.AsNoTracking().CountAsync(b => tripIds.Contains(b.TripId));

            var vm = new UserDetailsModel
            {
                UserID = u.UserID,
                Username = u.Username,
                FullName = u.FullName,
                Phone = u.Phone,
                RoleText = u.RoleText,
                LastLoginCairo = lastLogin,
                IsActive = u.IsActive,

                FromDate = fromDate,
                ToDate = toDate,
                ActionFilter = actionFilter,

                TotalDue = totalDue,
                TotalPaid = totalPaid,

                TotalAuditLogs = totalAuditLogs,
                TripsAsDriverUser = tripsAsDriverUser,
                BookingsOnThoseTrips = bookingsOnThoseTrips,

                RecentAudit = recentAudit
                    .Select(a => (UnixToCairo(a.CreatedAtUnix), a.Action, a.Entity, a.EntityId, a.Details))
                    .ToList()
            };

            return View(V_AdminUserDetails, vm);
        }

        [HttpGet]
        public async Task<IActionResult> UserPasswordModal(Guid id)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
            if (u == null) return NotFound();

            var vm = new AdminChangePasswordModel
            {
                UserId = u.UserID,
                Username = u.Username
            };

            return PartialView(V_UserPasswordModal, vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminChangePassword(AdminChangePasswordModel vm)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == vm.UserId);
            if (u == null)
            {
                ViewBag.Error = "User not found.";
                return RedirectToAction("UserDetails", new { id = vm.UserId });
            }

            vm.Username = u.Username;

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please fix validation errors and try again.";
                return RedirectToAction("UserDetails", new { id = vm.UserId });
            }

            if (vm.NewPassword != vm.ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return RedirectToAction("UserDetails", new { id = vm.UserId });
            }

            try
            {
                u.PasswordHash = _passwordHasher.HashData(vm.NewPassword);

                // Audit
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = GetCurrentUserId(),
                    Action = "change_password",
                    Entity = "users",
                    EntityId = u.UserID.ToString(),
                    Details = $"Admin reset password for user '{u.Username}'",
                    CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                await _db.SaveChangesAsync();

                ViewBag.Success = "Password updated successfully.";
                vm.NewPassword = "";
                vm.ConfirmPassword = "";
                return RedirectToAction("UserDetails", new { id = vm.UserId });
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to update password: " + ex.Message;
                return RedirectToAction("UserDetails", new { id = vm.UserId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBookingCollection(AddBookingCollectionInput input)
        {
            if (!ModelState.IsValid) return BadRequest();

            var me = GetCurrentUserId();

            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == input.BookingId);
            if (booking == null) return NotFound();

            if (booking.IsCanceledInt == 1)
            {
                TempData["Success"] = "Cannot collect on a canceled booking.";
                return RedirectToAction(nameof(UserDetails), new { id = booking.CreatedByUserId ?? Guid.Empty });
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var c = new BookingCollection
            {
                BookingId = booking.Id,
                Amount = input.Amount,
                Method = input.Method.Trim(),
                CollectedAtUnix = nowUnix,
                CollectedByUserId = me
            };

            _db.BookingCollections.Add(c);

            booking.PaidAmount += input.Amount;
            booking.StatusInt = booking.PaidAmount >= booking.TotalAmount ? 1 : 0;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Collection saved.";
            return RedirectToAction(nameof(UserDetails), new { id = booking.CreatedByUserId ?? Guid.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserToggleActive(Guid id)
        {
            var u = await _db.Users.FirstOrDefaultAsync(x => x.UserID == id);
            if (u == null) return NotFound();

            if (id == GetCurrentUserId())
                TempData["Error"] = "This User is System Administrator.";

            u.IsActiveInt = (u.IsActiveInt == 1) ? 0 : 1;

            await AuditAsync(u.IsActiveInt == 1 ? "activate" : "deactivate", "user", id, new
            {
                u.Username,
                u.IsActiveInt
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "User status updated.";
            return RedirectToAction(nameof(UserDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> UserFinanceModal(Guid id, string tab = "bookings", DateTime? bFrom = null, DateTime? bTo = null, string? bSearch = null, string? bStatus = null, DateTime? cFrom = null, DateTime? cTo = null, string? cMethod = null)
        {
            tab = string.IsNullOrWhiteSpace(tab) ? "bookings" : tab.Trim().ToLowerInvariant();
            if (tab != "bookings" && tab != "collections") tab = "bookings";

            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == id);
            if (u == null) return NotFound();

            long? bFromUnix = bFrom.HasValue ? ToUnixStartCairo(bFrom.Value) : null;
            long? bToUnix = bTo.HasValue ? ToUnixEndCairo(bTo.Value) : null;

            long? cFromUnix = cFrom.HasValue ? ToUnixStartCairo(cFrom.Value) : null;
            long? cToUnix = cTo.HasValue ? ToUnixEndCairo(cTo.Value) : null;

            var bookingsQ = _db.Bookings.AsNoTracking()
                .Where(b => b.CreatedByUserId == id);

            if (bFromUnix.HasValue) bookingsQ = bookingsQ.Where(b => b.CreatedAtUnix >= bFromUnix.Value);
            if (bToUnix.HasValue) bookingsQ = bookingsQ.Where(b => b.CreatedAtUnix <= bToUnix.Value);

            if (!string.IsNullOrWhiteSpace(bSearch))
            {
                var s = bSearch.Trim();
                bookingsQ = bookingsQ.Where(b =>
                    b.CustomerName.Contains(s) ||
                    b.Phone.Contains(s) ||
                    b.CompanyFrom.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(bStatus))
            {
                if (bStatus == "active") bookingsQ = bookingsQ.Where(b => b.IsCanceledInt == 0);
                else if (bStatus == "canceled") bookingsQ = bookingsQ.Where(b => b.IsCanceledInt == 1);
            }

            var bookings = await bookingsQ
                .OrderByDescending(b => b.CreatedAtUnix)
                .Take(300)
                .Select(b => new BookingRowModel
                {
                    Id = b.Id,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,
                    CairoTime = UnixToCairo(b.CreatedAtUnix),
                    TotalAmount = b.TotalAmount,
                    PaidAmount = b.PaidAmount,
                    IsCanceled = b.IsCanceledInt == 1
                })
                .ToListAsync();

            var totalDue = bookings.Where(x => !x.IsCanceled).Sum(x => x.TotalAmount);
            var totalPaid = bookings.Where(x => !x.IsCanceled).Sum(x => x.PaidAmount);

            var target = bookings
                .Where(x => !x.IsCanceled && x.PaidAmount < x.TotalAmount)
                .OrderByDescending(x => x.CairoTime)
                .FirstOrDefault();

            var collectionsQ = _db.BookingCollections
                .AsNoTracking()
                .Include(c => c.CollectedByUser)
                .Include(c => c.Booking)
                .Where(c => c.Booking.CreatedByUserId == id);

            if (cFromUnix.HasValue) collectionsQ = collectionsQ.Where(c => c.CollectedAtUnix >= cFromUnix.Value);
            if (cToUnix.HasValue) collectionsQ = collectionsQ.Where(c => c.CollectedAtUnix <= cToUnix.Value);

            if (!string.IsNullOrWhiteSpace(cMethod))
            {
                var s = cMethod.Trim();
                collectionsQ = collectionsQ.Where(c =>
                    c.Method.Contains(s) ||
                    c.CollectedByUser.Username.Contains(s) ||
                    c.Booking.CustomerName.Contains(s) ||
                    c.Booking.Phone.Contains(s) ||
                    c.Booking.CompanyFrom.Contains(s));
            }

            var collections = await collectionsQ
                .OrderByDescending(c => c.CollectedAtUnix)
                .Take(300)
                .Select(c => new CollectionRowModel
                {
                    Id = c.Id,
                    BookingId = c.BookingId,
                    Amount = c.Amount,
                    Method = c.Method,
                    CairoTime = UnixToCairo(c.CollectedAtUnix),
                    CreatedBy = c.CollectedByUser.Username
                })
                .ToListAsync();

            var vm = new UserFinanceModal
            {
                UserId = u.UserID,
                Username = u.Username,

                TotalDue = totalDue,
                TotalPaid = totalPaid,
                TotalCollected = collections.Sum(x => x.Amount),

                BFrom = bFrom,
                BTo = bTo,
                BSearch = bSearch,
                BStatus = bStatus,

                CFrom = cFrom,
                CTo = cTo,
                CMethod = cMethod,

                Bookings = bookings,
                Collections = collections,

                TargetBookingId = target?.Id,
                TargetBookingLabel = target == null ? null : $"{target.CustomerName} • {target.Phone}"
            };

            ViewData["FinanceTab"] = tab;

            return PartialView(V_UserFinanceModal, vm);
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////////// TECHNICAL ////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static long UtcNowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private async Task<TechnicalSupport> GetOrCreateSingletonAsync(CancellationToken ct)
        {
            var row = await _db.TechnicalSupports.FirstOrDefaultAsync(x => x.IsSingleton, ct);
            if (row != null) return row;

            row = new TechnicalSupport
            {
                CompanyPhone = "",
                ComplaintsPhone = "",
                UpdatedAtUnix = UtcNowUnix(),
                UpdatedByUserId = null,
                IsSingleton = true
            };

            _db.TechnicalSupports.Add(row);
            await _db.SaveChangesAsync(ct);
            return row;
        }

        [HttpGet]
        public async Task<IActionResult> TecSup(CancellationToken ct)
        {
            var row = await GetOrCreateSingletonAsync(ct);
            return View(V_SupportTech, row);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TecSup(string? companyPhone, string? complaintsPhone, CancellationToken ct)
        {
            var row = await GetOrCreateSingletonAsync(ct);

            row.CompanyPhone = (companyPhone ?? "").Trim();
            row.ComplaintsPhone = (complaintsPhone ?? "").Trim();
            row.UpdatedAtUnix = UtcNowUnix();

            var uid = GetCurrentUserId();
            row.UpdatedByUserId = (uid == Guid.Empty) ? null : uid;

            await _db.SaveChangesAsync(ct);

            TempData["Ok"] = "Saved successfully.";
            return RedirectToAction(nameof(TecSup));
        }
    }
}