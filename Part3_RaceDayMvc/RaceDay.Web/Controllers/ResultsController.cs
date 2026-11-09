using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser: capture and publish finish times and positions once an event has concluded.</summary>
[SessionAuthorize(Role = Roles.Organiser)]
public class ResultsController(IRaceDayApi api, UserContext me) : Controller
{
    private static readonly Regex TimePattern = new(@"^\d{1,2}:\d{2}:\d{2}$", RegexOptions.Compiled);

    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await api.GetEventAsync(eventId);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");

        var enrolments = await api.GetEnrolmentsForEventAsync(eventId);
        var results = await api.GetResultsForEventAsync(eventId);
        var byEnrolment = results.ToDictionary(r => r.EnrolmentId);

        return View(new ResultsPageViewModel
        {
            Event = ev,
            EventHasHappened = ev.EventDate <= DateTime.UtcNow,
            Rows = enrolments
                .OrderBy(e => e.CategoryName)
                .ThenBy(e => byEnrolment.TryGetValue(e.EnrolmentId, out var r) ? r.FinishPosition : int.MaxValue)
                .ThenBy(e => e.ParticipantName)
                .Select(e => new ResultRow { Enrolment = e, Result = byEnrolment.GetValueOrDefault(e.EnrolmentId) })
                .ToList()
        });
    }

    /// <summary>Saving a row publishes it straight away: participants see it under "My Results".</summary>
    [HttpPost]
    public async Task<IActionResult> Save(int eventId, int enrolmentId, int? resultId, string? finishTime, int? finishPosition)
    {
        var ev = await api.GetEventAsync(eventId);
        if (ev.OrganiserId != me.UserId) return RedirectToAction("AccessDenied", "Account");
        if (ev.EventDate > DateTime.UtcNow)
        {
            TempData["Error"] = "Results can only be captured after the event has taken place.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var time = finishTime?.Trim() ?? "";
        if (!TimePattern.IsMatch(time) || !TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var finish)
            || finish <= TimeSpan.Zero || finish >= TimeSpan.FromHours(24))
        {
            TempData["Error"] = "Enter the finish time as hours:minutes:seconds, for example 0:45:12 or 01:45:12.";
            return RedirectToAction(nameof(Index), new { eventId });
        }
        if (finishPosition is null or < 1 or > 100000)
        {
            TempData["Error"] = "Enter a finishing position of 1 or higher.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        try
        {
            if (resultId is { } existing)
                await api.UpdateResultAsync(existing, finish, finishPosition.Value);
            else
                await api.CreateResultAsync(enrolmentId, finish, finishPosition.Value);
            TempData["Success"] = "Result saved and published.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message; // e.g. that finishing position is already taken in this category
        }
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
