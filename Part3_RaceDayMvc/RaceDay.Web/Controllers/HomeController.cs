using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController(IRaceDayApi api, UserContext me) : Controller
{
    /// <summary>
    /// Visitors see the landing page, Participants see upcoming events (with filters),
    /// Organisers go to their dashboard.
    /// </summary>
    public async Task<IActionResult> Index([FromQuery] EventFilter filter)
    {
        if (!me.IsAuthenticated) return View("Landing");
        if (me.IsOrganiser) return RedirectToAction("Dashboard", "Organiser");

        // "Upcoming" by default: start from today unless the participant picks an earlier date.
        var from = filter.From ?? DateTime.UtcNow.Date;
        DateTime? to = filter.To is { } t ? t.Date.AddDays(1).AddSeconds(-1) : null;

        var events = await api.GetEventsAsync(filter.Type, from, to);
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            events = events.Where(e =>
                e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var mine = await api.GetMyEnrolmentsAsync();
        return View(new HomeViewModel
        {
            Events = events,
            EnrolledEventIds = mine.Select(m => m.EventId).ToHashSet(),
            Filter = filter
        });
    }
}
