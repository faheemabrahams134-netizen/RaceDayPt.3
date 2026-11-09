using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class EventsController(AppDbContext db, IBlobStorageService blobs) : ApiControllerBase
{
    /// <summary>List events (both roles). Optionally filter by type and date range.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetAll(
        [FromQuery] EventType? eventType, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = db.Events.AsNoTracking().AsQueryable();
        if (eventType.HasValue) query = query.Where(e => e.EventType == eventType.Value);
        if (from.HasValue) query = query.Where(e => e.EventDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.EventDate <= to.Value);

        var rows = await query.OrderBy(e => e.EventDate)
            .Select(e => new { Event = e, Count = e.Enrolments.Count }).ToListAsync();
        return Ok(rows.Select(r => r.Event.ToDto(r.Count)));
    }

    /// <summary>List the events created by the logged-in Organiser.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetMine()
    {
        var me = CurrentUserId;
        var rows = await db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == me).OrderBy(e => e.EventDate)
            .Select(e => new { Event = e, Count = e.Enrolments.Count }).ToListAsync();
        return Ok(rows.Select(r => r.Event.ToDto(r.Count)));
    }

    /// <summary>Get one event, including its categories (both roles).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetById(int id)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Error(404, "Event not found.");
        var count = await db.Enrolments.CountAsync(e => e.EventId == id);
        return Ok(ev.ToDetailDto(count));
    }

    /// <summary>Create an event (Organiser only).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> Create(EventRequest req)
    {
        var ev = new RaceEvent { OrganiserId = CurrentUserId };
        Apply(ev, req);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Update an event you created (Organiser only).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> Update(int id, EventRequest req)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Error(403, "You can only change your own events.");

        Apply(ev, req);
        await db.SaveChangesAsync();
        var count = await db.Enrolments.CountAsync(e => e.EventId == id);
        return Ok(ev.ToDto(count));
    }

    /// <summary>Upload or replace the banner image of one of your events (Organiser only).</summary>
    /// <remarks>
    /// Multipart form upload with a single field called "file". JPG, PNG, GIF or WebP up to 5 MB.
    /// The image is stored in Azure Blob Storage; only a reference is kept in the database.
    /// </remarks>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, IFormFile file, CancellationToken ct)
    {
        var ev = await db.Events.FindAsync(new object[] { id }, ct);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Error(403, "You can only change the banner of your own events.");

        var check = await ImageValidator.InspectAsync(file);
        if (check.Error is not null) return Error(400, check.Error);

        var blobName = $"{id}/{Guid.NewGuid():N}{check.Extension}";
        await using (var stream = file.OpenReadStream())
            await blobs.UploadAsync(BlobContainers.EventBanners, blobName, stream, check.ContentType, ct);

        var previous = ev.BannerBlob;
        ev.BannerBlob = blobName;
        await db.SaveChangesAsync(ct);
        if (previous is not null) await blobs.DeleteAsync(BlobContainers.EventBanners, previous, ct);

        var count = await db.Enrolments.CountAsync(e => e.EventId == id, ct);
        return Ok(ev.ToDto(count));
    }

    /// <summary>Download the banner image of an event (both roles).</summary>
    [HttpGet("{id:int}/banner")]
    [Produces("image/jpeg", "image/png", "image/gif", "image/webp")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBanner(int id, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev?.BannerBlob is null) return Error(404, "This event has no banner image.");
        var file = await blobs.DownloadAsync(BlobContainers.EventBanners, ev.BannerBlob, ct);
        if (file is null) return Error(404, "This event has no banner image.");
        return File(file.Content, file.ContentType);
    }

    /// <summary>Delete an event you created. Blocked while participants are enrolled (Organiser only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Error(403, "You can only delete your own events.");
        if (await db.Enrolments.AnyAsync(e => e.EventId == id))
            return Error(409, "This event has enrolments and cannot be deleted.");

        var banner = ev.BannerBlob;
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        if (banner is not null) await blobs.DeleteAsync(BlobContainers.EventBanners, banner);
        return NoContent();
    }

    private static void Apply(RaceEvent ev, EventRequest req)
    {
        ev.Name = req.Name.Trim();
        ev.Description = req.Description.Trim();
        ev.EventDate = req.EventDate!.Value;
        ev.Location = req.Location.Trim();
        ev.DistanceKm = req.DistanceKm!.Value;
        ev.EventType = req.EventType!.Value;
    }
}
