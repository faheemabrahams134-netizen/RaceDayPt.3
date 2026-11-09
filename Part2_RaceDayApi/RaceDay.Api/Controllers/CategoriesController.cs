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
public class CategoriesController(AppDbContext db) : ApiControllerBase
{
    /// <summary>List the categories of an event (both roles).</summary>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetForEvent(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return Error(404, "Event not found.");
        var cats = await db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId).ToListAsync();
        return Ok(cats.Select(c => c.ToDto()));
    }

    /// <summary>Add an age or distance category to one of your events (Organiser only).</summary>
    /// <remarks>Age categories need MinAge and/or MaxAge. Distance categories need DistanceKm.</remarks>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Create(int eventId, CategoryRequest req)
    {
        var ev = await db.Events.FindAsync(eventId);
        if (ev is null) return Error(404, "Event not found.");
        if (ev.OrganiserId != CurrentUserId) return Error(403, "You can only add categories to your own events.");

        var problem = Validate(req);
        if (problem is not null) return Error(400, problem);

        var cat = new Category { EventId = eventId };
        Apply(cat, req);
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", cat.ToDto());
    }

    /// <summary>Update a category on one of your events (Organiser only).</summary>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest req)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return Error(404, "Category not found.");
        if (cat.Event!.OrganiserId != CurrentUserId) return Error(403, "You can only change your own categories.");

        var problem = Validate(req);
        if (problem is not null) return Error(400, problem);

        Apply(cat, req);
        await db.SaveChangesAsync();
        return Ok(cat.ToDto());
    }

    /// <summary>Delete a category that has no enrolments (Organiser only).</summary>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return Error(404, "Category not found.");
        if (cat.Event!.OrganiserId != CurrentUserId) return Error(403, "You can only delete your own categories.");
        if (await db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Error(409, "This category has enrolments and cannot be deleted.");

        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(CategoryRequest r)
    {
        if (r.CategoryType == CategoryType.Age)
        {
            if (r.MinAge is null && r.MaxAge is null)
                return "Age categories require MinAge and/or MaxAge.";
            if (r.MinAge is not null && r.MaxAge is not null && r.MinAge > r.MaxAge)
                return "MinAge cannot be greater than MaxAge.";
            if (r.DistanceKm is not null)
                return "Age categories must not specify DistanceKm.";
        }
        else
        {
            if (r.DistanceKm is null)
                return "Distance categories require DistanceKm.";
            if (r.MinAge is not null || r.MaxAge is not null)
                return "Distance categories must not specify MinAge or MaxAge.";
        }
        return null;
    }

    private static void Apply(Category cat, CategoryRequest r)
    {
        cat.Name = r.Name.Trim();
        cat.Description = r.Description?.Trim();
        cat.CategoryType = r.CategoryType!.Value;
        cat.MinAge = r.MinAge;
        cat.MaxAge = r.MaxAge;
        cat.DistanceKm = r.DistanceKm;
    }
}
