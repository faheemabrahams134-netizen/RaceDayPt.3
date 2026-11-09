using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class UsersController(AppDbContext db, IBlobStorageService blobs) : ApiControllerBase
{
    /// <summary>Get the profile of the logged-in user.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        return user is null ? Error(401, "Your account no longer exists.") : Ok(user.ToDto());
    }

    /// <summary>Update the logged-in user's own profile.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return Error(401, "Your account no longer exists.");

        if (req.DateOfBirth is { } dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dob >= today || dob < today.AddYears(-120))
                return Error(400, "Date of birth is not valid.");
            user.DateOfBirth = dob;
        }

        user.FullName = req.FullName.Trim();
        user.Phone = req.Phone?.Trim();
        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }

    /// <summary>Upload or replace the logged-in user's profile picture.</summary>
    /// <remarks>Multipart form upload with a single field called "file". JPG, PNG, GIF or WebP up to 5 MB.</remarks>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> UploadPicture(IFormFile file, CancellationToken ct)
    {
        var user = await db.Users.FindAsync(new object[] { CurrentUserId }, ct);
        if (user is null) return Error(401, "Your account no longer exists.");

        var check = await ImageValidator.InspectAsync(file);
        if (check.Error is not null) return Error(400, check.Error);

        var blobName = $"{user.UserId}/{Guid.NewGuid():N}{check.Extension}";
        await using (var stream = file.OpenReadStream())
            await blobs.UploadAsync(BlobContainers.ProfilePictures, blobName, stream, check.ContentType, ct);

        var previous = user.ProfilePictureBlob;
        user.ProfilePictureBlob = blobName;
        await db.SaveChangesAsync(ct);
        if (previous is not null) await blobs.DeleteAsync(BlobContainers.ProfilePictures, previous, ct);

        return Ok(user.ToDto());
    }

    /// <summary>Download the logged-in user's profile picture.</summary>
    [HttpGet("me/picture")]
    [Produces("image/jpeg", "image/png", "image/gif", "image/webp")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPicture(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId, ct);
        if (user?.ProfilePictureBlob is null) return Error(404, "You have not uploaded a profile picture.");
        var file = await blobs.DownloadAsync(BlobContainers.ProfilePictures, user.ProfilePictureBlob, ct);
        if (file is null) return Error(404, "You have not uploaded a profile picture.");
        return File(file.Content, file.ContentType);
    }
}
