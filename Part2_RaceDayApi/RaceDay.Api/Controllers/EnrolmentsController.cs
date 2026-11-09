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
public class EnrolmentsController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Enter an event by choosing one of its categories (Participant only).</summary>
    /// <remarks>
    /// Records the link between the participant, the event and the chosen category.
    /// Age categories check the participant's age on the day of the event.
    /// </remarks>
    [HttpPost("events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int eventId, EnrolRequest req)
    {
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.EventDate <= DateTime.UtcNow) return Error(400, "This event has already taken place.");

        var cat = await db.Categories.FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId && c.EventId == eventId);
        if (cat is null) return Error(404, "Category not found for this event.");

        var me = CurrentUserId;
        if (await db.Enrolments.AnyAsync(e => e.EventId == eventId && e.ParticipantId == me))
            return Error(409, "You are already enrolled in this event.");

        var user = await db.Users.FindAsync(me);
        if (user is null) return Error(401, "Your account no longer exists.");

        if (cat.CategoryType == CategoryType.Age)
        {
            if (user.DateOfBirth is null)
                return Error(400, "Add your date of birth to your profile to enter an age category.");
            var age = Mapper.AgeOn(user.DateOfBirth.Value, DateOnly.FromDateTime(ev.EventDate));
            if ((cat.MinAge is not null && age < cat.MinAge) || (cat.MaxAge is not null && age > cat.MaxAge))
                return Error(400, "You are not eligible for this age category.");
        }

        var enrolment = new EventEnrolment { EventId = eventId, ParticipantId = me, CategoryId = cat.CategoryId };
        db.Enrolments.Add(enrolment);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Error(409, "You are already enrolled in this event.");
        }

        return Created($"/api/enrolments/mine",
            new EnrolmentDto(enrolment.EnrolmentId, ev.EventId, ev.Name, ev.EventDate, ev.Location,
                cat.CategoryId, cat.Name, user.UserId, user.FullName, enrolment.EnrolmentDate, enrolment.Status));
    }

    /// <summary>List every enrolment for one of your events (Organiser only).</summary>
    [HttpGet("events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetForEvent(int eventId)
    {
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Error(403, "You can only view enrolments for your own events.");

        var list = await db.Enrolments.AsNoTracking()
            .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .Where(e => e.EventId == eventId).OrderBy(e => e.EnrolmentId).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>List the logged-in participant's own enrolments (Participant only).</summary>
    [HttpGet("enrolments/mine")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetMine()
    {
        var me = CurrentUserId;
        var list = await db.Enrolments.AsNoTracking()
            .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .Where(e => e.ParticipantId == me).OrderBy(e => e.EnrolmentId).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>Confirm an enrolment, or set it back to Pending, in one of your events (Organiser only).</summary>
    [HttpPut("enrolments/{id:int}/status")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentDto>> SetStatus(int id, EnrolmentStatusRequest req)
    {
        var enrolment = await db.Enrolments
            .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Error(404, "Enrolment not found.");
        if (enrolment.Event!.OrganiserId != CurrentUserId)
            return Error(403, "You can only change enrolments for your own events.");

        enrolment.Status = req.Status!.Value;
        await db.SaveChangesAsync();
        return Ok(enrolment.ToDto());
    }

    /// <summary>Withdraw your own enrolment. Blocked once a result exists (Participant only).</summary>
    [HttpDelete("enrolments/{id:int}")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await db.Enrolments.FindAsync(id);
        if (enrolment is null) return Error(404, "Enrolment not found.");
        if (enrolment.ParticipantId != CurrentUserId) return Error(403, "You can only withdraw your own enrolment.");
        if (await db.Results.AnyAsync(r => r.EnrolmentId == id))
            return Error(409, "A result has already been recorded for this enrolment.");

        db.Enrolments.Remove(enrolment);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
