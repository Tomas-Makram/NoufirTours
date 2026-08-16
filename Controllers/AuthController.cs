using Microsoft.AspNetCore.Mvc;
using NoufirTours.Models;
using NoufirTours.Models.Auth;
using NoufirTours.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace NoufirTours.Controllers
{
    public class AuthController : Controller
    {
        private readonly DBContext _db; // Connect with database
        private readonly DataHasher _passwordHasher; // Using Hashing Code
        private readonly IDataCiphers _dataProtection; // Using En/De Cryption Data
        private readonly ILogger<AuthController> _logger; // Using Validate AntiForgery Token Key
        private readonly IMemoryCache _cache;

        public AuthController(DBContext db, DataHasher passwordHasher, IDataCiphers dataProtection, ILogger<AuthController> logger, IMemoryCache cache)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _dataProtection = dataProtection;
            _logger = logger;
            _cache = cache;
        }

        //=============================================================================

        private static readonly TimeZoneInfo CairoTz = GetCairoTimeZone();

        private static TimeZoneInfo GetCairoTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
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

        private static string GetClientIp(HttpContext http)
        {
            var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return http.Connection.RemoteIpAddress?.ToString() ?? "";
        }

        private async Task WriteAuditAsync(Guid userId, string action, string entity, Guid? entityId = null, object? detailsObj = null, CancellationToken ct = default)
        {
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var nowCairo = TimeZoneInfo.ConvertTime(nowUtc, CairoTz);

                var details = detailsObj == null ? null : AuditJson(detailsObj);

                await _db.Set<NoufirTours.Data.AuditLog>().AddAsync(new NoufirTours.Data.AuditLog
                {
                    UserId = userId,
                    Action = (action ?? "").Trim(),
                    Entity = (entity ?? "").Trim(),
                    EntityId = entityId.ToString(),
                    Details = details,
                    CreatedAtUnix = nowUtc.ToUnixTimeSeconds()
                }, ct);

                await _db.SaveChangesAsync(ct);
            }
            catch
            {
            }
        }

        //=============================================================================

        private sealed class LoginThrottleState
        {
            public int FailCount { get; set; }
            public DateTimeOffset? LockedUntilUtc { get; set; }
        }

        private static string NormalizeUserKey(string? u)
            => (u ?? "").Trim().ToLowerInvariant();

        private string ThrottleKey(string username)
        {
            // IP + username to avoid hurting everyone behind same IP only
            var ip = GetClientIp(HttpContext);
            return $"login:{ip}:{NormalizeUserKey(username)}";
        }

        private static TimeSpan ComputeFailDelay(int failCount)
        {
            // Exponential backoff with cap (and jitter)
            // 1st fail ~ 400ms, 2nd ~ 800ms, 3rd ~ 1600ms ... capped
            var exp = Math.Min(failCount, 5);          // cap exponent
            var ms = 400 * (1 << exp);                 // 400,800,1600,3200,6400,12800
            ms = Math.Min(ms, 8000);                   // cap to 8s

            // jitter 0..250ms to reduce timing patterns
            var jitter = Random.Shared.Next(0, 251);
            return TimeSpan.FromMilliseconds(ms + jitter);
        }

        private async Task<(bool allowed, string? reason)> EnforceLoginThrottleAsync(string username, CancellationToken ct = default)
        {
            var key = ThrottleKey(username);

            var state = _cache.Get<LoginThrottleState>(key) ?? new LoginThrottleState();

            // If currently locked
            if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value > DateTimeOffset.UtcNow)
            {
                var secs = (int)Math.Ceiling((state.LockedUntilUtc.Value - DateTimeOffset.UtcNow).TotalSeconds);
                return (false, $"Too many attempts. Try again after {secs} seconds.");
            }

            return (true, null);
        }

        private void RegisterLoginFailure(string username)
        {
            var key = ThrottleKey(username);
            var state = _cache.Get<LoginThrottleState>(key) ?? new LoginThrottleState();

            state.FailCount++;

            if (state.FailCount >= 6)
                state.LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10);

            // Keep state for some time (sliding)
            _cache.Set(key, state, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });
        }

        private void ClearLoginThrottle(string username)
        {
            _cache.Remove(ThrottleKey(username));
        }

        private async Task DelayOnFailureAsync(string username, CancellationToken ct = default)
        {
            var key = ThrottleKey(username);
            var state = _cache.Get<LoginThrottleState>(key) ?? new LoginThrottleState();

            var delay = ComputeFailDelay(state.FailCount);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }

        //=============================================================================

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null, CancellationToken ct = default)
        {
            if (IsLoggedIn())
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(model);

            var username = (model.username ?? "").Trim();
            var password = model.password ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                RegisterLoginFailure(username);
                await DelayOnFailureAsync(username, ct);
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            // Throttle/Lock check
            var (allowed, reason) = await EnforceLoginThrottleAsync(username, ct);
            if (!allowed)
            {
                ModelState.AddModelError("", "Too many login attempts. Please try again later.");
                return View(model);
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);

            if (user == null || user.IsActiveInt != 1)
            {
                RegisterLoginFailure(username);
                await DelayOnFailureAsync(username, ct);

                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            var ok = _passwordHasher.VerifyHashed(password, user.PasswordHash);
            if (!ok)
            {
                RegisterLoginFailure(username);
                await DelayOnFailureAsync(username, ct);

                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            ClearLoginThrottle(username);

            // Session
            HttpContext.Session.SetString("UserID", user.UserID.ToString());
            HttpContext.Session.SetString("UserName", user.FullName ?? user.Username);
            HttpContext.Session.SetString("UserRole", user.RoleText ?? "staff");

            // Cookie Auth
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Username),
                new Claim(ClaimTypes.Role, user.RoleText ?? "staff"),
                new Claim("username", user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var isPersistent = true;
            var expiresUtc = DateTimeOffset.UtcNow.AddMinutes(30);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = isPersistent,
                    ExpiresUtc = expiresUtc,
                    AllowRefresh = true
                });

            await WriteAuditAsync(
                user.UserID,
                action: "login",
                entity: "user",
                entityId: user.UserID,
                detailsObj: new
                {
                    username = user.Username,
                    ip = GetClientIp(HttpContext),
                    userAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                    returnUrl,
                    isPersistent,
                    expiresUtc = expiresUtc.ToString("O")
                },
                ct
            );

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("NoufirTours.Auth");
            Response.Cookies.Delete("NoufirTours.Session");
            return RedirectToAction("Index", "Home");
        }

        private bool IsLoggedIn()
        {
            // Session
            if (HttpContext.Session.GetInt32("UserID").HasValue)
                return true;

            // Cookie
            if (User?.Identity?.IsAuthenticated == true)
                return true;

            return false;
        }
    }
}