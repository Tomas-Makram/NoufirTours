using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace NoufirTours.Services
{
    public class RedirectIfAuthenticatedMiddleware
    {
        private readonly RequestDelegate _next;

        public RedirectIfAuthenticatedMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path;

            if (context.User?.Identity?.IsAuthenticated == true &&
                path.StartsWithSegments("/auth/login"))
            {
                context.Response.Redirect("/Home/Index");
                return;
            }

            await _next(context);
        }
    }
}