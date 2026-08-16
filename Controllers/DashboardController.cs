using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using NoufirTours.Models.Dashboard;
using System.Globalization;
using System.Security.Claims;

namespace NoufirTours.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly DBContext _db;
        private readonly IWebHostEnvironment _env;

        public DashboardController(DBContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        //---------------------------------------------------------------------------------------//
        ///////////////////////////////////////// HELPERS /////////////////////////////////////////
        //---------------------------------------------------------------------------------------//
        private Guid GetCurrentUserId()
        {
            // Try common claim names
            var v =
                User.FindFirst("UserId")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(v, out var id) ? id : Guid.Empty;
        }

        private string GetCurrentUsername()
        {
            // Prefer Username claim if you set it, otherwise Identity.Name
            return User?.FindFirstValue("Username")
                ?? User?.Identity?.Name
                ?? "";
        }

        private bool IsAdmin()
        {
            var role = User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role") ?? "";
            if (!string.IsNullOrWhiteSpace(role) && role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                return true;

            // fallback: if you store role in DB and not in claims
            return User!.IsInRole("admin");
        }

        private static bool IsYmd(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return DateTime.TryParseExact(
                s.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            );
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
        ////////////////////////////////////////// INDEX //////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] DashboardFilterModel filter)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            // Default values (today)
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            if (!filter.AllBookings && string.IsNullOrWhiteSpace(filter.From) && string.IsNullOrWhiteSpace(filter.To))
            {
                filter.From = today;
                filter.To = today;
            }

            if (!filter.AllBookings && !string.IsNullOrWhiteSpace(filter.From) && string.IsNullOrWhiteSpace(filter.To))
                filter.To = filter.From;

            if (!filter.AllBookings && string.IsNullOrWhiteSpace(filter.From) && !string.IsNullOrWhiteSpace(filter.To))
                filter.From = filter.To;

            if (!filter.AllBookings && !string.IsNullOrWhiteSpace(filter.From) && !string.IsNullOrWhiteSpace(filter.To))
            {
                if (string.CompareOrdinal(filter.From.Trim(), filter.To.Trim()) > 0)
                {
                    var tmp = filter.From;
                    filter.From = filter.To;
                    filter.To = tmp;
                }
            }

            // Trips filter
            var tripsQ = _db.Trips.AsNoTracking();

            if (!filter.IncludeArchivedTrips)
                tripsQ = tripsQ.Where(t => t.IsArchivedInt == 0);

            if (!filter.AllBookings && !string.IsNullOrWhiteSpace(filter.From) && !string.IsNullOrWhiteSpace(filter.To))
            {
                var from = filter.From.Trim();
                var to = filter.To.Trim();

                tripsQ = tripsQ.Where(t =>
                    t.DepartDate != null
                    && string.Compare(t.DepartDate, from) >= 0
                    && string.Compare(t.DepartDate, to) <= 0
                );
            }

            // Bookings filter
            var bookingsQ = _db.Bookings.AsNoTracking();

            if (!isAdmin)
                bookingsQ = bookingsQ.Where(b => b.CompanyFrom == username);
            else if (!string.IsNullOrWhiteSpace(filter.Company))
                bookingsQ = bookingsQ.Where(b => b.CompanyFrom.Contains(filter.Company.Trim()));

            // Flatten bookings to legs (GO/RETURN)
            var goLegsQ =
                from b in bookingsQ
                select new
                {
                    TripId = b.TripId,
                    BookingId = b.Id,
                    CompanyFrom = b.CompanyFrom,

                    //PaidAmount = b.PaidAmount,
                    //TotalAmount = b.TotalAmount,
                    PaidAmount = b.BookingTypeInt == 3 ? b.PaidAmount / 2m : b.PaidAmount,
                    TotalAmount = b.BookingTypeInt == 3 ? b.TotalAmount / 2m : b.TotalAmount,

                    SeatsText = b.SeatsText,
                    Segment = (b.BookingTypeInt == 2) ? "RETURN" : "GO"
                };

            var returnLegsQ =
                from b in bookingsQ
                where b.BookingTypeInt == 3 && b.ReturnTripId != null
                select new
                {
                    TripId = b.ReturnTripId!.Value,
                    BookingId = b.Id,
                    CompanyFrom = b.CompanyFrom,
                    //PaidAmount = 0m,
                    //TotalAmount = 0m,
                    PaidAmount = b.PaidAmount / 2m,
                    TotalAmount = b.TotalAmount / 2m,
                    SeatsText = b.SeatsReturnText ?? "",
                    Segment = "RETURN"
                };

            var legsQ = goLegsQ.Concat(returnLegsQ);

            // NEW: detect segment type per trip to know when route should be reversed
            var segmentsByTrip = await legsQ
                .GroupBy(l => l.TripId)
                .Select(g => new
                {
                    TripId = g.Key,
                    HasGo = g.Any(x => x.Segment == "GO"),
                    HasReturn = g.Any(x => x.Segment == "RETURN")
                })
                .ToListAsync();

            var segmentMap = segmentsByTrip.ToDictionary(
                x => x.TripId,
                x => new { x.HasGo, x.HasReturn }
            );

            // SQL-friendly aggregation (NO Companies list here)
            var legsAggQ =
                from l in legsQ
                group l by l.TripId into g
                select new
                {
                    TripId = g.Key,
                    BookingsCount = g.Select(x => x.BookingId).Distinct().Count(),
                    TotalPaid = g.Sum(x => (decimal?)x.PaidAmount) ?? 0m,
                    TotalDue = g.Sum(x => (decimal?)x.TotalAmount) ?? 0m
                };

            var rows = new List<DashboardTripRowModel>();
            // Join trips with aggregates
            if (isAdmin)
            {
                rows = await (
                    from t in tripsQ
                    join a in legsAggQ on t.Id equals a.TripId
                    select new DashboardTripRowModel
                    {
                        TripId = t.Id,
                        TripName = t.TripName ?? "",
                        DepartDate = t.DepartDate,
                        DepartTime = t.DepartTime,
                        FromCity = t.FromCity,
                        ToCity = t.ToCity,
                        IsArchived = t.IsArchivedInt == 1,

                        BookingsCount = a.BookingsCount,
                        SeatsCount = 0,

                        TotalPaid = a.TotalPaid,
                        TotalDue = a.TotalDue,

                        // IMPORTANT: keep it empty here to avoid EF translation issues
                        Companies = new List<string>()
                    }
                )
                .OrderByDescending(r => r.DepartDate)
                .ThenBy(r => r.DepartTime)
                .ToListAsync();
            }
            else
            {
                var userRowsRaw = await (
                    from l in legsQ
                    join b in bookingsQ on l.BookingId equals b.Id
                    join t in tripsQ on l.TripId equals t.Id
                    select new
                    {
                        TripId = t.Id,
                        CustomerName = b.CustomerName,
                        TripName = t.TripName,
                        DepartDate = t.DepartDate,
                        DepartTime = t.DepartTime,
                        FromCity = l.Segment == "RETURN" ? t.ToCity : t.FromCity,
                        ToCity = l.Segment == "RETURN" ? t.FromCity : t.ToCity,
                        IsArchived = t.IsArchivedInt == 1,
                        SeatsText = l.SeatsText,
                        PaidAmount = b.PaidAmount,
                        TotalAmount = b.TotalAmount
                    }
                )
                .OrderByDescending(x => x.DepartDate)
                .ThenBy(x => x.DepartTime)
                .ThenBy(x => x.CustomerName)
                .ToListAsync();

                rows = userRowsRaw.Select(x => new DashboardTripRowModel
                {
                    TripId = x.TripId,
                    ClientName = x.CustomerName ?? "",
                    TripName = x.TripName ?? "",
                    DepartDate = x.DepartDate,
                    DepartTime = x.DepartTime,
                    FromCity = x.FromCity,
                    ToCity = x.ToCity,
                    IsArchived = x.IsArchived,

                    BookingsCount = 1,
                    SeatsCount = SplitSeats(x.SeatsText).Count(),

                    TotalPaid = x.PaidAmount,
                    TotalDue = x.TotalAmount,

                    Companies = new List<string>()
                }).ToList();
            }

            // SeatsCount without N+1 (ONE query)
            var tripIds = rows.Select(r => r.TripId).ToList();

            if (isAdmin)
            {
                var seatsByTrip = await legsQ
                    .Where(l => tripIds.Contains(l.TripId))
                    .Select(l => new { l.TripId, l.SeatsText })
                    .ToListAsync();

                var seatCountMap = seatsByTrip
                    .GroupBy(x => x.TripId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(x => SplitSeats(x.SeatsText)).Count()
                    );

                foreach (var r in rows)
                    r.SeatsCount = seatCountMap.TryGetValue(r.TripId, out var c) ? c : 0;
            }

            // NEW: reverse route for RETURN-only trips
            foreach (var r in rows)
            {
                if (segmentMap.TryGetValue(r.TripId, out var seg))
                {
                    if (!seg.HasGo && seg.HasReturn)
                    {
                        var tmp = r.FromCity;
                        r.FromCity = r.ToCity;
                        r.ToCity = tmp;
                    }
                }
            }

            // Admin-only: fill Companies with a second query (safe)
            if (isAdmin)
            {
                var companiesPairs = await legsQ
                    .Where(l => tripIds.Contains(l.TripId))
                    .Select(l => new { l.TripId, l.CompanyFrom })
                    .Distinct()
                    .ToListAsync();

                var companiesMap = companiesPairs
                    .GroupBy(x => x.TripId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.CompanyFrom).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
                    );

                foreach (var r in rows)
                    r.Companies = companiesMap.TryGetValue(r.TripId, out var list) ? list : new List<string>();
            }

            var vm = new DashboardIndexModel
            {
                Filter = filter,
                IsAdmin = isAdmin,
                CurrentUsername = username,
                Trips = rows
            };

            return View("~/Views/Dashboard/Index.cshtml", vm);

        }

        //---------------------------------------------------------------------------------------//
        ///////////////////////////////////////// DETAILS /////////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> TripDetails(Guid id, [FromQuery] string? company)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            var trip = await _db.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null) return NotFound();

            var bookingsBaseQ = _db.Bookings.AsNoTracking();

            if (!isAdmin)
                bookingsBaseQ = bookingsBaseQ.Where(b => b.CompanyFrom == username);
            else if (!string.IsNullOrWhiteSpace(company))
                bookingsBaseQ = bookingsBaseQ.Include(b => b.CodeInfo).Where(b => b.CompanyFrom.Contains(company.Trim()));
            else
                bookingsBaseQ = bookingsBaseQ.Include(b => b.CodeInfo);

            // any booking whose main TripId == id  (GO or RETURN-only)
            // any ROUND booking whose ReturnTripId == id (RETURN leg)
            var goOrSingleReturnLegsQ =
                from b in bookingsBaseQ
                where b.TripId == id
                select new DashboardBookingRowModel
                {
                    BookingId = b.Id,
                    CompanyFrom = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,
                    CodeDel = isAdmin ? b.CodeInfo!.Code : "",
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,
                    SeatsText = b.SeatsText,
                    TripSegment = (b.BookingTypeInt == 2) ? "RETURN" : "GO",

                    CustomerDropoffPlace =
                        b.DestinationPlace != null ? b.DestinationPlace.PlaceName : null
                };

            var roundReturnLegsQ =
                from b in bookingsBaseQ
                where b.BookingTypeInt == 3 && b.ReturnTripId == id
                select new DashboardBookingRowModel
                {
                    BookingId = b.Id,
                    CodeDel = isAdmin ? b.CodeInfo!.Code : "",
                    CompanyFrom = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,
                    SeatsText = b.SeatsReturnText ?? "",
                    TripSegment = "RETURN",

                    CustomerDropoffPlace =
                        b.ReturnDestinationPlace != null ? b.ReturnDestinationPlace.PlaceName : null
                };

            var bookings = await goOrSingleReturnLegsQ
                .Concat(roundReturnLegsQ)
                .OrderBy(b => b.CompanyFrom)
                .ThenBy(b => b.CustomerName)
                .ToListAsync();

            // Deleted tickets for this trip
            var deletedTicketsQ = _db.DeletedTickets
                .AsNoTracking()
                .Include(x => x.DeletedByUser)
                .Where(x => x.TripId == id || x.ReturnTripId == id);

            if (!isAdmin)
                deletedTicketsQ = deletedTicketsQ.Where(x => x.CompanyFrom == username);
            else if (!string.IsNullOrWhiteSpace(company))
                deletedTicketsQ = deletedTicketsQ.Where(x => x.CompanyFrom.Contains(company.Trim()));

            var deletedTickets = await deletedTicketsQ
                .OrderByDescending(x => x.DeletedAtUnix)
                .ToListAsync();

            var vm = new DashboardTripDetailsModel
            {
                IsAdmin = isAdmin,
                CurrentUsername = username,
                TripId = trip.Id,
                TripName = trip.TripName,
                DepartDate = trip.DepartDate,
                DepartTime = trip.DepartTime,
                FromCity = trip.FromCity,
                ToCity = trip.ToCity,
                PickupPlace = trip.PickupPlace,
                DropoffPlace = trip.DropoffPlace,
                IsArchived = trip.IsArchivedInt == 1,
                Description = string.IsNullOrWhiteSpace(trip.Notes) ? "لا توجد ملاحظات" : trip.Notes,
                CompanyFilter = company,
                Bookings = bookings,
                DeletedTickets = deletedTickets
            };

            return View("~/Views/Dashboard/TripDetails.cshtml", vm);
        }

        //---------------------------------------------------------------------------------------//
        /////////////////////////////////////// PRINT (PDF) ///////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> PrintTrip(Guid id, [FromQuery] string? company)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            var trip = await _db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (trip == null)
            {
                TempData["DashError"] = "Trip not found.";
                return RedirectToAction("Index");
            }

            var bookingsBaseQ = _db.Bookings.AsNoTracking();

            if (!isAdmin)
                bookingsBaseQ = bookingsBaseQ.Where(b => b.CompanyFrom == username);
            else if (!string.IsNullOrWhiteSpace(company))
                bookingsBaseQ = bookingsBaseQ.Where(b => b.CompanyFrom.Contains(company.Trim()));

            var goOrSingleReturnLegsQ =
                from b in bookingsBaseQ
                where b.TripId == id
                select new DashboardBookingRowModel
                {
                    BookingId = b.Id,
                    CompanyFrom = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,

                    SeatsText = b.SeatsText,
                    TripSegment = (b.BookingTypeInt == 2) ? "RETURN" : "GO",
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,

                    CustomerDropoffPlace =
                        b.DestinationPlace != null ? b.DestinationPlace.PlaceName : null
                };

            var roundReturnLegsQ =
                from b in bookingsBaseQ
                where b.BookingTypeInt == 3 && b.ReturnTripId == id
                select new DashboardBookingRowModel
                {
                    BookingId = b.Id,
                    CompanyFrom = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,

                    SeatsText = b.SeatsReturnText ?? "",
                    TripSegment = "RETURN",
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,

                    CustomerDropoffPlace =
                        b.ReturnDestinationPlace != null ? b.ReturnDestinationPlace.PlaceName : null
                };

            var bookings = await goOrSingleReturnLegsQ
                .Concat(roundReturnLegsQ)
                .OrderBy(b => b.CompanyFrom)
                .ThenBy(b => b.CustomerName)
                .ToListAsync();

            if (!isAdmin && bookings.Count == 0)
            {
                TempData["DashError"] = "You don’t have bookings on this trip (or you don’t have access).";
                return RedirectToAction("Index");
            }

            var vm = new DashboardTripDetailsModel
            {
                IsAdmin = isAdmin,
                CurrentUsername = username,

                TripId = trip.Id,
                TripName = trip.TripName,
                DepartDate = trip.DepartDate,
                DepartTime = trip.DepartTime,
                FromCity = trip.FromCity,
                ToCity = trip.ToCity,
                PickupPlace = trip.PickupPlace,
                DropoffPlace = trip.DropoffPlace,
                IsArchived = trip.IsArchivedInt == 1,
                Description = string.IsNullOrWhiteSpace(trip.Notes) ? "لا توجد ملاحظات" : trip.Notes,

                CompanyFilter = company,
                Bookings = bookings
            };

            return View("~/Views/Dashboard/PrintTrip.cshtml", vm);
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////////// Excel Export ///////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        [HttpGet]
        public async Task<IActionResult> ExportExcel([FromQuery] DashboardFilterModel filter)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            // Validate/Normalize date inputs
            string? from = null, to = null;

            if (!filter.AllBookings)
            {
                var rawFrom = (filter.From ?? "").Trim();
                var rawTo = (filter.To ?? "").Trim();

                if (!string.IsNullOrWhiteSpace(rawFrom) && string.IsNullOrWhiteSpace(rawTo))
                    rawTo = rawFrom;

                if (string.IsNullOrWhiteSpace(rawFrom) && !string.IsNullOrWhiteSpace(rawTo))
                    rawFrom = rawTo;

                if (string.IsNullOrWhiteSpace(rawFrom) || string.IsNullOrWhiteSpace(rawTo))
                {
                    TempData["DashError"] = "Please select From and To dates (or enable All bookings).";
                    return RedirectToAction("Index");
                }

                if (!IsYmd(rawFrom) || !IsYmd(rawTo))
                {
                    TempData["DashError"] = "Invalid date format. Please pick dates from the calendar.";
                    return RedirectToAction("Index");
                }

                if (string.CompareOrdinal(rawFrom, rawTo) > 0)
                {
                    var tmp = rawFrom;
                    rawFrom = rawTo;
                    rawTo = tmp;
                }

                from = rawFrom;
                to = rawTo;
            }

            // Trips filter
            var tripsQ = _db.Trips.AsNoTracking();

            if (!filter.IncludeArchivedTrips)
                tripsQ = tripsQ.Where(t => t.IsArchivedInt == 0);

            if (!filter.AllBookings && from != null && to != null)
            {
                tripsQ = tripsQ.Where(t =>
                    t.DepartDate != null &&
                    string.Compare(t.DepartDate, from) >= 0 &&
                    string.Compare(t.DepartDate, to) <= 0
                );
            }

            // Bookings filter
            var bookingsBaseQ = _db.Bookings.AsNoTracking();

            if (!isAdmin)
                bookingsBaseQ = bookingsBaseQ.Where(b => b.CompanyFrom == username);
            else if (!string.IsNullOrWhiteSpace(filter.Company))
                bookingsBaseQ = bookingsBaseQ.Where(b => b.CompanyFrom.Contains(filter.Company.Trim()));

            // Flatten bookings into legs then join by TripId so RETURN legs appear on their return trip day
            var goOrSingleReturnLegsQ =
                from b in bookingsBaseQ
                select new
                {
                    TripId = b.TripId,
                    Company = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,
                    Seats = b.SeatsText,
                    Segment = (b.BookingTypeInt == 2) ? "RETURN" : "GO",
                    CustomerDropoffPlace = b.DestinationPlace != null ? b.DestinationPlace.PlaceName : null,
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,
                    TicketCode = isAdmin ? (b.CodeInfo!.Code ?? "") : ""
                };

            var roundReturnLegsQ =
                from b in bookingsBaseQ
                where b.BookingTypeInt == 3 && b.ReturnTripId != null
                select new
                {
                    TripId = b.ReturnTripId!.Value,
                    Company = b.CompanyFrom,
                    CustomerName = b.CustomerName,
                    Phone = b.Phone,
                    Seats = b.SeatsReturnText ?? "",
                    Segment = "RETURN",
                    CustomerDropoffPlace = b.ReturnDestinationPlace != null ? b.ReturnDestinationPlace.PlaceName : null,
                    Description = string.IsNullOrWhiteSpace(b.Notes) ? "لا توجد ملاحظات" : b.Notes,
                    TicketCode = isAdmin ? (b.CodeInfo!.Code ?? "") : ""
                };

            var legsQ = goOrSingleReturnLegsQ.Concat(roundReturnLegsQ);

            var busesQ = _db.Buses.AsNoTracking();

            var data = await (
                 from l in legsQ
                 join t in tripsQ on l.TripId equals t.Id
                 join b in busesQ on t.BusId equals b.Id into bj
                 from bus in bj.DefaultIfEmpty()
                 select new
                 {
                     Company = l.Company,
                     CustomerName = l.CustomerName,
                     Phone = l.Phone,
                     Seats = l.Seats,
                     Segment = l.Segment,

                     TripName = t.TripName,
                     TripDate = t.DepartDate,
                     TripTime = t.DepartTime,

                     FromCity = l.Segment == "RETURN"
                         ? (t.ToCity ?? "")
                         : (t.FromCity ?? ""),

                     ToCity = l.Segment == "RETURN"
                         ? (t.FromCity ?? "")
                         : (t.ToCity ?? ""),

                     BusName = bus != null
                         ? (bus.BusNumber + (bus.PlateNumber != null && bus.PlateNumber != "" ? $" ({bus.PlateNumber})" : ""))
                         : "",

                     CustomerDropoffPlace = l.CustomerDropoffPlace,
                     Descrption = l.Description,
                     TicketCode = l.TicketCode
                 })
                 .OrderByDescending(x => x.TripDate)
                 .ThenBy(x => x.TripTime)
                 .ThenBy(x => x.Company)
                 .ThenBy(x => x.CustomerName)
                 .ToListAsync();

            if (data.Count == 0)
            {
                TempData["DashError"] = "No data found for the selected filters.";
                return RedirectToAction("Index");
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bookings");

            // COLORS / THEME
            var noufirBlue = XLColor.FromHtml("#0D6EFD");
            var toursYellow = XLColor.FromHtml("#FFC107");
            var brandGreen = XLColor.FromHtml("#198754");
            var darkText = XLColor.FromHtml("#0F172A");
            var mutedText = XLColor.FromHtml("#64748B");
            var softBlue = XLColor.FromHtml("#EAF2FF");
            var softYellow = XLColor.FromHtml("#FFF8DB");
            var softGreen = XLColor.FromHtml("#EAF8F0");
            var softGray = XLColor.FromHtml("#F8FAFC");
            var borderColor = XLColor.FromHtml("#D7E0EA");
            var white = XLColor.White;
            var notesRed = XLColor.FromHtml("#DC2626");
            var notesSoft = XLColor.FromHtml("#FEF2F2");

            int totalColumns = isAdmin ? 14 : 13;

            // TOP BRAND AREA
            ws.Range(1, 1, 1, totalColumns).Merge();
            var titleCell = ws.Cell(1, 1);
            titleCell.Style.Fill.BackgroundColor = darkText;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 22;

            var rich = titleCell.GetRichText();
            rich.ClearText();
            rich.AddText("Noufir").SetFontColor(noufirBlue).SetBold().SetFontSize(22);
            rich.AddText("Tours").SetFontColor(toursYellow).SetBold().SetFontSize(22);

            ws.Range(2, 1, 2, totalColumns).Merge();
            ws.Cell(2, 1).Value = "Bookings Report";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 15;
            ws.Cell(2, 1).Style.Font.FontColor = darkText;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(2, 1).Style.Fill.BackgroundColor = softBlue;

            string periodText = filter.AllBookings
                ? "Period: All bookings"
                : $"Period: {from} to {to}";

            ws.Range(3, 1, 3, totalColumns).Merge();
            ws.Cell(3, 1).Value = periodText;
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Cell(3, 1).Style.Font.FontSize = 11;
            ws.Cell(3, 1).Style.Font.FontColor = mutedText;
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(3, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(3, 1).Style.Fill.BackgroundColor = softYellow;

            ws.Row(1).Height = 32;
            ws.Row(2).Height = 24;
            ws.Row(3).Height = 20;

            // Optional Logo
            try
            {
                var logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    ws.AddPicture(logoPath)
                      .MoveTo(ws.Cell("A1"))
                      .WithSize(84, 84);
                }
            }
            catch
            {
            }

            // HEADER ROW
            int headerRow = 5;
            int c = 1;

            ws.Cell(headerRow, c++).Value = "Company";
            ws.Cell(headerRow, c++).Value = "Customer Name";
            ws.Cell(headerRow, c++).Value = "Customer Phone";

            ws.Cell(headerRow, c++).Value = "Trip Name";
            ws.Cell(headerRow, c++).Value = "Bus";

            ws.Cell(headerRow, c++).Value = "Trip Day";
            ws.Cell(headerRow, c++).Value = "Trip Time";
            ws.Cell(headerRow, c++).Value = "From";
            ws.Cell(headerRow, c++).Value = "To";

            ws.Cell(headerRow, c++).Value = "Customer Dropoff Place";
            ws.Cell(headerRow, c++).Value = "Segment";
            ws.Cell(headerRow, c++).Value = "Seats";
            ws.Cell(headerRow, c++).Value = "Notes";

            if (isAdmin)
                ws.Cell(headerRow, c++).Value = "Ticket Code";

            var lastCol = c - 1;

            var headerRange = ws.Range(headerRow, 1, headerRow, lastCol);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = white;
            headerRange.Style.Fill.BackgroundColor = brandGreen;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.OutsideBorderColor = borderColor;
            headerRange.Style.Border.InsideBorderColor = borderColor;

            ws.Row(headerRow).Height = 24;
            ws.SheetView.FreezeRows(headerRow);

            // DATA ROWS
            int r = headerRow + 1;
            foreach (var x in data)
            {
                int col = 1;

                ws.Cell(r, col++).Value = x.Company;
                ws.Cell(r, col++).Value = x.CustomerName;
                ws.Cell(r, col++).Value = x.Phone;

                ws.Cell(r, col++).Value = x.TripName ?? "";
                ws.Cell(r, col++).Value = x.BusName ?? "";

                ws.Cell(r, col++).Value = x.TripDate ?? "";
                ws.Cell(r, col++).Value = x.TripTime ?? "";
                ws.Cell(r, col++).Value = x.FromCity ?? "";
                ws.Cell(r, col++).Value = x.ToCity ?? "";

                ws.Cell(r, col++).Value = x.CustomerDropoffPlace ?? "";
                ws.Cell(r, col++).Value = x.Segment;
                ws.Cell(r, col++).Value = NormalizeSeats(x.Seats);
                ws.Cell(r, col++).Value = x.Descrption ?? "";

                if (isAdmin)
                    ws.Cell(r, col++).Value = x.TicketCode ?? "";

                var rowRange = ws.Range(r, 1, r, lastCol);
                rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.OutsideBorderColor = borderColor;
                rowRange.Style.Border.InsideBorderColor = borderColor;
                rowRange.Style.Fill.BackgroundColor = (r % 2 == 0) ? XLColor.White : softGray;

                // Segment style
                int segmentCol = 11;
                var segCell = ws.Cell(r, segmentCol);
                if ((x.Segment ?? "").Equals("RETURN", StringComparison.OrdinalIgnoreCase))
                {
                    segCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF4E5");
                    segCell.Style.Font.FontColor = XLColor.FromHtml("#9A3412");
                    segCell.Style.Font.Bold = true;
                    segCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    segCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF2FF");
                    segCell.Style.Font.FontColor = XLColor.FromHtml("#1D4ED8");
                    segCell.Style.Font.Bold = true;
                    segCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Notes style
                int notesCol = 13;
                var notesCell = ws.Cell(r, notesCol);
                notesCell.Style.Font.FontColor = notesRed;
                notesCell.Style.Fill.BackgroundColor = notesSoft;
                notesCell.Style.Alignment.WrapText = true;

                r++;
            }

            int lastDataRow = r - 1;

            // TABLE / FILTER / ALIGNMENTS
            var fullRange = ws.Range(headerRow, 1, lastDataRow, lastCol);
            fullRange.SetAutoFilter();

            ws.Column(1).Width = 18; // Company
            ws.Column(2).Width = 24; // Customer Name
            ws.Column(3).Width = 18; // Phone
            ws.Column(4).Width = 20; // Trip Name
            ws.Column(5).Width = 24; // Bus
            ws.Column(6).Width = 14; // Trip Day
            ws.Column(7).Width = 12; // Trip Time
            ws.Column(8).Width = 16; // From
            ws.Column(9).Width = 16; // To
            ws.Column(10).Width = 28; // Dropoff
            ws.Column(11).Width = 12; // Segment
            ws.Column(12).Width = 16; // Seats
            ws.Column(13).Width = 34; // Notes
            if (isAdmin)
                ws.Column(14).Width = 18; // Ticket Code

            ws.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (isAdmin)
                ws.Column(14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(1, 1, lastDataRow, lastCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Notes wrap
            ws.Column(13).Style.Alignment.WrapText = true;

            // Smooth row heights
            for (int row = headerRow + 1; row <= lastDataRow; row++)
                ws.Row(row).Height = 22;

            // Add bottom summary
            int summaryRow = lastDataRow + 2;
            ws.Range(summaryRow, 1, summaryRow, lastCol).Merge();
            ws.Cell(summaryRow, 1).Value = $"Total exported rows: {data.Count}";
            ws.Cell(summaryRow, 1).Style.Font.Bold = true;
            ws.Cell(summaryRow, 1).Style.Font.FontColor = darkText;
            ws.Cell(summaryRow, 1).Style.Fill.BackgroundColor = softGreen;
            ws.Cell(summaryRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(summaryRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // whole used area border
            ws.Range(1, 1, summaryRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, summaryRow, lastCol).Style.Border.OutsideBorderColor = borderColor;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            var fileName = BuildExcelName_NoDay(filter, from, to);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string BuildExcelName_NoDay(DashboardFilterModel filter, string? from, string? to)
        {
            if (filter.AllBookings) return $"Dashboard_Bookings_All_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to) && from == to)
                return $"Dashboard_Bookings_{from}_({DateTime.Now:HHmm}).xlsx";

            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                return $"Dashboard_Bookings_{from}_to_{to}_({DateTime.Now:HHmm}).xlsx";

            return $"Dashboard_Bookings_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }

        //---------------------------------------------------------------------------------------//
        ////////////////////////////////// SEAT PARSING HELPERS ///////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static IEnumerable<string> SplitSeats(string? seatsText)
        {
            if (string.IsNullOrWhiteSpace(seatsText)) yield break;

            // supports "A1,A2" or "A1 A2" or JSON-ish "[A1,A2]" (best effort)
            var s = seatsText.Trim()
                .Trim('[', ']', '{', '}', '"');

            var parts = s.Split(new[] { ',', ';', ' ', '|', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var seat = p.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(seat))
                    yield return seat;
            }
        }

        private static string NormalizeSeats(string? seatsText)
        {
            var list = SplitSeats(seatsText).ToList();
            return list.Count == 0 ? "" : string.Join(", ", list);
        }

        //---------------------------------------------------------------------------------------//
        //////////////////////////////////// DELETE BY CODE ///////////////////////////////////////
        //---------------------------------------------------------------------------------------//

        private static DateTime CairoNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.Now;
            }
        }

        private static DateTime? ParseTripDepartDateTime(string? departDate, string? departTime)
        {
            if (string.IsNullOrWhiteSpace(departDate) || string.IsNullOrWhiteSpace(departTime))
                return null;

            var s = $"{departDate.Trim()} {departTime.Trim()}";
            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            // fallback
            if (DateTime.TryParse(s, out dt))
                return dt;

            return null;
        }

        private static DateTime? GetDeleteDeadline(Trip? mainTrip, Trip? returnTrip)
        {
            var d1 = mainTrip == null ? (DateTime?)null : ParseTripDepartDateTime(mainTrip.DepartDate, mainTrip.DepartTime);
            var d2 = returnTrip == null ? (DateTime?)null : ParseTripDepartDateTime(returnTrip.DepartDate, returnTrip.DepartTime);

            DateTime? earliest = null;
            if (d1.HasValue && d2.HasValue) earliest = (d1.Value <= d2.Value) ? d1.Value : d2.Value;
            else if (d1.HasValue) earliest = d1.Value;
            else if (d2.HasValue) earliest = d2.Value;

            if (!earliest.HasValue) return null;

            return earliest.Value.AddDays(-1);
        }

        [HttpGet]
        public async Task<IActionResult> PreviewDeleteTicket(string? code)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            code = (code ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new DeleteTicketPreviewModel
                {
                    Found = false,
                    CanDelete = false,
                    ErrorMessage = "Please enter the ticket code."
                });
            }

            var codeRow = await _db.BookingCodes
                .Include(x => x.Booking)
                    .ThenInclude(b => b.Trip)
                .Include(x => x.Booking)
                    .ThenInclude(b => b.ReturnTrip)
                .FirstOrDefaultAsync(x => x.Code.ToUpper() == code);

            if (codeRow == null || codeRow.Booking == null)
            {
                return Json(new DeleteTicketPreviewModel
                {
                    Found = false,
                    CanDelete = false,
                    ErrorMessage = "Ticket code not found."
                });
            }

            var booking = codeRow.Booking;

            if (!isAdmin && !string.Equals(booking.CompanyFrom ?? "", username ?? "", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new DeleteTicketPreviewModel
                {
                    Found = false,
                    CanDelete = false,
                    ErrorMessage = "You don’t have access to this ticket."
                });
            }

            var deadline = GetDeleteDeadline(booking.Trip, booking.ReturnTrip);
            var now = CairoNow();

            bool canDelete = true;
            string? msg = null;

            if (!isAdmin && deadline.HasValue && now >= deadline.Value)
            {
                canDelete = false;
                msg = $"Delete not allowed. You can delete only before: {deadline.Value:yyyy/MM/dd HH:mm} (Cairo).";
            }

            string bookingTypeText = booking.BookingTypeInt switch
            {
                1 => "GO",
                2 => "RETURN",
                3 => "ROUND",
                _ => "-"
            };

            var mainFrom = booking.Trip?.FromCity ?? "-";
            var mainTo = booking.Trip?.ToCity ?? "-";
            var returnFrom = booking.ReturnTrip?.FromCity ?? "-";
            var returnTo = booking.ReturnTrip?.ToCity ?? "-";

            var mainRoute = booking.BookingTypeInt == 2
                ? $"{mainTo} → {mainFrom}"
                : $"{mainFrom} → {mainTo}";

            var returnRoute = booking.ReturnTrip != null
                ? (booking.BookingTypeInt == 3
                    ? $"{returnTo} → {returnFrom}"
                    : $"{returnFrom} → {returnTo}")
                : null;

            return Json(new DeleteTicketPreviewModel
            {
                Found = true,
                CanDelete = canDelete,
                ErrorMessage = msg,

                Code = codeRow.Code,
                BookingId = booking.Id,

                CustomerName = booking.CustomerName,
                Phone = booking.Phone,
                CompanyFrom = booking.CompanyFrom,

                BookingType = bookingTypeText,
                MainSeats = NormalizeSeats(booking.SeatsText),
                ReturnSeats = NormalizeSeats(booking.SeatsReturnText),

                MainTripName = booking.Trip?.TripName,
                MainTripDate = booking.Trip?.DepartDate,
                MainTripTime = booking.Trip?.DepartTime,
                MainRoute = mainRoute,

                ReturnTripName = booking.ReturnTrip?.TripName,
                ReturnTripDate = booking.ReturnTrip?.DepartDate,
                ReturnTripTime = booking.ReturnTrip?.DepartTime,
                ReturnRoute = returnRoute,

                PaidAmount = booking.PaidAmount,
                TotalAmount = booking.TotalAmount,

                Notes = string.IsNullOrWhiteSpace(booking.Notes) ? "لا توجد ملاحظات" : booking.Notes,
                DeleteDeadlineText = deadline.HasValue ? deadline.Value.ToString("yyyy/MM/dd HH:mm") + " (Cairo)" : "-"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTicketByCode(string? code, [FromQuery] DashboardFilterModel filter)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            code = (code ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["DashError"] = "Please enter the ticket code.";
                return RedirectToAction("Index", filter);
            }

            var codeRow = await _db.BookingCodes
                .Include(x => x.Booking)
                    .ThenInclude(b => b.Trip)
                .Include(x => x.Booking)
                    .ThenInclude(b => b.ReturnTrip)
                .FirstOrDefaultAsync(x => x.Code.ToUpper() == code);

            if (codeRow == null || codeRow.Booking == null)
            {
                TempData["DashError"] = "Ticket code not found.";
                return RedirectToAction("Index", filter);
            }

            var booking = codeRow.Booking;

            if (!isAdmin)
            {
                if (!string.Equals(booking.CompanyFrom ?? "", username ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["DashError"] = "You don’t have access to delete this ticket.";
                    return RedirectToAction("Index", filter);
                }
            }

            var deadline = GetDeleteDeadline(booking.Trip, booking.ReturnTrip);
            if (deadline.HasValue)
            {
                var now = CairoNow();
                if (!isAdmin && now >= deadline.Value)
                {
                    TempData["DashError"] = $"Delete not allowed. You can delete only before: {deadline.Value:yyyy/MM/dd HH:mm} (Cairo).";
                    return RedirectToAction("Index", filter);
                }
            }

            try
            {
                var userId = GetCurrentUserId();

                var deleted = new DeletedTicket
                {
                    BookingId = booking.Id,
                    TicketCode = codeRow.Code,

                    TripId = booking.TripId,
                    ReturnTripId = booking.ReturnTripId,

                    CustomerName = booking.CustomerName,
                    Phone = booking.Phone,
                    CompanyFrom = booking.CompanyFrom!,
                    Notes = booking.Notes,

                    SeatsText = booking.SeatsText,
                    SeatsReturnText = booking.SeatsReturnText,
                    BookingTypeInt = booking.BookingTypeInt,

                    PaidAmount = booking.PaidAmount,
                    TotalAmount = booking.TotalAmount,

                    DestinationPlaceName = booking.DestinationPlaceName,
                    ReturnDestinationPlaceName = booking.ReturnDestinationPlaceName,

                    CreatedAtUnix = booking.CreatedAtUnix,
                    DeletedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    DeletedByUserId = userId == Guid.Empty ? null : userId,
                    DeleteReason = $"Deleted from dashboard by code {code}.",

                    TripName = booking.Trip?.TripName,
                    TripDepartDate = booking.Trip?.DepartDate,
                    TripDepartTime = booking.Trip?.DepartTime,
                    TripFromCity = booking.Trip?.FromCity,
                    TripToCity = booking.Trip?.ToCity,

                    ReturnTripName = booking.ReturnTrip?.TripName,
                    ReturnTripDepartDate = booking.ReturnTrip?.DepartDate,
                    ReturnTripDepartTime = booking.ReturnTrip?.DepartTime,
                    ReturnTripFromCity = booking.ReturnTrip?.FromCity,
                    ReturnTripToCity = booking.ReturnTrip?.ToCity
                };

                _db.DeletedTickets.Add(deleted);

                if (codeRow != null)
                    _db.BookingCodes.Remove(codeRow);

                _db.Bookings.Remove(booking);

                await _db.SaveChangesAsync();

                await AddAuditAsync(
                    userId,
                    "delete_ticket",
                    "deleted_tickets",
                    deleted.Id.ToString(),
                    $"Deleted ticket code {code} for customer {booking.CustomerName}."
                );

                TempData["DashOk"] = $"Ticket deleted successfully (CODE: {code}).";
                return RedirectToAction("Index", filter);
            }
            catch
            {
                TempData["DashError"] = "Database error while deleting ticket. Please try again.";
                return RedirectToAction("Index", filter);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DeletedTickets(string? q = null)
        {
            var isAdmin = IsAdmin();
            var username = GetCurrentUsername();

            q = (q ?? "").Trim();

            var query = _db.DeletedTickets
                .AsNoTracking()
                .Include(x => x.DeletedByUser)
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(x => x.CompanyFrom == username);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    (x.CustomerName ?? "").Contains(q) ||
                    (x.Phone ?? "").Contains(q) ||
                    (x.TicketCode ?? "").Contains(q) ||
                    (x.CompanyFrom ?? "").Contains(q) ||
                    (x.TripName ?? "").Contains(q));
            }

            var rows = await query
                .OrderByDescending(x => x.DeletedAtUnix)
                .ToListAsync();

            return View("~/Views/Dashboard/DeletedTickets.cshtml", rows);
        }
    }
}