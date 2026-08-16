using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

public class RequireUserIdAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;

        var idStr = http.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(idStr))
            idStr = http.Session?.GetString("UserID");

        if (!Guid.TryParse(idStr, out var id) || id == Guid.Empty)
        {
            var returnUrl = http.Request.Path + http.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl });
            return;
        }

        base.OnActionExecuting(context);
    }
}