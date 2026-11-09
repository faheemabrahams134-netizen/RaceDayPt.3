using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser: see who has enrolled for an event and confirm enrolments.</summary>
[SessionAuthorize(Role = Roles.Organiser)]
public class EnrolmentsController(IRaceDayApi api, UserContext me) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await api.GetEventAsync(eventId);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");

        var list = await api.GetEnrolmentsForEventAsync(eventId);
        return View(new EnrolmentsPageViewModel
        {
            Event = ev,
            Enrolments = list.OrderBy(e => e.CategoryName).ThenBy(e => e.ParticipantName).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int eventId, int enrolmentId, EnrolmentStatus status)
    {
        try
        {
            var updated = await api.SetEnrolmentStatusAsync(enrolmentId, status);
            TempData["Success"] = $"{updated.ParticipantName} is now {updated.Status}.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
