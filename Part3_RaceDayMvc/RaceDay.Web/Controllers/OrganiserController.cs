using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = Roles.Organiser)]
public class OrganiserController(IRaceDayApi api) : Controller
{
    /// <summary>Summary of all my events, enrolments per event and upcoming dates.</summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var events = await api.GetMyEventsAsync();
        var now = DateTime.UtcNow;
        return View(new DashboardViewModel
        {
            Events = events.OrderBy(e => e.EventDate).ToList(),
            Upcoming = events.Where(e => e.EventDate > now).OrderBy(e => e.EventDate).ToList(),
            TotalEnrolments = events.Sum(e => e.EnrolmentCount)
        });
    }
}
