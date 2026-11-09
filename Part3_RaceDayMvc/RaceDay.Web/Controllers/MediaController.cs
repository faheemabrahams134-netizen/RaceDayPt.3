using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Serves images to the browser. The bytes come from the API (which reads Azure Blob Storage):
/// this app never talks to Azure directly.
/// </summary>
[SessionAuthorize]
public class MediaController(IRaceDayApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> EventBanner(int id)
    {
        var image = await api.GetBannerAsync(id);
        return ToResult(image);
    }

    [HttpGet]
    public async Task<IActionResult> ProfilePicture()
    {
        var image = await api.GetProfilePictureAsync();
        return ToResult(image);
    }

    private IActionResult ToResult(RaceDay.Web.Models.ImageFile? image)
    {
        if (image is null) return NotFound();
        // The ?v= query string changes whenever a new image is uploaded, so a private cache is safe.
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(image.Bytes, image.ContentType);
    }
}
