using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Event details (both roles) and enrolling (Participants).</summary>
[SessionAuthorize]
public class EventsController(IRaceDayApi api, UserContext me) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await api.GetEventAsync(id);
        var model = new EventDetailsViewModel
        {
            Event = ev,
            IsPast = ev.EventDate <= DateTime.UtcNow
        };

        if (me.IsParticipant)
        {
            var mine = await api.GetMyEnrolmentsAsync();
            model.MyEnrolment = mine.FirstOrDefault(e => e.EventId == id);

            var profile = await api.GetMeAsync();
            model.HasDateOfBirth = profile.DateOfBirth is not null;
            if (profile.DateOfBirth is { } dob)
            {
                var on = DateOnly.FromDateTime(ev.EventDate);
                var age = on.Year - dob.Year;
                if (on < dob.AddYears(age)) age--;
                model.MyAge = age;
            }
        }

        if (model.IsPast) model.Results = await api.GetResultsForEventAsync(id);
        return View(model);
    }

    [HttpPost]
    [SessionAuthorize(Role = Roles.Participant)]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            TempData["Error"] = "Please choose a category before enrolling.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        try
        {
            var enrolment = await api.EnrolAsync(eventId, categoryId.Value);
            TempData["Success"] = $"You're in! You have been enrolled for {enrolment.EventName} ({enrolment.CategoryName}). The organiser will confirm your spot.";
            return RedirectToAction("Enrolments", "Participant");
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = eventId });
        }
    }
}
