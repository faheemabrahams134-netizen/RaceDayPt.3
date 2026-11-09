using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public record SessionUser(int UserId, string FullName, string Email, string Role, string? PictureVersion);

public static class SessionKeys
{
    public const string User = "raceday.user";
    public const string ApiCookie = "raceday.api-cookie";
}

/// <summary>
/// The signed-in user for this request, read from the server-side session.
/// The user stays signed in (with their role) across all pages until they log out or the session expires.
/// </summary>
public class UserContext(IHttpContextAccessor accessor)
{
    private SessionUser? _cached;
    private bool _loaded;

    private ISession? Session => accessor.HttpContext?.Session;

    public SessionUser? Current
    {
        get
        {
            if (_loaded) return _cached;
            var json = Session?.GetString(SessionKeys.User);
            _cached = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SessionUser>(json);
            _loaded = true;
            return _cached;
        }
    }

    public bool IsAuthenticated => Current is not null;
    public bool IsOrganiser => Current?.Role == Roles.Organiser;
    public bool IsParticipant => Current?.Role == Roles.Participant;
    public int UserId => Current?.UserId ?? 0;
    public string FullName => Current?.FullName ?? "";
    public string Role => Current?.Role ?? "";

    /// <summary>Remember this user (and their role) in the session after a successful login or profile change.</summary>
    public void SignIn(UserDto user)
    {
        var current = new SessionUser(user.UserId, user.FullName, user.Email, user.Role, user.ProfilePictureVersion);
        Session?.SetString(SessionKeys.User, JsonSerializer.Serialize(current));
        _cached = current;
        _loaded = true;
    }

    /// <summary>End the session completely (user details and the API cookie).</summary>
    public void SignOut()
    {
        Session?.Clear();
        _cached = null;
        _loaded = true;
    }
}
