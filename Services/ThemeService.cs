using Microsoft.AspNetCore.Http;

namespace NoufirTours.Services
{
    public interface IThemeService
    {
        bool IsDarkMode(HttpContext context);
        void ToggleTheme(HttpContext context);
        string GetCurrentTheme(HttpContext context);
    }

    public class ThemeService : IThemeService
    {
        private const string ThemeCookieName = "AppTheme";
        private const string DarkMode = "dark";
        private const string LightMode = "light";

        public bool IsDarkMode(HttpContext context)
        {
            var theme = GetCurrentTheme(context);
            return theme == DarkMode;
        }

        public void ToggleTheme(HttpContext context)
        {
            var currentTheme = GetCurrentTheme(context);
            var newTheme = currentTheme == LightMode ? DarkMode : LightMode;

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddYears(1),
                Path = "/"
            };

            context.Response.Cookies.Append(ThemeCookieName, newTheme, cookieOptions);
        }

        public string GetCurrentTheme(HttpContext context)
        {
            if (context == null) return LightMode;

            var theme = context.Request.Cookies[ThemeCookieName];

            if (string.IsNullOrEmpty(theme))
            {
                theme = LightMode;
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddYears(1),
                    Path = "/"
                };
                context.Response.Cookies.Append(ThemeCookieName, theme, cookieOptions);
            }

            return theme;
        }
    }
}