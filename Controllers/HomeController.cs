using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using NoufirTours.Models.Home;
using NoufirTours.Models.Home.Settings;
using NoufirTours.Models.Trips.Accounts;
using NoufirTours.Services;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CollectionRowModel = NoufirTours.Models.Home.Settings.CollectionRowModel;

namespace NoufirTours.Controllers;

public class HomeController : Controller
{

    //---------------------------------------------------------------------------------------//
    //////////////////////////////////////// FIELDS //////////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private readonly DBContext _db;
    private readonly ILogger<HomeController> _logger;
    private readonly DataHasher _dataHasher;
    private readonly IDataCiphers _dataProtection;


    // Cached per-request (no repeat in every action)
    private User? _currentUser;
    private Guid _currentUserId;
    private bool _isLoggedIn;

    private static readonly HashSet<string> _showVerifyAlertActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Index"
    };

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// CONSTRUCTOR ///////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    public HomeController(DBContext db, ILogger<HomeController> logger, DataHasher DataHasher, IDataCiphers dataProtection)
    {
        _db = db;
        _logger = logger;
        _dataHasher = DataHasher;
        _dataProtection = dataProtection;
    }

    // Session login state + Layout ViewBags + verify banner + blocked check
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a : "";
            var showVerifyAlert = _showVerifyAlertActions.Contains(actionName!);

            var (ok, userId, user) = await ApplySessionLoginStateAsync(showVerifyAlert);

            _isLoggedIn = ok;
            _currentUserId = userId;
            _currentUser = user;

            var endpoint = context.HttpContext.GetEndpoint();
            var requiresAuth = endpoint?.Metadata?.GetMetadata<AuthorizeAttribute>() != null;

            if (requiresAuth && (!ok || user == null))
            {
                context.Result = RedirectToAction("Login", "Auth");
                return;
            }

            if (requiresAuth && user != null && !user.IsActive)
            {
                TempData["ErrorMessage"] = "Your account is blocked. Contact support.";
                context.Result = RedirectToAction(nameof(Index));
                return;
            }

            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HomeController OnActionExecutionAsync failed.");

            TempData["ErrorMessage"] = "Something went wrong. Please try again.";
            context.Result = RedirectToAction(nameof(Index));
        }
    }

    //---------------------------------------------------------------------------------------//
    //////////////////////////////////////// Time ZONE ///////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private static readonly TimeZoneInfo CairoTz = GetCairoTimeZone();

    private static long ToUnixStartOfDayCairo(DateTime dayCairo)
    {
        var unspecified = DateTime.SpecifyKind(dayCairo.Date, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, CairoTz);
        return new DateTimeOffset(utc).ToUnixTimeSeconds();
    }

    private static long ToUnixEndOfDayCairo(DateTime dayCairo)
    {
        var end = dayCairo.Date.AddDays(1).AddSeconds(-1);
        var unspecified = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, CairoTz);
        return new DateTimeOffset(utc).ToUnixTimeSeconds();
    }

    private static string CairoFromUnix(long unix)
    {
        // Display only (no DB writes)
        var cairoTz = GetCairoTimeZone();
        var dtUtc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        var cairo = TimeZoneInfo.ConvertTimeFromUtc(dtUtc, cairoTz);
        return cairo.ToString("yyyy-MM-dd HH:mm");
    }

    private static TimeZoneInfo GetCairoTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
    }

    //---------------------------------------------------------------------------------------//
    //////////////////////////////// SESSION + VIEWBAG HELPERS ////////////////////////////////
    //---------------------------------------------------------------------------------------//

    // sets ViewBag.UserLoggedIn, ViewBag.Email, ViewBag.type, ViewBag.IsVerified, ViewBag.NeedsVerification
    private async Task<(bool ok, Guid userId, User? user)> ApplySessionLoginStateAsync(bool showVerifyAlert = false)
    {
        var userIdStr = HttpContext.Session.GetString("UserID");
        var userIdSession = Guid.TryParse(userIdStr, out var g) ? g : Guid.Empty;

        if (userIdSession == Guid.Empty)
        {
            ViewBag.UserLoggedIn = false;
            return (false, Guid.Empty, null);
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == userIdSession);

        if (user == null)
        {
            HttpContext.Session.Clear();
            ViewBag.UserLoggedIn = false;
            return (false, Guid.Empty, null);
        }

        if (user.IsActiveInt != 1)
        {
            HttpContext.Session.Clear();
            ViewBag.UserLoggedIn = false;
            return (false, Guid.Empty, null);
        }

        ViewBag.UserLoggedIn = true;
        ViewBag.UserName = user.FullName ?? user.Username;
        ViewBag.UserRole = user.RoleText;

        return (true, user.UserID, user);
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// USER HELPERS //////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private Guid GetCurrentUserId()
    {
        // Try common claim names
        var v =
            User.FindFirst("UserId")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(v, out var id) ? id : Guid.Empty;
    }

    private static bool TryParseIsoDate(string? iso, out DateTime d)
    {
        d = default;
        if (string.IsNullOrWhiteSpace(iso)) return false;
        return DateTime.TryParseExact(
            iso.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out d
        );
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// HOME PAGES ////////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        bool logged = ViewBag.UserLoggedIn ?? (User?.Identity?.IsAuthenticated == true);
        return View();
    }

    // Privacy
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    // Terms
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Terms()
    {
        return View();
    }

    //---------------------------------------------------------------------------------------//
    //////////////////////////////////////// AUDIT LOG ////////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private async Task AddAuditAsync(Guid userId, string action, string entity, string? entityId, string? details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        await _db.SaveChangesAsync();
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// SETTINGS PAGE /////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Settings(string tab = "profile", string? from = null, string? to = null, string? q = null, string? auditFrom = null, string? auditTo = null, string? auditAction = null)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return RedirectToAction("Index", "Home");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == userId);
        if (user == null) return RedirectToAction("Index", "Home");

        var vm = await BuildSettingsVM(userId, user, tab);

        var tabNorm = (tab ?? "profile").Trim().ToLowerInvariant();

        // finance filters
        if (tabNorm == "finance")
        {
            ApplyFinanceFilters(vm, from, to, q);

            ViewData["FinanceFrom"] = (from ?? "").Trim();
            ViewData["FinanceTo"] = (to ?? "").Trim();
            ViewData["FinanceQ"] = (q ?? "").Trim();
        }

        if (tabNorm == "audit")
        {
            await ApplyAuditFiltersAsync(userId, vm, auditFrom, auditTo, auditAction);

            // keep form values
            vm.AuditFrom = (auditFrom ?? "").Trim();
            vm.AuditTo = (auditTo ?? "").Trim();
            vm.AuditAction = (auditAction ?? "").Trim();
        }

        return View("Views/Home/Settings.cshtml", vm);
    }

    private async Task ApplyAuditFiltersAsync(Guid userId, SettingsModel vm, string? from, string? to, string? action)
    {
        action = (action ?? "").Trim();

        bool hasFrom = TryParseIsoDate(from, out var fromDay);
        bool hasTo = TryParseIsoDate(to, out var toDay);

        long? fromUnix = hasFrom ? ToUnixStartOfDayCairo(fromDay) : null;
        long? toUnix = hasTo ? ToUnixEndOfDayCairo(toDay) : null;

        var q = _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (fromUnix.HasValue) q = q.Where(x => x.CreatedAtUnix >= fromUnix.Value);
        if (toUnix.HasValue) q = q.Where(x => x.CreatedAtUnix <= toUnix.Value);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var aa = action.ToLowerInvariant();
            q = q.Where(x => (x.Action ?? "").ToLower().Contains(aa));
        }

        bool hasAnyFilter = fromUnix.HasValue || toUnix.HasValue || !string.IsNullOrWhiteSpace(action);

        q = q.OrderByDescending(x => x.CreatedAtUnix);

        if (!hasAnyFilter)
            q = q.Take(50);

        var logs = await q
            .Select(x => new AuditLogRowModel
            {
                CreatedAtUnix = x.CreatedAtUnix,
                CreatedAtText = CairoFromUnix(x.CreatedAtUnix),
                Action = x.Action,
                Entity = x.Entity,
            })
            .ToListAsync();

        vm.AuditLogs = logs;
    }

    //---------------------------------------------------------------------------------------//
    /////////////////////////////////// SETTINGS ACTIONS /////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(SettingsModel input)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return RedirectToAction("Settings", new { tab = "profile" });

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserID == userId);
        if (user == null) return RedirectToAction("Settings", new { tab = "profile" });

        // sanitize
        input.FullName = string.IsNullOrWhiteSpace(input.FullName) ? null : input.FullName.Trim();
        input.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();

        var oldFullName = user.FullName;
        var oldPhone = user.Phone;

        user.FullName = input.FullName;
        user.Phone = input.Phone;

        await _db.SaveChangesAsync();

        // Audit
        var details = $"FullName: '{oldFullName}' -> '{user.FullName}', Phone: '{oldPhone}' -> '{user.Phone}'";
        await AddAuditAsync(userId, "update_profile", "users", userId.ToString(), details);

        return RedirectToAction("Settings", new { tab = "profile" });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(SettingsModel input)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return RedirectToAction("Settings", new { tab = "password" });

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserID == userId);
        if (user == null) return RedirectToAction("Settings", new { tab = "password" });

        // basic validation
        if (!ModelState.IsValid)
        {
            // rebuild VM with errors
            var vmBad = await BuildSettingsVM(userId, user, "password");
            vmBad.ErrorMessage = "Please fix password validation errors.";
            vmBad.Password = input.Password ?? new ChangePasswordModel();
            return View("Views/Home/Settings.cshtml", vmBad);
        }

        if (string.IsNullOrEmpty(input.Password.CurrentPassword) || !_dataHasher.VerifyHashed(input.Password.CurrentPassword, user.PasswordHash))
        {
            var vmWrong = await BuildSettingsVM(userId, user, "password");
            vmWrong.ErrorMessage = "Current password is incorrect.";
            vmWrong.Password = new ChangePasswordModel(); // clear
            return View("Views/Home/Settings.cshtml", vmWrong);
        }

        user.PasswordHash = _dataHasher.HashData(input.Password.NewPassword ?? "");
        await _db.SaveChangesAsync();

        await AddAuditAsync(userId, "change_password", "users", userId.ToString(), "User changed password.");

        return RedirectToAction("Settings", new { tab = "password" });
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////// SETTINGS VM BUILDERS //////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private async Task<SettingsModel> BuildSettingsVM(Guid userId, User user, string tab)
    {
        tab = string.IsNullOrWhiteSpace(tab) ? "profile" : tab.Trim().ToLowerInvariant();

        // Collections by user
        var collectionsRaw = await _db.BookingCollections
            .AsNoTracking()
            .Where(x => x.CollectedByUserId == userId)
            .OrderByDescending(x => x.CollectedAtUnix)
            .Take(200)
            .Select(x => new
            {
                x.BookingId,
                x.Amount,
                x.Method,
                x.CollectedAtUnix
            })
            .ToListAsync();

        var collectionBookingIds = collectionsRaw
            .Select(x => x.BookingId)
            .Distinct()
            .ToList();

        Dictionary<Guid, BookingMiniModel> bookingsForCollections;
        if (collectionBookingIds.Count == 0)
        {
            bookingsForCollections = new Dictionary<Guid, BookingMiniModel>();
        }
        else
        {
            bookingsForCollections = await _db.Bookings
                .AsNoTracking()
                .Where(b => collectionBookingIds.Contains(b.Id))
                .Select(b => new BookingMiniModel
                {
                    Id = b.Id,
                    TripId = b.TripId,
                    CustomerName = b.CustomerName ?? "",
                    Phone = b.Phone ?? "",
                    PaidAmount = b.PaidAmount,
                    TotalAmount = b.TotalAmount,
                    IsCanceledInt = b.IsCanceledInt,
                    CreatedAtUnix = b.CreatedAtUnix,
                    CanceledAtUnix = b.CanceledAtUnix,
                    CancelNote = b.CancelNote
                })
                .ToDictionaryAsync(x => x.Id);
        }

        // Canceled bookings by user
        var canceledBookingsRaw = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.IsCanceledInt == 1 && b.CanceledByUserId == userId)
            .OrderByDescending(b => b.CanceledAtUnix)
            .Take(200)
            .Select(b => new BookingMiniModel
            {
                Id = b.Id,
                TripId = b.TripId,
                CustomerName = b.CustomerName ?? "",
                Phone = b.Phone ?? "",
                PaidAmount = b.PaidAmount,
                TotalAmount = b.TotalAmount,
                IsCanceledInt = b.IsCanceledInt,
                CreatedAtUnix = b.CreatedAtUnix,
                CanceledAtUnix = b.CanceledAtUnix,
                CancelNote = b.CancelNote
            })
            .ToListAsync();

        // Booking ops from AuditLog
        var bookingAuditRaw = await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.EntityId != null && l.Entity.ToLower() == "bookings")
            .OrderByDescending(l => l.CreatedAtUnix)
            .Take(400)
            .Select(l => new
            {
                l.CreatedAtUnix,
                l.Action,
                EntityId = l.EntityId!,
                l.Details
            })
            .ToListAsync();

        var bookingAudit = bookingAuditRaw
            .Select(x =>
            {
                var ok = Guid.TryParse(x.EntityId, out var gid);
                return new
                {
                    x.CreatedAtUnix,
                    x.Action,
                    BookingGuid = ok ? gid : Guid.Empty,
                    x.Details,
                    RawEntityId = x.EntityId
                };
            })
            .Where(x => x.BookingGuid != Guid.Empty)
            .ToList();

        var auditedBookingIds = bookingAudit
            .Select(x => x.BookingGuid)
            .Distinct()
            .ToList();

        Dictionary<Guid, BookingMiniModel> auditedBookings;
        if (auditedBookingIds.Count == 0)
        {
            auditedBookings = new Dictionary<Guid, BookingMiniModel>();
        }
        else
        {
            auditedBookings = await _db.Bookings
                .AsNoTracking()
                .Where(b => auditedBookingIds.Contains(b.Id))
                .Select(b => new BookingMiniModel
                {
                    Id = b.Id,
                    TripId = b.TripId,
                    CustomerName = b.CustomerName ?? "",
                    Phone = b.Phone ?? "",
                    PaidAmount = b.PaidAmount,
                    TotalAmount = b.TotalAmount,
                    IsCanceledInt = b.IsCanceledInt,
                    CreatedAtUnix = b.CreatedAtUnix,
                    CanceledAtUnix = b.CanceledAtUnix,
                    CancelNote = b.CancelNote
                })
                .ToDictionaryAsync(x => x.Id);
        }

        var recentBookings = auditedBookings.Values
            .OrderByDescending(x => x.CreatedAtUnix)
            .Take(80)
            .Select(x => new BookingRowModel
            {
                BookingId = x.Id,
                TripId = x.TripId,
                CustomerName = x.CustomerName,
                CustomerPhone = x.Phone,
                PaidAmount = x.PaidAmount,
                TotalAmount = x.TotalAmount,
                IsCanceled = x.IsCanceledInt == 1,
                CreatedAtText = CairoFromUnix(x.CreatedAtUnix)
            })
            .ToList();

        // FINANCE (My Bookings)
        var myBookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.CreatedByUserId == userId)
            .OrderByDescending(b => b.CreatedAtUnix)
            .Take(500)
            .Select(b => new
            {
                b.Id,
                b.CreatedAtUnix,
                b.CustomerName,
                b.Phone,
                b.TotalAmount,
                b.PaidAmount,
                b.IsCanceledInt
            })
            .ToListAsync();

        decimal Due(decimal total, decimal paid) => (total - paid) < 0 ? 0 : (total - paid);

        string StatusText(bool isCanceled, decimal total, decimal paid)
        {
            if (isCanceled) return "Canceled";
            if (paid >= total && total > 0) return "Paid";
            if (paid > 0) return "Partial";
            return "Unpaid";
        }

        var financeBookings = myBookings
            .Select(b => new BookingFinanceRowModel
            {
                BookingId = b.Id,
                CreatedAtUnix = b.CreatedAtUnix,
                CreatedAtText = CairoFromUnix(b.CreatedAtUnix),
                CustomerName = b.CustomerName ?? "",
                CustomerPhone = b.Phone ?? "",
                TotalAmount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                DueAmount = Due(b.TotalAmount, b.PaidAmount),
                IsCanceled = b.IsCanceledInt == 1,
                StatusText = StatusText(b.IsCanceledInt == 1, b.TotalAmount, b.PaidAmount)
            })
            .ToList();

        var active = myBookings.Where(x => x.IsCanceledInt == 0).ToList();
        var canceled = myBookings.Where(x => x.IsCanceledInt == 1).ToList();

        var totals = new FinanceTotalsModel
        {
            TotalBookings = myBookings.Count,
            ActiveBookings = active.Count,
            CanceledBookings = canceled.Count,

            ActiveTotalAmount = active.Sum(x => x.TotalAmount),
            ActivePaidAmount = active.Sum(x => x.PaidAmount),
            ActiveDueAmount = active.Sum(x => Due(x.TotalAmount, x.PaidAmount)),

            CanceledTotalAmount = canceled.Sum(x => x.TotalAmount),
            CanceledPaidAmount = canceled.Sum(x => x.PaidAmount),
        };

        // Recent Collections for my bookings
        var myBookingIds = myBookings.Select(x => x.Id).ToList();

        var recentCollections = myBookingIds.Count == 0
            ? new List<CollectionRowModel>()
            : await _db.BookingCollections
                .AsNoTracking()
                .Include(c => c.Booking)
                .Where(c => myBookingIds.Contains(c.BookingId))
                .OrderByDescending(c => c.CollectedAtUnix)
                .Take(50)
                .Select(c => new CollectionRowModel
                {
                    BookingId = c.BookingId,
                    Amount = c.Amount,
                    Method = c.Method,
                    CollectedAtUnix = c.CollectedAtUnix,
                    CollectedAtText = CairoFromUnix(c.CollectedAtUnix),
                    CustomerName = c.Booking.CustomerName,
                    CustomerPhone = c.Booking.Phone
                })
                .ToListAsync();

        // Recent Canceled (my bookings)
        var recentCanceled = canceled
            .OrderByDescending(x => x.CreatedAtUnix)
            .Take(50)
            .Select(b => new CanceledBookingRowModel
            {
                BookingId = b.Id, // Guid
                TotalAmount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                CustomerName = b.CustomerName ?? "",
                CustomerPhone = b.Phone ?? "",
                CanceledAtUnix = null,
                CanceledAtText = "-",
                CancelNote = null
            })
            .ToList();

        var totalCollected = totals.ActivePaidAmount;
        var totalCanceledPaid = totals.CanceledPaidAmount;
        var canceledCount = totals.CanceledBookings;

        // Finance Timeline
        var events = new List<FinanceEventRowModel>();

        foreach (var c in collectionsRaw)
        {
            bookingsForCollections.TryGetValue(c.BookingId, out var b);

            events.Add(new FinanceEventRowModel
            {
                Unix = c.CollectedAtUnix,
                DateText = CairoFromUnix(c.CollectedAtUnix),
                Type = "collection",
                Action = "collection",
                BookingId = c.BookingId.ToString(),
                TripId = b?.TripId,
                CustomerName = b?.CustomerName,
                CustomerPhone = b?.Phone,
                Amount = c.Amount,
                PaidAmount = b?.PaidAmount,
                TotalAmount = b?.TotalAmount,
                Method = c.Method,
                Note = null
            });
        }

        foreach (var b in canceledBookingsRaw)
        {
            var unix = b.CanceledAtUnix ?? b.CreatedAtUnix;

            events.Add(new FinanceEventRowModel
            {
                Unix = unix,
                DateText = b.CanceledAtUnix.HasValue ? CairoFromUnix(b.CanceledAtUnix.Value) : CairoFromUnix(b.CreatedAtUnix),
                Type = "cancel",
                Action = "cancel_booking",
                BookingId = b.Id.ToString(),
                TripId = b.TripId,
                CustomerName = b.CustomerName,
                CustomerPhone = b.Phone,
                PaidAmount = b.PaidAmount,
                TotalAmount = b.TotalAmount,
                Note = b.CancelNote
            });
        }

        foreach (var a in bookingAudit)
        {
            auditedBookings.TryGetValue(a.BookingGuid, out var b);

            events.Add(new FinanceEventRowModel
            {
                Unix = a.CreatedAtUnix,
                DateText = CairoFromUnix(a.CreatedAtUnix),
                Type = "booking",
                Action = a.Action,
                BookingId = a.BookingGuid.ToString(),
                TripId = b?.TripId,
                CustomerName = b?.CustomerName,
                CustomerPhone = b?.Phone,
                PaidAmount = b?.PaidAmount,
                TotalAmount = b?.TotalAmount,
                Note = a.Details
            });
        }

        var financeTimeline = events
            .OrderByDescending(x => x.Unix)
            .Take(300)
            .ToList();

        // General audit logs
        var logs = new List<AuditLogRowModel>();

        return new SettingsModel
        {
            UserId = user.UserID,
            Username = user.Username,
            RoleText = user.RoleText,
            IsActive = user.IsActiveInt == 1,
            FullName = user.FullName,
            Phone = user.Phone,
            ActiveTab = tab,

            FinanceBookings = financeBookings,

            RecentCollections = recentCollections,

            AuditLogs = logs
        };
    }

    private void ApplyFinanceFilters(SettingsModel vm, string? from, string? to, string? q)
    {
        q = (q ?? "").Trim();

        bool hasFrom = TryParseIsoDate(from, out var fromDay);
        bool hasTo = TryParseIsoDate(to, out var toDay);

        long? fromUnix = hasFrom ? ToUnixStartOfDayCairo(fromDay) : null;
        long? toUnix = hasTo ? ToUnixEndOfDayCairo(toDay) : null;

        // Filter bookings (includes canceled already because FinanceBookings has IsCanceled)
        var bookings = (vm.FinanceBookings ?? new List<BookingFinanceRowModel>()).AsQueryable();

        if (fromUnix.HasValue) bookings = bookings.Where(b => b.CreatedAtUnix >= fromUnix.Value);
        if (toUnix.HasValue) bookings = bookings.Where(b => b.CreatedAtUnix <= toUnix.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qq = q.ToLowerInvariant();
            bookings = bookings.Where(b =>
                (b.CustomerName ?? "").ToLower().Contains(qq) ||
                (b.CustomerPhone ?? "").ToLower().Contains(qq) ||
                b.BookingId.ToString().Contains(qq)
            );
        }

        var filteredBookings = bookings
            .OrderByDescending(x => x.CreatedAtUnix)
            .ToList();

        vm.FinanceBookings = filteredBookings;

        // Fetch collections for THESE bookings within same date range (and filter q too)
        var bookingIds = filteredBookings.Select(x => x.BookingId).Distinct().ToList();

        if (bookingIds.Count == 0)
        {
            vm.RecentCollections = new List<CollectionRowModel>();

            ViewData["FinanceCard_TotalDue"] = 0m;
            ViewData["FinanceCard_TotalPaid"] = 0m;
            ViewData["FinanceCard_Remaining"] = 0m;
            ViewData["FinanceCard_TotalCollected"] = 0m;
            ViewData["FinanceCard_BookingsCount"] = 0;
            ViewData["FinanceCard_CollectionsCount"] = 0;
            return;
        }

        var collectionsQ = _db.BookingCollections
            .AsNoTracking()
            .Include(c => c.Booking)
            .Where(c => bookingIds.Contains(c.BookingId));

        if (fromUnix.HasValue) collectionsQ = collectionsQ.Where(c => c.CollectedAtUnix >= fromUnix.Value);
        if (toUnix.HasValue) collectionsQ = collectionsQ.Where(c => c.CollectedAtUnix <= toUnix.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qq = q.ToLowerInvariant();
            collectionsQ = collectionsQ.Where(c =>
                (c.Booking.CustomerName ?? "").ToLower().Contains(qq) ||
                (c.Booking.Phone ?? "").ToLower().Contains(qq) ||
                c.BookingId.ToString().Contains(qq) ||
                (c.Method ?? "").ToLower().Contains(qq)
            );
        }

        var collections = collectionsQ
            .OrderByDescending(c => c.CollectedAtUnix)
            .Take(200)
            .Select(c => new CollectionRowModel
            {
                BookingId = c.BookingId,
                Amount = c.Amount,
                Method = c.Method,
                CollectedAtUnix = c.CollectedAtUnix,
                CollectedAtText = CairoFromUnix(c.CollectedAtUnix),
                CustomerName = c.Booking.CustomerName,
                CustomerPhone = c.Booking.Phone
            })
            .ToList();

        vm.RecentCollections = collections;

        // Recompute summary cards based on filtered result (ACTIVE only)
        var active = filteredBookings.Where(x => !x.IsCanceled).ToList();

        var totalDue = active.Sum(x => x.TotalAmount);
        var totalPaidBookings = active.Sum(x => x.PaidAmount);

        var remaining = totalDue - totalPaidBookings;
        if (remaining < 0) remaining = 0;

        var totalCollected = collections.Sum(x => x.Amount);

        ViewData["FinanceCard_TotalDue"] = totalDue;
        ViewData["FinanceCard_TotalPaid"] = totalPaidBookings;
        ViewData["FinanceCard_Remaining"] = remaining;
        ViewData["FinanceCard_TotalCollected"] = totalCollected;

        ViewData["FinanceCard_BookingsCount"] = filteredBookings.Count;
        ViewData["FinanceCard_CollectionsCount"] = collections.Count;
    }

    //---------------------------------------------------------------------------------------//
    /////////////////////////////////////// SCANNER //////////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Scan()
    {
        return View("Views/Home/Tickets/Scan.cshtml", new TicketScanModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(TicketScanModel vm)
    {
        vm.Error = null;

        // 1) Parse BookingId GUID
        var raw = (vm.BookingIdRaw ?? "").Trim();
        if (!TryExtractBookingId(raw, out var bookingId))
        {
            vm.Error = "Invalid booking ID. Scan a valid QR code or enter the booking GUID.";
            return View("~/Views/Home/Tickets/Scan.cshtml", vm);
        }

        // Load booking + main trip + bus + seats + driver phones
        try
        {
            var booking = await _db.Bookings
              .AsNoTracking()
              .Include(b => b.DestinationPlace)
              .Include(b => b.ReturnDestinationPlace)
              .Include(b => b.Trip)
                  .ThenInclude(t => t.Bus)
                      .ThenInclude(bus => bus!.Seats)
              .Include(b => b.Trip)
                  .ThenInclude(t => t.Driver)
                      .ThenInclude(d => d!.Phones)
              .FirstOrDefaultAsync(b => b.Id == bookingId);



            DateTime? tripDateToCheck = null;

            // GO ticket
            if (booking!.BookingTypeInt == 1)
            {
                if (DateTime.TryParseExact(booking.Trip.DepartDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    tripDateToCheck = d;
                }
            }

            // RETURN ticket
            else if (booking.BookingTypeInt == 2)
            {
                if (booking.ReturnTripId.HasValue)
                {
                    var returnTrip = await _db.Trips
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == booking.ReturnTripId.Value);

                    if (returnTrip != null &&
                        DateTime.TryParseExact(returnTrip.DepartDate, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        tripDateToCheck = d;
                    }
                }
            }

            // ROUND ticket -> check RETURN date
            else if (booking.BookingTypeInt == 3)
            {
                if (booking.ReturnTripId.HasValue)
                {
                    var returnTrip = await _db.Trips
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == booking.ReturnTripId.Value);

                    if (returnTrip != null &&
                        DateTime.TryParseExact(returnTrip.DepartDate, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        tripDateToCheck = d;
                    }
                }
            }

            // FINAL CHECK
            if (tripDateToCheck.HasValue && tripDateToCheck.Value.Date < DateTime.Today)
            {
                vm.Error = "This ticket has expired.";
                vm.BookingIdRaw = bookingId.ToString("D");
                return View("Views/Home/Tickets/Scan.cshtml", vm);
            }


            // GO segment
            // GO destination from booking
            var goDest = ResolveDestinationPlaceNameFromBooking(booking, isReturnSegment: false);

            vm.Go = BuildSegment(
                title: "GO",
                trip: booking.Trip,
                bus: booking.Trip?.Bus,
                driver: booking.Trip?.Driver,
                seatsText: booking.SeatsText,
                destinationPlaceName: goDest
            );

            // RETURN destination from booking
            vm.Return = null;

            var isRound = (booking.BookingTypeInt == 3) && booking.ReturnTripId.HasValue;
            if (isRound)
            {
                var returnTrip = await _db.Trips
                    .AsNoTracking()
                    .Include(t => t.Bus).ThenInclude(b => b!.Seats)
                    .Include(t => t.Driver).ThenInclude(d => d!.Phones)
                    .FirstOrDefaultAsync(t => t.Id == booking.ReturnTripId!.Value);

                var retDest = ResolveDestinationPlaceNameFromBooking(booking, isReturnSegment: true);

                vm.Return = BuildSegment(
                    title: "RETURN",
                    trip: returnTrip,
                    bus: returnTrip?.Bus,
                    driver: returnTrip?.Driver,
                    seatsText: booking.SeatsReturnText,
                    destinationPlaceName: retDest
                );
            }

            vm.Booking = booking;
            vm.BookingIdRaw = bookingId.ToString("D");

            return View("Views/Home/Tickets/Scan.cshtml", vm);
        }
        catch (Exception)
        {
            vm.Error = $"No reservation with this number: {bookingId}";
            vm.BookingIdRaw = bookingId.ToString("D");
            return View("~/Views/Home/Tickets/Scan.cshtml", vm);
        }
    }

    //---------------------------------------------------------------------------------------//
    /////////////////////////////////// SCANNER HELPERS //////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private static TicketTripSegmentModel BuildSegment(string title, Trip? trip, Bus? bus, Driver? driver, string? seatsText, string destinationPlaceName)
    {
        var seg = new TicketTripSegmentModel
        {
            Title = title,
            Trip = trip,
            Bus = bus,
            Driver = driver,
            DestinationPlaceName = string.IsNullOrWhiteSpace(destinationPlaceName) ? "-" : destinationPlaceName.Trim(),
            BookedSeats = ParseSeatCodes(seatsText)
        };

        var (arch, reason) = ComputeTripArchive(trip);
        seg.IsArchived = arch;
        seg.ArchiveReason = reason;

        return seg;
    }

    private static HashSet<string> ParseSeatCodes(string? seatsText)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(seatsText)) return set;

        var parts = seatsText
            .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);

        foreach (var p in parts) set.Add(p);
        return set;
    }

    private static (bool isArchived, string reason) ComputeTripArchive(Trip? trip)
    {
        if (trip == null) return (true, "Trip not found");

        var tripArchived =
            (trip.GetType().GetProperty("IsArchived")?.GetValue(trip) as bool?) == true
            || (trip.GetType().GetProperty("IsArchivedInt")?.GetValue(trip) as int?) == 1;

        var departDate = trip.GetType().GetProperty("DepartDate")?.GetValue(trip)?.ToString();
        var departTime = trip.GetType().GetProperty("DepartTime")?.GetValue(trip)?.ToString();

        if (tripArchived) return (true, "Archived (DB)");

        if (string.IsNullOrWhiteSpace(departDate) || string.IsNullOrWhiteSpace(departTime))
            return (false, "");

        if (!DateTime.TryParseExact($"{departDate} {departTime}", "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return (false, "");

        if (dt < DateTime.Now) return (true, "Ended");
        return (false, "");
    }

    private static string ResolveDestinationPlaceNameFromBooking(Booking booking, bool isReturnSegment)
    {
        if (booking == null) return "-";

        // Return segment destination
        if (isReturnSegment)
        {
            // 1) If navigation loaded
            var navName = booking.ReturnDestinationPlace?.PlaceName;
            if (!string.IsNullOrWhiteSpace(navName))
                return navName.Trim();

            // 2) Fallback to stored text column
            if (!string.IsNullOrWhiteSpace(booking.ReturnDestinationPlaceName))
                return booking.ReturnDestinationPlaceName!.Trim();

            return "-";
        }

        // GO segment destination
        {
            // 1) If navigation loaded
            var navName = booking.DestinationPlace?.PlaceName;
            if (!string.IsNullOrWhiteSpace(navName))
                return navName.Trim();

            // 2) Fallback to stored text column
            if (!string.IsNullOrWhiteSpace(booking.DestinationPlaceName))
                return booking.DestinationPlaceName.Trim();

            return "-";
        }
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// GUID PARSING //////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    private static readonly Regex GuidRegex = new Regex(@"(?i)\b[0-9a-f]{8}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{12}\b", RegexOptions.Compiled);

    private static bool TryExtractBookingId(string raw, out Guid bookingId)
    {
        bookingId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        raw = raw.Trim();

        // 1) If input is exactly a GUID
        if (Guid.TryParse(raw, out bookingId) && bookingId != Guid.Empty)
            return true;

        // 2) If input contains a GUID anywhere (QR text / URL / "booking=GUID" / etc.)
        var m = GuidRegex.Match(raw);
        if (m.Success && Guid.TryParse(m.Value, out bookingId) && bookingId != Guid.Empty)
            return true;

        return false;
    }

    //---------------------------------------------------------------------------------------//
    ////////////////////////////////////// ERROR PAGE ////////////////////////////////////////
    //---------------------------------------------------------------------------------------//

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}