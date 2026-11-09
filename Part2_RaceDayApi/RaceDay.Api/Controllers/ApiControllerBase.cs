using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The id of the user stored in the session cookie.</summary>
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected ObjectResult Error(int statusCode, string detail) =>
        Problem(detail: detail, statusCode: statusCode);
}
