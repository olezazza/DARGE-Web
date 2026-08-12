using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;

public class CultureController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(culture))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true
                }
            );
        }

        string targetUrl = NormalizeReturnUrl(returnUrl);
        return Redirect(targetUrl);
    }

    private string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return User.Identity?.IsAuthenticated == true ? "/Dashboard" : "/";
        }

        // Normalize POST-only endpoints to safe GET pages to prevent 405 Method Not Allowed crashes
        if (returnUrl.Contains("/Assistant/Ask", StringComparison.OrdinalIgnoreCase))
        {
            return "/Assistant";
        }

        if (returnUrl.Contains("/Projects/GeneratePlan", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.Contains("/Projects/CreateFromPlan", StringComparison.OrdinalIgnoreCase))
        {
            return "/Projects/Plan";
        }

        if (returnUrl.Contains("/Projects/AskAi", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.Contains("/Projects/AddTask", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.Contains("/Projects/CompleteTask", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.Contains("/Projects/DeleteTask", StringComparison.OrdinalIgnoreCase) ||
            returnUrl.Contains("/Projects/Delete", StringComparison.OrdinalIgnoreCase))
        {
            // If returnUrl has a projectId parameter, or path like /Projects/Details/5, keep it
            return returnUrl.Contains("/Projects/", StringComparison.OrdinalIgnoreCase) ? returnUrl.Split('?')[0] : "/Projects";
        }

        return returnUrl;
    }
}
