using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ResultsController(AppDbContext db) : ApiControllerBase
{
    private IQueryable<RaceResult> WithDetails() =>
        db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e!.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant);

    /// <summary>Record the finish time and position for an enrolment in one of your events (Organiser only).</summary>
    [HttpPost("enrolments/{enrolmentId:int}/result")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> Create(int enrolmentId, ResultRequest req)
    {
        var enrolment = await db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return Error(404, "Enrolment not found.");
        if (enrolment.Event!.OrganiserId != CurrentUserId)
            return Error(403, "You can only record results for your own events.");

        var problem = ValidateTime(req.FinishTime!.Value);
        if (problem is not null) return Error(400, problem);

        if (await db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return Error(409, "A result has already been recorded for this enrolment.");
        if (await PositionTaken(enrolment.EventId, enrolment.CategoryId, req.FinishPosition!.Value, null))
            return Error(409, "That finishing position is already taken in this category.");

        var result = new RaceResult
        {
            EnrolmentId = enrolmentId,
            FinishTime = req.FinishTime.Value,
            FinishPosition = req.FinishPosition.Value
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(r => r.ResultId == result.ResultId);
        var counts = await FinisherCounts(new[] { enrolment.EventId });
        return Created($"/api/events/{enrolment.EventId}/results", Map(saved, counts));
    }

    /// <summary>Correct a result in one of your events (Organiser only).</summary>
    [HttpPut("results/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> Update(int id, ResultRequest req)
    {
        var result = await db.Results.Include(r => r.Enrolment).ThenInclude(e => e!.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Error(404, "Result not found.");
        if (result.Enrolment!.Event!.OrganiserId != CurrentUserId)
            return Error(403, "You can only change results for your own events.");

        var problem = ValidateTime(req.FinishTime!.Value);
        if (problem is not null) return Error(400, problem);
        if (await PositionTaken(result.Enrolment.EventId, result.Enrolment.CategoryId, req.FinishPosition!.Value, id))
            return Error(409, "That finishing position is already taken in this category.");

        result.FinishTime = req.FinishTime.Value;
        result.FinishPosition = req.FinishPosition.Value;
        await db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(r => r.ResultId == id);
        var counts = await FinisherCounts(new[] { saved.Enrolment!.EventId });
        return Ok(Map(saved, counts));
    }

    /// <summary>Get the results of an event, ordered by category then finishing position (both roles).</summary>
    [HttpGet("events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return Error(404, "Event not found.");
        var list = await WithDetails().Where(r => r.Enrolment!.EventId == eventId).ToListAsync();
        var counts = await FinisherCounts(new[] { eventId });
        return Ok(list.OrderBy(r => r.Enrolment!.CategoryId).ThenBy(r => r.FinishPosition).Select(r => Map(r, counts)));
    }

    /// <summary>Get the logged-in participant's own results (Participant only).</summary>
    [HttpGet("results/mine")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetMine()
    {
        var me = CurrentUserId;
        var list = await WithDetails().Where(r => r.Enrolment!.ParticipantId == me).ToListAsync();
        var counts = await FinisherCounts(list.Select(r => r.Enrolment!.EventId));
        return Ok(list.OrderByDescending(r => r.Enrolment!.Event!.EventDate).Select(r => Map(r, counts)));
    }

    /// <summary>Number of recorded finishers per (event, category), used for "47th out of 312".</summary>
    private async Task<Dictionary<(int EventId, int CategoryId), int>> FinisherCounts(IEnumerable<int> eventIds)
    {
        var ids = eventIds.Distinct().ToList();
        var peers = await db.Results.AsNoTracking().Include(r => r.Enrolment)
            .Where(r => ids.Contains(r.Enrolment!.EventId)).ToListAsync();
        return peers.GroupBy(r => (r.Enrolment!.EventId, r.Enrolment.CategoryId))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static ResultDto Map(RaceResult r, Dictionary<(int EventId, int CategoryId), int> counts) =>
        r.ToDto(counts.GetValueOrDefault((r.Enrolment!.EventId, r.Enrolment.CategoryId)));

    private Task<bool> PositionTaken(int eventId, int categoryId, int position, int? ignoreResultId) =>
        db.Results.AnyAsync(r => r.Enrolment!.EventId == eventId
                                 && r.Enrolment.CategoryId == categoryId
                                 && r.FinishPosition == position
                                 && (ignoreResultId == null || r.ResultId != ignoreResultId));

    private static string? ValidateTime(TimeSpan t) =>
        t <= TimeSpan.Zero || t >= TimeSpan.FromHours(24)
            ? "Finish time must be greater than 00:00:00 and less than 24 hours."
            : null;
}
