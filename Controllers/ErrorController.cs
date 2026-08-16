using Microsoft.AspNetCore.Mvc;

namespace NoufirTours.Controllers
{
    public class ErrorController : Controller
    {
        // /Error/404
        [Route("Error/404")]
        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }

        // /Error/Status/404 أو /Error/Status/500
        [Route("Error/Status/{code:int}")]
        public IActionResult Status(int code)
        {
            Response.StatusCode = code;
            if (code == 404) return View("NotFound");
            return View("Status");
        }
    }
}