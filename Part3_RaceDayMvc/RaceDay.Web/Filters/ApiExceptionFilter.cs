using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Turns API failures that a controller did not handle itself into friendly pages:
/// 401 -> back to login, 403 -> access denied, anything else -> a clean error page (never a stack trace).
/// </summary>
public class ApiExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<ApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException ex) return;

        var http = context.HttpContext;
        var tempData = tempDataFactory.GetTempData(http);

        switch (ex.StatusCode)
        {
            case 401:
                http.Session.Clear();
                tempData["Error"] = "Your session has ended. Please log in again.";
                var returnUrl = http.Request.Method == HttpMethods.Get ? http.Request.Path + http.Request.QueryString : null;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                break;
            case 403:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                break;
            default:
                logger.LogWarning("API call failed with status {Status}: {Message}", ex.StatusCode, ex.Message);
                tempData["ErrorDetail"] = ex.Message;
                context.Result = new RedirectToActionResult("Failure", "Error", new { status = ex.StatusCode });
                break;
        }

        context.ExceptionHandled = true;
    }
}
