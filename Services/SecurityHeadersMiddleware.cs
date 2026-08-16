using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace NoufirTours.Services
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Generate nonce for each request
            var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            context.Items["CSPNonce"] = nonce;

            context.Response.Headers["Content-Security-Policy"] =
                $"default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
                $"style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                $"img-src 'self' data: https:; " +
                $"font-src 'self' https://cdn.jsdelivr.net; " +
                $"connect-src 'self' https://nominatim.openstreetmap.org; " + //$"connect-src 'self'; " +
                $"frame-ancestors 'none'; " +
                $"base-uri 'self'; " +
                $"object-src 'none'; " +
                $"upgrade-insecure-requests;";

            // Additional security headers
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "geolocation=(self), microphone=()";
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"; // HSTS

            await _next(context);
        }
    }
}