using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

/// <summary>Friendly error pages. Open to everyone and never calls the API, so it can never loop.</summary>
[IgnoreAntiforgeryToken]
public class ErrorController : Controller
{
    /// <summary>Unhandled exception: no technical details are shown.</summary>
    [Route("Error")]
    public IActionResult Index() =>
        View("Error", new ErrorViewModel
        {
            Status = 500,
            Title = "Something went wrong",
            Message = "We hit an unexpected problem. Please try again, or head back to the home page."
        });

    /// <summary>404 and other status codes.</summary>
    [Route("Error/Status")]
    public IActionResult Status(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { Status = 404, Title = "Page not found", Message = "We couldn't find the page you were looking for." },
            403 => new ErrorViewModel { Status = 403, Title = "Access denied", Message = "You don't have permission to see that page." },
            _ => new ErrorViewModel { Status = code, Title = "Something went wrong", Message = "That request could not be completed." }
        };
        Response.StatusCode = code;
        return View("Error", model);
    }

    /// <summary>An API call failed in a way the page could not handle; the message was prepared by the API client.</summary>
    public IActionResult Failure(int status)
    {
        var message = TempData["ErrorDetail"] as string ?? "That request could not be completed.";
        var title = status switch
        {
            404 => "We couldn't find that",
            503 or 504 => "RaceDay is unavailable",
            _ => "Something went wrong"
        };
        return View("Error", new ErrorViewModel { Status = status, Title = title, Message = message });
    }
}
