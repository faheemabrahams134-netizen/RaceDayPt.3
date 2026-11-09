using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a controller or action using the server-side session.
/// Not signed in -> redirected to the login page (and back afterwards).
/// Signed in with the wrong role -> "access denied" page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>Optional: "Organiser" or "Participant". Leave empty to allow any signed-in user.</summary>
    public string? Role { get; set; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<UserContext>();

        if (!user.IsAuthenticated)
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == HttpMethods.Get ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (!string.IsNullOrEmpty(Role) && !string.Equals(user.Role, Role, StringComparison.Ordinal))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
