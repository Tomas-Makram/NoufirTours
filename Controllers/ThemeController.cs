using NoufirTours.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NoufirTours.Controllers
{
    [AllowAnonymous]
    public class ThemeController : Controller
    {
        private readonly IThemeService _themeService;

        public ThemeController(IThemeService themeService)
        {
            _themeService = themeService;
        }

        [HttpPost]
        public IActionResult ToggleTheme()
        {
            _themeService.ToggleTheme(HttpContext);

            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpGet]
        public IActionResult GetCurrentTheme()
        {
            var theme = _themeService.GetCurrentTheme(HttpContext);
            return Json(new { theme = theme });
        }
    }
}