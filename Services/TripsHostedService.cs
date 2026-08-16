using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NoufirTours.Services
{
    public sealed class AutoTripPlannerOptions
    {
        public bool Enabled { get; set; } = true;

        // time in Cairo
        public int RunHour { get; set; } = 5;
        public int RunMinute { get; set; } = 0;

        // Linux: Africa/Cairo, Windows: Egypt Standard Time
        public string TimeZoneId { get; set; } = "Africa/Cairo";
    }
    
    public sealed class TripsHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<AutoTripPlannerOptions> _opt;

        public TripsHostedService(IServiceScopeFactory scopeFactory, IOptionsMonitor<AutoTripPlannerOptions> opt)
        {
            _scopeFactory = scopeFactory;
            _opt = opt;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _opt.CurrentValue;

                if (!options.Enabled)
                {
                    // sleep a bit and re-check
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var delay = GetDelayUntilNextRun(options);
                if (delay < TimeSpan.Zero) delay = TimeSpan.FromSeconds(10);

                await Task.Delay(delay, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<ICheckRunnerToday>();
                await gate.EnsureRanTodayAsync(stoppingToken);

                // then loop to schedule the next run
            }
        }

        private static TimeSpan GetDelayUntilNextRun(AutoTripPlannerOptions opt)
        {
            var tz = ResolveTimeZone(opt.TimeZoneId);

            var nowUtc = DateTimeOffset.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

            var next = new DateTimeOffset(
                nowLocal.Year, nowLocal.Month, nowLocal.Day,
                opt.RunHour, opt.RunMinute, 0,
                nowLocal.Offset
            );

            if (nowLocal >= next)
                next = next.AddDays(1);

            return next.ToUniversalTime() - nowUtc;
        }

        private static TimeZoneInfo ResolveTimeZone(string id)
        {
            // try exact
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch
            {
                // fallback between linux/windows common IDs for Cairo
                try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); } catch { }
                return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            }
        }
    }
}