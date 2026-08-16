using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using System.Text.RegularExpressions;

namespace NoufirTours.Services
{
    public interface IDailyWork
    {
        Task RunOnceAsync(CancellationToken ct = default);
        
        Task EnsureTripsForDateAsync(string dateIso, CancellationToken ct = default);

        Task CleanupFutureUnbookedTripsAsync(string todayIso, CancellationToken ct);

        // Activate inactive trips that match route for the searched date
        Task EnsureTripsForRouteAsync(string dateIso, string? from, string? to, CancellationToken ct = default);

        // Activate inactive trips that can satisfy minimum available seats (optionally within route)
        Task EnsureTripsForMinSeatsAsync(string dateIso, int minSeats, string? from, string? to, CancellationToken ct = default);

        Task<Guid?> EnsureBestReturnTripAsync(Guid mainTripId, string returnDateIso, int minSeats, CancellationToken ct = default);

        Task<List<Guid>> EnsureTripsForMinSeatsSmartAsync(string dateIso, int neededSeats, string? from, string? to, int[] allowedPriceTypes, bool isReturnLeg, CancellationToken ct = default);

        Task<List<Guid>> EnsureTripsForSeatPackingAsync(string dateIso, int neededSeats, string? from, string? to, int[] allowedPriceTypes, bool allowRoundBothDirections, CancellationToken ct = default);
    }

    public sealed class DailyWork : IDailyWork
    {
        private readonly DBContext _db;

        // Cairo timezone
        private static readonly TimeZoneInfo CairoTz = ResolveCairoTimeZone();
        private static readonly SemaphoreSlim _activateLock = new(1, 1);

        public DailyWork(DBContext db)
        {
            _db = db;
        }

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            var nowCairo = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CairoTz);
            var todayIso = nowCairo.Date.ToString("yyyy-MM-dd");
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Run enabled plans
            var plans = await _db.AutoTripPlans
                .Include(p => p.Items)
                .Where(p => p.IsEnabledInt == 1)
                .ToListAsync(ct);

            foreach (var plan in plans)
            {
                // Skip if already executed today (for Daily) OR done (for SpecificDate)
                if (!ShouldRunPlanToday(plan, todayIso, nowCairo))
                    continue;

                await EnsureTripsForPlanAsync(plan, todayIso, nowUnix, ct);

                // Mark completion
                plan.UpdatedAtUnix = nowUnix;

                if ((AutoPlanScheduleType)plan.ScheduleTypeInt != AutoPlanScheduleType.Daily)
                {
                    // SpecificDate -> permanently done after creating
                    plan.isDone = true;
                }

                await _db.SaveChangesAsync(ct);
            }

            // Cleanup past trips
            await CleanupPastTripsAsync(todayIso, nowUnix, ct);

