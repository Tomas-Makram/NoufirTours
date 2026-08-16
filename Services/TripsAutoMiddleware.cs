namespace NoufirTours.Services
{
    public sealed class TripsAutoMiddleware
    {
        private readonly RequestDelegate _next;

        public TripsAutoMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICheckRunnerToday gate)
        {
            await gate.EnsureRanTodayAsync(context.RequestAborted);
            await _next(context);
        }
    }
}