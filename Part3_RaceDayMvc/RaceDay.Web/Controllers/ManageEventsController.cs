using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser: create, edit and delete my events (including the banner image).</summary>
[SessionAuthorize(Role = Roles.Organiser)]
public class ManageEventsController(IRaceDayApi api, UserContext me) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var events = await api.GetMyEventsAsync();
        return View(events.OrderBy(e => e.EventDate).ToList());
    }

    // ------------------------------------------------------------ create

    [HttpGet]
    public IActionResult Create() =>
        View("Form", new EventFormViewModel { EventDate = DateTime.UtcNow.Date.AddDays(30).AddHours(7) });

    [HttpPost]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("Form", model);

        try
        {
            var created = await api.CreateEventAsync(model.Name.Trim(), model.Description.Trim(), model.EventDate!.Value,
                model.Location.Trim(), model.DistanceKm!.Value, model.EventType!.Value);

            if (HasFile(model.Banner))
            {
                try
                {
                    await api.UploadBannerAsync(created.EventId, model.Banner!);
                }
                catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
                {
                    TempData["Warning"] = $"\"{created.Name}\" was created, but the banner could not be uploaded: {ex.Message}";
                    return RedirectToAction("Index", "Categories", new { eventId = created.EventId });
                }
            }

            TempData["Success"] = $"\"{created.Name}\" has been created. Add its categories next.";
            return RedirectToAction("Index", "Categories", new { eventId = created.EventId });
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    // ------------------------------------------------------------ edit

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await api.GetEventAsync(id);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");

        return View("Form", new EventFormViewModel
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            ExistingBannerVersion = ev.BannerVersion
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("Form", model);

        try
        {
            var updated = await api.UpdateEventAsync(id, model.Name.Trim(), model.Description.Trim(), model.EventDate!.Value,
                model.Location.Trim(), model.DistanceKm!.Value, model.EventType!.Value);

            if (HasFile(model.Banner))
            {
                try
                {
                    await api.UploadBannerAsync(id, model.Banner!);
                }
                catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
                {
                    TempData["Warning"] = $"\"{updated.Name}\" was saved, but the banner could not be uploaded: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["Success"] = $"\"{updated.Name}\" has been updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    // ------------------------------------------------------------ delete

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await api.GetEventAsync(id);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");
        return View(ev);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await api.DeleteEventAsync(id);
            TempData["Success"] = "The event has been deleted.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            // e.g. 409: the event already has enrolments.
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------ helpers

    private static bool HasFile(IFormFile? file) => file is { Length: > 0 };

    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is { Length: > MaxImageBytes })
            ModelState.AddModelError(nameof(model.Banner), "The banner image must be 5 MB or smaller.");
    }
}
