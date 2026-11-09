using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Participant-only pages: my enrolments and my results.</summary>
[SessionAuthorize(Role = Roles.Participant)]
public class ParticipantController(IRaceDayApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Enrolments()
    {
        var list = await api.GetMyEnrolmentsAsync();
        return View(list.OrderBy(e => e.EventDate).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        try
        {
            await api.WithdrawAsync(id);
            TempData["Success"] = "Your enrolment has been withdrawn.";
        }
        catch (ApiException ex) when (ex.StatusCode is not (401 or 403))
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Enrolments));
    }

    [HttpGet]
    public async Task<IActionResult> Results()
    {
        var list = await api.GetMyResultsAsync();
        return View(list.OrderByDescending(r => r.EventDate).ToList());
    }
}
