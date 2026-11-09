using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordService hasher, IConfiguration config) : ApiControllerBase
{
    /// <summary>Register a new Organiser or Participant.</summary>
    /// <remarks>
    /// Participants must supply a date of birth. Organisers must supply the organiser invite code,
    /// so nobody can promote themselves to Organiser just by picking the role.
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req)
    {
        var role = Roles.All.FirstOrDefault(r => r.Equals(req.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null)
            return Error(400, "Role must be either 'Organiser' or 'Participant'.");

        if (role == Roles.Organiser)
        {
            var expected = config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expected) || req.OrganiserCode != expected)
                return Error(403, "A valid organiser invite code is required to register as an Organiser.");
        }
        else
        {
            if (req.DateOfBirth is null)
                return Error(400, "Date of birth is required for Participants.");
        }

        if (req.DateOfBirth is { } dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dob >= today || dob < today.AddYears(-120))
                return Error(400, "Date of birth is not valid.");
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Error(409, "An account with this email already exists.");

        var user = new AppUser
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(req.Password),
            Role = role,
            Phone = req.Phone?.Trim(),
            DateOfBirth = req.DateOfBirth
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Error(409, "An account with this email already exists.");
        }

        return Created("/api/users/me", user.ToDto());
    }

    /// <summary>Log in. Starts a server-issued session (cookie) that stores the user's id and role.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
            return Error(401, "Invalid email or password.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });

        return Ok(user.ToDto());
    }

    /// <summary>Log out and end the current session.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
