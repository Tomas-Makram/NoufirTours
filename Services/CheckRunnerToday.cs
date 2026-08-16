using Microsoft.EntityFrameworkCore;
using NoufirTours.Data;
using NoufirTours.Models;

namespace NoufirTours.Services
{
    public interface ICheckRunnerToday
    {
        Task EnsureRanTodayAsync(CancellationToken ct = default);
    }

    public sealed class CheckRunnerToday : ICheckRunnerToday
    {
        private readonly DBContext _db;
        private readonly IDailyWork _runner;

        private const string K_LastRunDate = "auto_trip_planner:last_run_date";
        private const string K_IsDone = "auto_trip_planner:is_done";
        private const string K_LastRunUnix = "auto_trip_planner:last_run_unix";

        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static readonly TimeZoneInfo CairoTz = ResolveCairoTimeZone();

        public CheckRunnerToday(DBContext db, IDailyWork runner)
        {
            _db = db;
            _runner = runner;
        }

        public async Task EnsureRanTodayAsync(CancellationToken ct = default)
        {
            var nowCairo = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CairoTz);
            var todayIso = nowCairo.Date.ToString("yyyy-MM-dd");
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _lock.WaitAsync(ct);
            try
            {
                var lastRunDate = await _db.Set<AppSetting>()
                    .FirstOrDefaultAsync(x => x.Key == K_LastRunDate, ct);

                var isDone = await _db.Set<AppSetting>()
                    .FirstOrDefaultAsync(x => x.Key == K_IsDone, ct);

                var lastDateValue = lastRunDate?.Value?.Trim();
                var doneValue = string.Equals(isDone?.Value, "true", StringComparison.OrdinalIgnoreCase);

                if (lastDateValue == todayIso && doneValue)
                    return;

                if (lastDateValue != todayIso)
                {
                    if (lastRunDate == null)
                        _db.Add(new AppSetting { Key = K_LastRunDate, Value = todayIso });
                    else
                        lastRunDate.Value = todayIso;

                    if (isDone == null)
                        _db.Add(new AppSetting { Key = K_IsDone, Value = "false" });
                    else
                        isDone.Value = "false";

                    await _db.SaveChangesAsync(ct);
                }

                await _runner.RunOnceAsync(ct);

                if (isDone == null)
                    _db.Add(new AppSetting { Key = K_IsDone, Value = "true" });
                else
                    isDone.Value = "true";

                var lastUnix = await _db.Set<AppSetting>()
                    .FirstOrDefaultAsync(x => x.Key == K_LastRunUnix, ct);

                if (lastUnix == null)
                    _db.Add(new AppSetting { Key = K_LastRunUnix, Value = nowUnix.ToString() });
                else
                    lastUnix.Value = nowUnix.ToString();

                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static TimeZoneInfo ResolveCairoTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        }
    }
}