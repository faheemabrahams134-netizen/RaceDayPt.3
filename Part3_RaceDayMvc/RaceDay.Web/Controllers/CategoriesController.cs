using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser: add age or distance categories to an event.</summary>
[SessionAuthorize(Role = Roles.Organiser)]
public class CategoriesController(IRaceDayApi api, UserContext me) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await api.GetEventAsync(eventId);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");
        return View(new CategoriesPageViewModel { Event = ev, Form = new CategoryFormViewModel { EventId = eventId } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryFormViewModel form)
    {
        ValidateRules(form);
        if (!ModelState.IsValid) return await RenderIndexAsync(form);

        try
        {
            await api.CreateCategoryAsync(form.EventId, form.Name.Trim(), form.Description, form.CategoryType!.Value,
                form.MinAge, form.MaxAge, form.DistanceKm);
            TempData["Success"] = $"Category \"{form.Name.Trim()}\" added.";
            return RedirectToAction(nameof(Index), new { eventId = form.EventId });
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RenderIndexAsync(form);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var ev = await api.GetEventAsync(eventId);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");

        var cat = ev.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (cat is null) return NotFound();

        return View(new CategoryFormViewModel
        {
            EventId = eventId,
            CategoryId = id,
            Name = cat.Name,
            Description = cat.Description,
            CategoryType = cat.CategoryType,
            MinAge = cat.MinAge,
            MaxAge = cat.MaxAge,
            DistanceKm = cat.DistanceKm
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormViewModel form)
    {
        form.EventId = eventId;
        form.CategoryId = id;
        ValidateRules(form);
        if (!ModelState.IsValid) return View(form);

        try
        {
            await api.UpdateCategoryAsync(id, form.Name.Trim(), form.Description, form.CategoryType!.Value,
                form.MinAge, form.MaxAge, form.DistanceKm);
            TempData["Success"] = $"Category \"{form.Name.Trim()}\" updated.";
            return RedirectToAction(nameof(Index), new { eventId });
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        try
        {
            await api.DeleteCategoryAsync(id);
            TempData["Success"] = "Category deleted.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message; // e.g. the category already has enrolments
        }
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private async Task<IActionResult> RenderIndexAsync(CategoryFormViewModel form)
    {
        var ev = await api.GetEventAsync(form.EventId);
        return View("Index", new CategoriesPageViewModel { Event = ev, Form = form });
    }

    /// <summary>Same rules as the API, so the organiser gets a clear message next to the right field.</summary>
    private void ValidateRules(CategoryFormViewModel f)
    {
        if (f.CategoryType == CategoryType.Age)
        {
            if (f.MinAge is null && f.MaxAge is null)
                ModelState.AddModelError(nameof(f.MinAge), "Enter a minimum age, a maximum age, or both.");
            else if (f.MinAge is not null && f.MaxAge is not null && f.MinAge > f.MaxAge)
                ModelState.AddModelError(nameof(f.MinAge), "The minimum age cannot be greater than the maximum age.");
            f.DistanceKm = null;
        }
        else if (f.CategoryType == CategoryType.Distance)
        {
            if (f.DistanceKm is null)
                ModelState.AddModelError(nameof(f.DistanceKm), "Enter the distance in kilometres.");
            f.MinAge = null;
            f.MaxAge = null;
        }
    }
}
