using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;
using NoufirTours.Services;
using System.Security.Claims;

int TimeOut = 10; // Minutes
bool ifRequestSend = true;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews().AddSessionStateTempDataProvider();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Thems
builder.Services.AddSingleton<IThemeService, ThemeService>();

// Password Hasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Location Service (HttpClient)
builder.Services.AddHttpClient<ILocationService, LocationService>();

// Database
builder.Services.AddDbContext<DBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Connection"))
);

// Session
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "NoufirTours.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(TimeOut);
});

// Data Protection (Persist Keys)
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .SetApplicationName("NoufirTours")
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder));

// Authentication (Cookies)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Home/Error";

        options.Cookie.Name = "NoufirTours.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(TimeOut);
        options.SlidingExpiration = ifRequestSend;

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (!ifRequestSend)
                {
                    var issuedUtc = context.Properties.IssuedUtc;
                    if (issuedUtc.HasValue)
                    {
                        var absoluteExpiry = issuedUtc.Value.AddMinutes(TimeOut);

                        if (DateTimeOffset.UtcNow >= absoluteExpiry)
                        {
                            context.RejectPrincipal();

                            await context.HttpContext.SignOutAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme);

                            context.HttpContext.Session.Clear();

                            context.HttpContext.Response.Cookies.Delete("NoufirTours.Auth");
                            context.HttpContext.Response.Cookies.Delete("NoufirTours.Session");
                        }
                    }
                }

                await Task.CompletedTask;
            }
        };
    });

// Custom Services
builder.Services.AddScoped<IDataCiphers, DataCiphers>();
builder.Services.AddScoped<DataHasher, PasswordHasherService>();

// Services Run Automatic Trips Planning & Settings
builder.Services.Configure<AutoTripPlannerOptions>(opt =>
{
    opt.Enabled = true;
    opt.RunHour = 0;
    opt.RunMinute = 0;
    opt.TimeZoneId = "Africa/Cairo";
});

builder.Services.AddScoped<IDailyWork, DailyWork>();
builder.Services.AddScoped<ICheckRunnerToday, CheckRunnerToday>();

builder.Services.AddHostedService<TripsHostedService>();

var app = builder.Build();

// Test appSettings
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DBContext>();

    var todayIso = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var lastDate = await db.Set<AppSetting>()
        .FirstOrDefaultAsync(x => x.Key == "auto_trip_planner:last_run_date");

    if (lastDate == null)
    {
        db.Add(new AppSetting
        {
            Key = "auto_trip_planner:last_run_date",
            Value = "2000-01-01"
        });
    }
    else
    {
        lastDate.Value = "2000-01-01";
    }

    var oldUnix = new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        .ToUnixTimeSeconds();

    var plans = await db.AutoTripPlans.ToListAsync();

    foreach (var plan in plans)
    {
        plan.UpdatedAtUnix = oldUnix;
        plan.isDone = false;
    }

    await db.SaveChangesAsync();
}

await DbSeeder.SeedAdminAsync(app.Services);

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// Routing Secure

// Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        if (string.IsNullOrEmpty(context.Session.GetString("UserID")))
        {
            var idClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(idClaim))
                context.Session.SetString("UserID", idClaim);

            context.Session.SetString("UserName", context.User.Identity?.Name ?? "User");
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? "staff";
            context.Session.SetString("UserRole", role);
        }
    }
    await next();
});

// Middleware Security Headers (CSP + Routing)
app.UseMiddleware<RedirectIfAuthenticatedMiddleware>();
app.UseMiddleware<RedirectIfNotAuthenticatedMiddleware>();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<TripsAutoMiddleware>();

app.UseStatusCodePagesWithReExecute("/Error/Status/{0}");

app.UseExceptionHandler("/Error/Status/500");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();