            // Cleanup future trips
            await CleanupFutureUnbookedTripsAsync(todayIso, ct);
        }

        private async Task<bool> IsBusOrDriverBusyAsync(Guid? busId, Guid? driverId, string departDate, string departTime, CancellationToken ct)
        {
            if (!busId.HasValue || !driverId.HasValue)
                return true;

            departDate = (departDate ?? "").Trim();
            departTime = (departTime ?? "").Trim();

            if (string.IsNullOrWhiteSpace(departDate) || string.IsNullOrWhiteSpace(departTime))
                return true;

            return await _db.Trips.AnyAsync(t =>
                t.IsArchivedInt == 0 &&
                t.DepartDate == departDate &&
                t.DepartTime == departTime &&
                (
                    t.BusId == busId ||
                    t.DriverId == driverId
                )
            , ct);
        }

        private static bool ShouldRunPlanToday(AutoTripPlan plan, string todayIso, DateTimeOffset nowCairo)
        {
            var schedule = (AutoPlanScheduleType)plan.ScheduleTypeInt;

            if (schedule == AutoPlanScheduleType.Daily)
            {
                if (plan.UpdatedAtUnix.HasValue)
                {
                    var updated = DateTimeOffset.FromUnixTimeSeconds(plan.UpdatedAtUnix.Value);
                    var updatedCairo = TimeZoneInfo.ConvertTime(updated, CairoTz);
                    if (updatedCairo.Date.ToString("yyyy-MM-dd") == todayIso)
                        return false;
                }
                return true;
            }

            if (plan.isDone) return false;
            if (string.IsNullOrWhiteSpace(plan.SpecificDate)) return false;
            return string.Equals(plan.SpecificDate.Trim(), todayIso, StringComparison.Ordinal);
        }

        private async Task ApplyActivationModeOnlyForNewTripsAsync(AutoTripPlan plan, string departDate, List<Guid> newTripIds, CancellationToken ct)
        {
            if (newTripIds == null || newTripIds.Count == 0) return;

            var enabledItems = plan.Items
                .Where(i => i.IsEnabledInt == 1)
                .OrderBy(i => i.OrderNo)
                .ToList();

            if (enabledItems.Count == 0) return;

            var mode = (AutoPlanActivationMode)plan.ActivationModeInt;

            // Load ONLY the trips we just created + ensure they are AutoPlan
            var newTrips = await _db.Trips
                .Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.DepartDate == departDate &&
                    newTripIds.Contains(t.Id) &&
                    t.TripOriginInt == (int)TripOrigin.AutoPlan
                )
                .ToListAsync(ct);

            if (newTrips.Count == 0) return;

            // 1) ParallelAllActive => كل الجديد Active
            if (mode == AutoPlanActivationMode.ParallelAllActive)
            {
                foreach (var t in newTrips)
                    t.IsActiveInt = 1;

                await _db.SaveChangesAsync(ct);
                return;
            }

            // 2) Sequential* => Active واحد لكل (Type + Route)
            static string N(string? s) => (s ?? "").Trim();
            static bool Eq(string? a, string? b) => string.Equals(N(a), N(b), StringComparison.OrdinalIgnoreCase);

            static string RouteKey(Trip t)
            {
                var from = N(t.FromCity);
                var to = N(t.ToCity);

                if (t.PriceTypeInt == (int)TripPriceType.Round)
                {
                    if (string.Compare(from, to, StringComparison.OrdinalIgnoreCase) <= 0)
                        return $"{from}||{to}";
                    return $"{to}||{from}";
                }

                return $"{from}->{to}";
            }

            static string GroupKey(Trip t) => $"{t.PriceTypeInt}::{RouteKey(t)}";

            bool StrongMatch(Trip t, AutoTripPlanItem item)
            {
                var itemType = NormalizePriceType(item);

                return Eq(t.TripName, item.TripName)
                    && Eq(t.DepartTime, item.DepartTime)
                    && Eq(t.FromCity, item.FromCity)
                    && Eq(t.ToCity, item.ToCity)
                    && t.BusId == item.BusId
                    && t.DriverId == item.DriverId
                    && t.PriceTypeInt == itemType;
            }

            // اقفل كل الجديد مبدئياً
            foreach (var t in newTrips)
                t.IsActiveInt = 0;

            var groups = newTrips
                .GroupBy(GroupKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in groups)
            {
                ct.ThrowIfCancellationRequested();

                var tripsInGroup = g
                    .OrderBy(t => t.DepartTime)
                    .ThenBy(t => t.Id)
                    .ToList();

                if (tripsInGroup.Count == 0) continue;

                Trip? chosen = null;

                // حاول تختار Trip مطابق لأول item في الخطة داخل نفس المجموعة
                foreach (var item in enabledItems)
                {
                    chosen = tripsInGroup
                        .Where(t => StrongMatch(t, item))
                        .OrderBy(t => t.DepartTime)
                        .ThenBy(t => t.Id)
                        .FirstOrDefault();

                    if (chosen != null) break;
                }

                chosen ??= tripsInGroup.FirstOrDefault();

                if (chosen != null)
                    chosen.IsActiveInt = 1;
            }

            await _db.SaveChangesAsync(ct);
        }

        private async Task EnsureTripsForPlanOnDateAsync(AutoTripPlan plan, string departDate, long nowUnix, CancellationToken ct)
        {
            var enabledItems = plan.Items
                .Where(i => i.IsEnabledInt == 1)
                .OrderBy(i => i.OrderNo)
                .ToList();

            if (enabledItems.Count == 0) return;

            var newTripIds = new List<Guid>(capacity: enabledItems.Count);
            var mode = (AutoPlanActivationMode)plan.ActivationModeInt;

            await _activateLock.WaitAsync(ct);
            try
            {
                var used = await _db.Trips
                    .AsNoTracking()
                    .Where(t => t.IsArchivedInt == 0 && t.DepartDate == departDate)
                    .Select(t => new { t.BusId, t.DriverId })
                    .ToListAsync(ct);

                var usedBus = new HashSet<Guid>();
                var usedDriver = new HashSet<Guid>();

                foreach (var x in used)
                {
                    if (x.BusId.HasValue) usedBus.Add(x.BusId.Value);
                    if (x.DriverId.HasValue) usedDriver.Add(x.DriverId.Value);
                }

                foreach (var item in enabledItems)
                {
                    ct.ThrowIfCancellationRequested();

                    var priceType = NormalizePriceType(item);

                    if (item.BusId.HasValue && usedBus.Contains(item.BusId.Value))
                        continue;

                    if (item.DriverId.HasValue && usedDriver.Contains(item.DriverId.Value))
                        continue;

                    var exists = await _db.Trips.AnyAsync(t =>
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == departDate &&
                        t.DepartTime == item.DepartTime &&
                        t.TripName == item.TripName &&
                        t.FromCity == item.FromCity &&
                        t.ToCity == item.ToCity &&
                        t.BusId == item.BusId &&
                        t.DriverId == item.DriverId &&
                        t.PriceTypeInt == priceType &&
                        t.TripOriginInt == (int)TripOrigin.AutoPlan &&
                        t.AutoPlanId == plan.Id &&
                        t.AutoPlanItemId == item.Id
                    , ct);

                    if (exists) continue;

                    var busy = await IsBusOrDriverBusyAsync(item.BusId, item.DriverId, departDate, item.DepartTime, ct);
                    if (busy) continue;

                    var defaultActive = (mode == AutoPlanActivationMode.ParallelAllActive) ? 1 : 0;

                    var trip = new Trip
                    {
                        Id = Guid.NewGuid(),

                        TripName = item.TripName,
                        DepartDate = departDate,
                        DepartTime = item.DepartTime,

                        FromCity = item.FromCity,
                        ToCity = item.ToCity,

                        PickupPlace = item.PickupPlace,
                        PickupLat = item.PickupLat,
                        PickupLon = item.PickupLon,

                        DropoffPlace = item.DropoffPlace,
                        Notes = item.Notes,

                        SeatPriceGo = item.SeatPriceGo,
                        SeatPriceReturn = item.SeatPriceReturn,

                        PriceTypeInt = priceType,

                        BusId = item.BusId,
                        DriverId = item.DriverId,

                        IsArchivedInt = 0,
                        IsActiveInt = defaultActive,
                        CreatedAtUnix = nowUnix,

                        TripOriginInt = (int)TripOrigin.AutoPlan,
                        AutoPlanId = plan.Id,
                        AutoPlanItemId = item.Id
                    };

                    _db.Trips.Add(trip);
                    newTripIds.Add(trip.Id);

                    if (item.BusId.HasValue) usedBus.Add(item.BusId.Value);
                    if (item.DriverId.HasValue) usedDriver.Add(item.DriverId.Value);
                }

                if (newTripIds.Count == 0) return;

                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueTripBusDateViolation(ex))
                {
                    // ignore - race condition safe
                }

                // ✅ IMPORTANT: only keep ids that actually exist in DB
                var savedIds = await _db.Trips
                    .AsNoTracking()
                    .Where(t => newTripIds.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync(ct);

                if (savedIds.Count == 0) return;

                await ApplyActivationModeOnlyForNewTripsAsync(plan, departDate, savedIds, ct);
            }
            finally
            {
                _activateLock.Release();
            }
        }

        private static bool IsUniqueTripBusDateViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("ux_trips_depdate_busid", StringComparison.OrdinalIgnoreCase))
                return true;

            // SQL Server duplicate key codes: 2601, 2627
            if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
                return sqlEx.Number == 2601 || sqlEx.Number == 2627;

            return false;
        }

        private static int NormalizePriceType(AutoTripPlanItem item)
        {
            // لو النوع صحيح سيبه
            if (item.PriceTypeInt == (int)TripPriceType.Go ||
                item.PriceTypeInt == (int)TripPriceType.Return ||
                item.PriceTypeInt == (int)TripPriceType.Round)
                return item.PriceTypeInt;

            return (int)TripPriceType.Go;
        }

        public async Task EnsureTripsForDateAsync(string dateIso, CancellationToken ct = default)
        {
            dateIso = (dateIso ?? "").Trim();
            if (string.IsNullOrWhiteSpace(dateIso)) return;

            var nowCairo = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CairoTz);
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // enabled plans
            var plans = await _db.AutoTripPlans
                .Include(p => p.Items)
                .Where(p => p.IsEnabledInt == 1)
                .ToListAsync(ct);

            foreach (var plan in plans)
            {
                ct.ThrowIfCancellationRequested();

                var schedule = (AutoPlanScheduleType)plan.ScheduleTypeInt;

                // Daily plan: allow generating trips for ANY searched date (not only today)
                // SpecificDate: only if matches that date and not done
                if (schedule == AutoPlanScheduleType.Daily)
                {
                    await EnsureTripsForPlanOnDateAsync(plan, dateIso, nowUnix, ct);
                }
                else
                {
                    if (plan.isDone) continue;
                    if (string.IsNullOrWhiteSpace(plan.SpecificDate)) continue;

                    if (string.Equals(plan.SpecificDate.Trim(), dateIso, StringComparison.Ordinal))
                        await EnsureTripsForPlanOnDateAsync(plan, dateIso, nowUnix, ct);
                }
            }
        }

        // Organize All Daily Trips for the Plan by Activation Mode
        private async Task EnsureTripsForPlanAsync(AutoTripPlan plan, string todayIso, long nowUnix, CancellationToken ct)
        {
            var departDate =
                ((AutoPlanScheduleType)plan.ScheduleTypeInt == AutoPlanScheduleType.Daily)
                    ? todayIso
                    : (plan.SpecificDate ?? todayIso);

            await EnsureTripsForPlanOnDateAsync(plan, departDate, nowUnix, ct);
        }

        // Route activation => Activate ONE best inactive trip only
        public async Task EnsureTripsForRouteAsync(string dateIso, string? from, string? to, CancellationToken ct = default)
        {
            dateIso = (dateIso ?? "").Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();

            if (string.IsNullOrWhiteSpace(dateIso)) return;
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)) return;

            await _activateLock.WaitAsync(ct);
            try
            {
                var activeQ = _db.Trips.AsNoTracking().Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.DepartDate == dateIso &&
                    t.IsActiveInt == 1
                );

                if (!string.IsNullOrWhiteSpace(from))
                    activeQ = activeQ.Where(t => EF.Functions.Like(t.FromCity ?? "", $"%{from}%"));

                if (!string.IsNullOrWhiteSpace(to))
                    activeQ = activeQ.Where(t => EF.Functions.Like(t.ToCity ?? "", $"%{to}%"));

                if (await activeQ.AnyAsync(ct)) return;

                var inactiveQ = _db.Trips.Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.DepartDate == dateIso &&
                    t.IsActiveInt == 0 &&
                    t.TripOriginInt == (int)TripOrigin.AutoPlan
                );

                if (!string.IsNullOrWhiteSpace(from))
                    inactiveQ = inactiveQ.Where(t => EF.Functions.Like(t.FromCity ?? "", $"%{from}%"));

                if (!string.IsNullOrWhiteSpace(to))
                    inactiveQ = inactiveQ.Where(t => EF.Functions.Like(t.ToCity ?? "", $"%{to}%"));

                var one = await inactiveQ
                    .OrderBy(t => t.DepartTime)
                    .ThenBy(t => t.Id)
                    .FirstOrDefaultAsync(ct);

                if (one == null) return;

                one.IsActiveInt = 1;
                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _activateLock.Release();
            }
        }

        public async Task EnsureTripsForMinSeatsAsync(string dateIso, int minSeats, string? from, string? to, CancellationToken ct = default)
        {
            dateIso = (dateIso ?? "").Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();

            if (string.IsNullOrWhiteSpace(dateIso)) return;
            if (minSeats <= 0) return;

            await _activateLock.WaitAsync(ct);
            try
            {
                // get candidates (active OR inactive) with total seats
                async Task<List<(Guid Id, string? DepartTime, int IsActive, int SeatsTotal)>> LoadTripsAsync(int isActive)
                {
                    var q = _db.Trips.AsNoTracking()
                        .Include(t => t.Bus)
                        .Where(t =>
                            t.IsArchivedInt == 0 &&
                            t.DepartDate == dateIso &&
                            t.IsActiveInt == isActive
                        );

                    if (isActive == 0)
                    {
                        q = q.Where(t => t.TripOriginInt == (int)TripOrigin.AutoPlan);
                    }

                    if (!string.IsNullOrWhiteSpace(from))
                        q = q.Where(t => (t.FromCity ?? "").Contains(from));

                    if (!string.IsNullOrWhiteSpace(to))
                        q = q.Where(t => (t.ToCity ?? "").Contains(to));

                    var list = await q.Select(t => new
                    {
                        t.Id,
                        t.DepartTime,
                        IsActive = t.IsActiveInt,
                        SeatsTotal = (t.Bus != null && t.Bus.SeatsCount.HasValue) ? t.Bus.SeatsCount.Value : 0
                    }).ToListAsync(ct);

                    return list.Select(x => (x.Id, x.DepartTime, x.IsActive, x.SeatsTotal)).ToList()!;
                }

                var activeTrips = await LoadTripsAsync(isActive: 1);
                if (activeTrips.Count > 0)
                {
                    var activeIds = activeTrips.Select(x => x.Id).ToList();

                    var activeBookings = await _db.Bookings.AsNoTracking()
                        .Where(b => activeIds.Contains(b.TripId) && b.IsCanceledInt == 0)
                        .Select(b => new { b.TripId, b.SeatsText })
                        .ToListAsync(ct);

                    var bookedMap = activeBookings
                        .GroupBy(x => x.TripId)
                        .ToDictionary(g => g.Key, g =>
                        {
                            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var b in g)
                                foreach (var s in ParseSeatsCsv(b.SeatsText))
                                    set.Add(s);
                            return set.Count;
                        });

                    var hasActiveEnough = activeTrips.Any(t =>
                    {
                        var booked = bookedMap.TryGetValue(t.Id, out var cnt) ? cnt : 0;
                        var avail = Math.Max(0, t.SeatsTotal - booked);
                        return avail >= minSeats;
                    });

                    if (hasActiveEnough) return;
                }

                var inactiveTrips = await LoadTripsAsync(isActive: 0);
                if (inactiveTrips.Count == 0) return;

                var inactiveIds = inactiveTrips.Select(x => x.Id).ToList();

                var bookings = await _db.Bookings.AsNoTracking()
                    .Where(b => inactiveIds.Contains(b.TripId) && b.IsCanceledInt == 0)
                    .Select(b => new { b.TripId, b.SeatsText })
                    .ToListAsync(ct);

                var bookedInactiveMap = bookings
                    .GroupBy(x => x.TripId)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var b in g)
                            foreach (var s in ParseSeatsCsv(b.SeatsText))
                                set.Add(s);
                        return set.Count;
                    });

                var best = inactiveTrips
                    .Select(t =>
                    {
                        var booked = bookedInactiveMap.TryGetValue(t.Id, out var cnt) ? cnt : 0;
                        var avail = Math.Max(0, t.SeatsTotal - booked);
                        return new { t.Id, t.DepartTime, Avail = avail };
                    })
                    .Where(x => x.Avail >= minSeats)
                    .OrderBy(x => x.DepartTime)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();

                if (best == null) return;

                // tracked activate one
                var one = await _db.Trips
                    .Where(t =>
                        t.Id == best.Id &&
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == dateIso &&
                        t.IsActiveInt == 0 &&
                        t.TripOriginInt == (int)TripOrigin.AutoPlan
                    )
                    .FirstOrDefaultAsync(ct);

                if (one == null) return;

                one.IsActiveInt = 1;
                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _activateLock.Release();
            }
        }

        private static IEnumerable<string> ParseSeatsCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) yield break;

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var s = part.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    yield return s;
            }
        }
 
        public async Task<Guid?> EnsureBestReturnTripAsync(Guid mainTripId, string returnDateIso, int minSeats, CancellationToken ct = default)
        {
            returnDateIso = (returnDateIso ?? "").Trim();
            if (string.IsNullOrWhiteSpace(returnDateIso)) return null;
            if (minSeats <= 0) minSeats = 1;

            var main = await _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus)
                .FirstOrDefaultAsync(t => t.Id == mainTripId && t.IsArchivedInt == 0, ct);

            if (main == null) return null;

            // reverse route exact
            var from = (main.ToCity ?? "").Trim();
            var to = (main.FromCity ?? "").Trim();
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return null;

            // Ensure trips exist for date (from enabled plans) - doesn't activate random
            await EnsureTripsForDateAsync(returnDateIso, ct);

            // Ensure route has at least one active trip (but we will activate best ourselves)
            //    We won't call EnsureTripsForRouteAsync blindly, because it may activate first by time without minSeats check.
            //    We'll do best-selection below.

            // Get all candidates (active + inactive) for that reverse route
            var candidates = await _db.Trips
                .Include(t => t.Bus)
                .Where(t =>
                    t.IsArchivedInt == 0 &&
                    t.IsActiveInt >= 0 &&
                    t.DepartDate == returnDateIso &&
                    (t.FromCity ?? "") == from &&
                    (t.ToCity ?? "") == to
                )
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return null;

            // compute availability for each trip correctly (counts bookings on TripId OR ReturnTripId)
            var availMap = await ComputeAvailabilityMapAsync(candidates.Select(x => x.Id).ToList(), ct);

            // prefer already-active and enough seats
            var bestActive = candidates
                .Where(t => t.IsActiveInt == 1)
                .Select(t => new { Trip = t, Avail = availMap.TryGetValue(t.Id, out var a) ? a : 0 })
                .Where(x => x.Avail >= minSeats)
                .OrderBy(x => x.Trip.DepartTime)
                .ThenBy(x => x.Trip.Id)
                .FirstOrDefault();

            if (bestActive != null)
                return bestActive.Trip.Id;

            // otherwise: pick best inactive enough seats then activate that ONE
            var bestInactive = candidates
                .Where(t => t.IsActiveInt == 0 && t.TripOriginInt == (int)TripOrigin.AutoPlan)
                .Select(t => new { Trip = t, Avail = availMap.TryGetValue(t.Id, out var a) ? a : 0 })
                .Where(x => x.Avail >= minSeats)
                .OrderBy(x => x.Trip.DepartTime)
                .ThenBy(x => x.Trip.Id)
                .FirstOrDefault();

            if (bestInactive == null)
                return null;

            // activate only this one (tracked entity) BUT only if AutoPlan
            var tracked = await _db.Trips.FirstOrDefaultAsync(t =>
                t.Id == bestInactive.Trip.Id &&
                t.TripOriginInt == (int)TripOrigin.AutoPlan &&
                t.IsArchivedInt == 0
            , ct);

            if (tracked == null) return null;

            tracked.IsActiveInt = 1;
            await _db.SaveChangesAsync(ct);
            return tracked.Id;
        }

        // counts booked seats for trip considering BOTH sides of round bookings
        private async Task<Dictionary<Guid, int>> ComputeAvailabilityMapAsync(List<Guid> tripIds, CancellationToken ct)
        {
            if (tripIds == null || tripIds.Count == 0) return new Dictionary<Guid, int>();

            // load seat totals
            var totals = await _db.Trips
                .AsNoTracking()
                .Include(t => t.Bus)
                .Where(t => tripIds.Contains(t.Id))
                .Select(t => new
                {
                    t.Id,
                    SeatsTotal = (t.Bus != null && t.Bus.SeatsCount.HasValue) ? t.Bus.SeatsCount.Value : 0
                })
                .ToListAsync(ct);

            var totalsMap = totals.ToDictionary(x => x.Id, x => x.SeatsTotal);

            // load bookings affecting these tripIds (TripId or ReturnTripId)
            var bookings = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.IsCanceledInt == 0 &&
                            (tripIds.Contains(b.TripId) ||
                             (b.ReturnTripId.HasValue && tripIds.Contains(b.ReturnTripId.Value))))
                .Select(b => new { b.TripId, b.ReturnTripId, b.SeatsText, b.SeatsReturnText })
                .ToListAsync(ct);

            var bookedSetMap = new Dictionary<Guid, HashSet<string>>();

            void AddSeat(Guid tid, string? csv)
            {
                if (!bookedSetMap.TryGetValue(tid, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bookedSetMap[tid] = set;
                }

                if (string.IsNullOrWhiteSpace(csv)) return;

                foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var s = (part ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(s)) set.Add(s);
                }
            }

            foreach (var b in bookings)
            {
                if (tripIds.Contains(b.TripId))
                    AddSeat(b.TripId, b.SeatsText);

                if (b.ReturnTripId.HasValue && tripIds.Contains(b.ReturnTripId.Value))
                    AddSeat(b.ReturnTripId.Value, b.SeatsReturnText);
            }

            var result = new Dictionary<Guid, int>();
            foreach (var tid in tripIds)
            {
                var total = totalsMap.TryGetValue(tid, out var t) ? t : 0;
                var booked = bookedSetMap.TryGetValue(tid, out var set) ? set.Count : 0;
                var avail = Math.Max(0, total - booked);
                result[tid] = avail;
            }

            return result;
        }

        public async Task<List<Guid>> EnsureTripsForMinSeatsSmartAsync(string dateIso, int neededSeats, string? from, string? to, int[] allowedPriceTypes, bool isReturnLeg, CancellationToken ct = default)
        {
            dateIso = (dateIso ?? "").Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();

            if (string.IsNullOrWhiteSpace(dateIso)) return new List<Guid>();
            if (neededSeats <= 0) neededSeats = 1;

            allowedPriceTypes ??= Array.Empty<int>();

            await _activateLock.WaitAsync(ct);
            try
            {
                await EnsureTripsForDateAsync(dateIso, ct);

                var q = _db.Trips
                    .AsNoTracking()
                    .Include(t => t.Bus)
                    .Where(t =>
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == dateIso &&
                        t.IsActiveInt >= 0
                    );

                if (allowedPriceTypes.Length > 0)
                    q = q.Where(t => allowedPriceTypes.Contains(t.PriceTypeInt));

                if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
                {
                    var f = from ?? "";
                    var tt = to ?? "";

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

                var candidates = await q.ToListAsync(ct);
                if (candidates.Count == 0) return new List<Guid>();

                var ids = candidates.Select(x => x.Id).ToList();
                var availMap = await ComputeAvailabilityMapAsync(ids, ct);

                var rows = candidates.Select(t =>
                {
                    var avail = availMap.TryGetValue(t.Id, out var a) ? a : 0;
                    return new
                    {
                        TripId = t.Id,
                        t.DepartTime,
                        IsActive = (t.IsActiveInt == 1),
                        Avail = avail,
                        Origin = t.TripOriginInt
                    };
                }).ToList();

                // Active includes manual+auto (دي خدمة موجودة للعميل)
                var activeRows = rows.Where(x => x.IsActive && x.Avail > 0).ToList();
                var activeSum = activeRows.Sum(x => x.Avail);

                if (activeSum >= neededSeats)
                {
                    return activeRows
                        .OrderBy(x => x.Avail)
                        .ThenBy(x => x.DepartTime)
                        .ThenBy(x => x.TripId)
                        .Select(x => x.TripId)
                        .ToList();
                }

                // ✅ activate ONLY AutoPlan inactive
                var inactiveRows = rows
                    .Where(x => !x.IsActive && x.Avail > 0 && x.Origin == (int)TripOrigin.AutoPlan)
                    .OrderBy(x => x.Avail)
                    .ThenBy(x => x.DepartTime)
                    .ThenBy(x => x.TripId)
                    .ToList();

                if (inactiveRows.Count == 0)
                    return new List<Guid>();

                var toActivate = new List<Guid>();
                var sum = activeSum;

                foreach (var r in inactiveRows)
                {
                    if (sum >= neededSeats) break;
                    toActivate.Add(r.TripId);
                    sum += r.Avail;
                }

                if (toActivate.Count > 0)
                {
                    var tracked = await _db.Trips
                        .Where(t =>
                            toActivate.Contains(t.Id) &&
                            t.IsArchivedInt == 0 &&
                            t.DepartDate == dateIso &&
                            t.IsActiveInt == 0 &&
                            t.TripOriginInt == (int)TripOrigin.AutoPlan
                        )
                        .ToListAsync(ct);

                    foreach (var t in tracked)
                        t.IsActiveInt = 1;

                    await _db.SaveChangesAsync(ct);
                }

                var orderedActive = activeRows
                    .OrderBy(x => x.Avail)
                    .ThenBy(x => x.DepartTime)
                    .ThenBy(x => x.TripId)
                    .Select(x => x.TripId)
                    .ToList();

                orderedActive.AddRange(toActivate);
                return orderedActive;
            }
            finally
            {
                _activateLock.Release();
            }
        }

        public async Task<List<Guid>> EnsureTripsForSeatPackingAsync(string dateIso, int neededSeats, string? from, string? to, int[] allowedPriceTypes, bool allowRoundBothDirections, CancellationToken ct = default)
        {
            dateIso = (dateIso ?? "").Trim();
            from = (from ?? "").Trim();
            to = (to ?? "").Trim();

            if (string.IsNullOrWhiteSpace(dateIso)) return new List<Guid>();
            if (neededSeats <= 0) neededSeats = 1;

            allowedPriceTypes ??= Array.Empty<int>();

            await _activateLock.WaitAsync(ct);
            try
            {
                await EnsureTripsForDateAsync(dateIso, ct);

                var q = _db.Trips
                    .AsNoTracking()
                    .Include(t => t.Bus)
                    .Where(t =>
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == dateIso &&
                        t.IsActiveInt >= 0
                    );

                if (allowedPriceTypes.Length > 0)
                    q = q.Where(t => allowedPriceTypes.Contains(t.PriceTypeInt));

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

                var candidates = await q.ToListAsync(ct);
                if (candidates.Count == 0) return new List<Guid>();

                var ids = candidates.Select(x => x.Id).ToList();
                var availMap = await ComputeAvailabilityMapAsync(ids, ct);

                var rows = candidates
                    .Select(t => new
                    {
                        TripId = t.Id,
                        IsActive = (t.IsActiveInt == 1),
                        DepartTime = t.DepartTime ?? "",
                        Avail = availMap.TryGetValue(t.Id, out var a) ? a : 0,
                        Origin = t.TripOriginInt
                    })
                    .Where(x => x.Avail > 0)
                    .ToList();

                if (rows.Count == 0) return new List<Guid>();

                var single = rows
                    .Where(x => x.Avail >= neededSeats)
                    .OrderBy(x => x.Avail)
                    .ThenBy(x => x.DepartTime)
                    .ThenBy(x => x.TripId)
                    .FirstOrDefault();

                List<Guid> chosen;

                if (single != null)
                {
                    chosen = new List<Guid> { single.TripId };
                }
                else
                {
                    var ordered = rows
                        .OrderBy(x => x.Avail)
                        .ThenBy(x => x.DepartTime)
                        .ThenBy(x => x.TripId)
                        .ToList();

                    chosen = new List<Guid>();
                    var sum = 0;

                    foreach (var r in ordered)
                    {
                        if (sum >= neededSeats) break;
                        chosen.Add(r.TripId);
                        sum += r.Avail;
                    }

                    if (sum < neededSeats)
                        return chosen;
                }

                // ✅ Activate ONLY AutoPlan inactive among chosen
                var tracked = await _db.Trips
                    .Where(t =>
                        chosen.Contains(t.Id) &&
                        t.IsArchivedInt == 0 &&
                        t.DepartDate == dateIso &&
                        t.IsActiveInt == 0 &&
                        t.TripOriginInt == (int)TripOrigin.AutoPlan
                    )
                    .ToListAsync(ct);

                foreach (var t in tracked)
                    t.IsActiveInt = 1;

                if (tracked.Count > 0)
                    await _db.SaveChangesAsync(ct);

                return chosen;
            }
            finally
            {
                _activateLock.Release();
            }
        }

        private async Task<bool> CanDeleteTripAsync(Guid tripId, CancellationToken ct)
        {
            var placeIds = await _db.Set<TripPlace>()
                .AsNoTracking()
                .Where(p => p.TripId == tripId)
                .Select(p => p.Id)
                .ToListAsync(ct);

            var hasAnyReference = await _db.Bookings
                .AsNoTracking()
                .AnyAsync(b =>
                    b.TripId == tripId ||
                    (b.ReturnTripId.HasValue && b.ReturnTripId.Value == tripId) ||

                    // destination place refs
                    (b.DestinationPlaceId.HasValue && placeIds.Contains(b.DestinationPlaceId.Value)) ||
                    (b.ReturnDestinationPlaceId.HasValue && placeIds.Contains(b.ReturnDestinationPlaceId.Value))

                , ct);

            return !hasAnyReference;
        }

        // Clean and Archive All past trips before today
        private async Task CleanupPastTripsAsync(string todayIso, long nowUnix, CancellationToken ct)
        {
            var pastTrips = await _db.Trips
                .Where(t => string.Compare(t.DepartDate, todayIso) < 0)
                .ToListAsync(ct);

            if (pastTrips.Count == 0) return;

            foreach (var trip in pastTrips)
            {
                ct.ThrowIfCancellationRequested();

                var canDelete = await CanDeleteTripAsync(trip.Id, ct);

                if (canDelete)
                {
                    var places = await _db.Set<TripPlace>()
                        .Where(p => p.TripId == trip.Id)
                        .ToListAsync(ct);

                    if (places.Count > 0)
                        _db.Set<TripPlace>().RemoveRange(places);

                    _db.Trips.Remove(trip);
                }
                else
                {
                    trip.IsArchivedInt = 1;
                    trip.IsActiveInt = 0;
                    trip.ArchivedAtUnix = nowUnix;
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        // Clean and Archive All Future trips before today
        public async Task CleanupFutureUnbookedTripsAsync(string todayIso, CancellationToken ct)
        {
            var futureTrips = await _db.Trips
                .Where(t =>
                    string.Compare(t.DepartDate, todayIso) > 0 &&
                    t.TripOriginInt == (int)TripOrigin.AutoPlan)
                .ToListAsync(ct);

            if (futureTrips.Count == 0) return;

            var tripsToDelete = new List<Trip>();

            foreach (var trip in futureTrips)
            {
                ct.ThrowIfCancellationRequested();

                var canDelete = await CanDeleteTripAsync(trip.Id, ct);

                if (canDelete)
                {
                    tripsToDelete.Add(trip);
                }
            }

            if (tripsToDelete.Count == 0) return;

            var tripIds = tripsToDelete.Select(t => t.Id).ToList();

            var places = await _db.Set<TripPlace>()
                .Where(p => tripIds.Contains(p.TripId))
                .ToListAsync(ct);

            if (places.Count > 0)
                _db.Set<TripPlace>().RemoveRange(places);

            _db.Trips.RemoveRange(tripsToDelete);

            await _db.SaveChangesAsync(ct);
        }

        // Get Time ZoneInfo with fallback for Cairo
        private static TimeZoneInfo ResolveCairoTimeZone()
        {
            // Linux usually supports "Africa/Cairo"
            // Windows often uses "Egypt Standard Time"
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            }
        }
    }
}