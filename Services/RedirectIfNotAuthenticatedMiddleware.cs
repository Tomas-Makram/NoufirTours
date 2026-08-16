using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace NoufirTours.Services
{
    public class RedirectIfNotAuthenticatedMiddleware
    {
        private readonly RequestDelegate _next;

        public RedirectIfNotAuthenticatedMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

            // Static + auth pages (قبل routing)
            if (IsPublicPathByPrefix(path))
            {
                await _next(context);
                return;
            }

            // IMPORTANT: requires UseRouting() before this middleware
            var endpoint = context.GetEndpoint();
            var allowAnon = endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
            if (allowAnon)
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                try { context.Session?.Clear(); } catch { }
                try { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); } catch { }

                context.Response.Redirect("/Auth/Login");
                return;
            }

            await _next(context);
        }

        private static bool IsPublicPathByPrefix(string path)
        {
            if (path.StartsWith("/auth/login")) return true;
            if (path.StartsWith("/auth/logout")) return true;

            // Static files
            if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
                path.StartsWith("/images") || path.StartsWith("/favicon") || path.StartsWith("/uploads"))
                return true;

            if (path.StartsWith("/swagger")) return true;

            return false;
        }
    }
}