using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController(IRaceDayApi api, UserContext me) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    // ------------------------------------------------------------ login / register / logout

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (me.IsAuthenticated) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            // Start from a clean session, then let the API start its own session (cookie) for this user.
            HttpContext.Session.Clear();
            var user = await api.LoginAsync(model.Email.Trim(), model.Password);
            me.SignIn(user);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Password = "";
            return View(model);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (me.IsAuthenticated) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != Roles.Organiser && model.Role != Roles.Participant)
            ModelState.AddModelError(nameof(model.Role), "Choose Participant or Organiser.");
        if (model.Role == Roles.Participant && model.DateOfBirth is null)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Participants must enter their date of birth.");
        if (model.Role == Roles.Organiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need the invite code.");
        if (model.DateOfBirth is { } dob && dob >= DateOnly.FromDateTime(DateTime.UtcNow))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Your date of birth must be in the past.");

        if (!ModelState.IsValid) return View(model);

        try
        {
            await api.RegisterAsync(model.FullName.Trim(), model.Email.Trim(), model.Password, model.Role,
                model.Phone, model.DateOfBirth,
                model.Role == Roles.Organiser ? model.OrganiserCode?.Trim() : null);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        // Account created: sign the new user straight in.
        try
        {
            HttpContext.Session.Clear();
            var user = await api.LoginAsync(model.Email.Trim(), model.Password);
            me.SignIn(user);
            TempData["Success"] = $"Welcome to RaceDay, {user.FullName}!";
            return RedirectToAction("Index", "Home");
        }
        catch (ApiException)
        {
            TempData["Success"] = "Your account has been created. Please log in.";
            return RedirectToAction(nameof(Login));
        }
    }

    [HttpPost]
    [SessionAuthorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await api.LogoutAsync();
        }
        catch (ApiException)
        {
            // Even if the API is unreachable the local session must still end.
        }

        me.SignOut();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ------------------------------------------------------------ profile

    [HttpGet]
    [SessionAuthorize]
    public async Task<IActionResult> Profile()
    {
        var user = await api.GetMeAsync();
        me.SignIn(user); // refresh the cached name / picture
        return View(ToViewModel(user));
    }

    [HttpPost]
    [SessionAuthorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        model.Email = me.Current?.Email;
        model.Role = me.Role;
        model.PictureVersion = me.Current?.PictureVersion;

        if (me.IsParticipant && model.DateOfBirth is null)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Participants must have a date of birth (it is used to check age categories).");
        if (model.DateOfBirth is { } dob && dob >= DateOnly.FromDateTime(DateTime.UtcNow))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Your date of birth must be in the past.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await api.UpdateMeAsync(model.FullName.Trim(), model.Phone, model.DateOfBirth);
            me.SignIn(user);
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [SessionAuthorize]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData["Error"] = "Choose an image to upload first.";
            return RedirectToAction(nameof(Profile));
        }
        if (picture.Length > MaxImageBytes)
        {
            TempData["Error"] = "That image is too large. Please choose one that is 5 MB or smaller.";
            return RedirectToAction(nameof(Profile));
        }

        try
        {
            var user = await api.UploadProfilePictureAsync(picture);
            me.SignIn(user);
            TempData["Success"] = "Your profile picture has been updated.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Profile));
    }

    private static ProfileViewModel ToViewModel(UserDto user) => new()
    {
        FullName = user.FullName,
        Phone = user.Phone,
        DateOfBirth = user.DateOfBirth,
        Email = user.Email,
        Role = user.Role,
        PictureVersion = user.ProfilePictureVersion
    };
}
