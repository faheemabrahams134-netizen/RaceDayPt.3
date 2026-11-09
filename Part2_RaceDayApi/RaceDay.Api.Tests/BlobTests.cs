using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class BlobTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    // Smallest valid-looking PNG / JPEG headers: the API checks the real file signature.
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_AndDownloadIt()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var upload = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var json = await TestData.ReadJsonAsync(upload);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("bannerVersion").GetString()));

        var download = await organiser.GetAsync($"/api/events/{eventId}/banner");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadBanner_Twice_ChangesVersion()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var first = await TestData.ReadJsonAsync(await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "a.png", "image/png")));
        var second = await TestData.ReadJsonAsync(await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(Jpeg, "b.jpg", "image/jpeg")));

        Assert.NotEqual(first.GetProperty("bannerVersion").GetString(), second.GetProperty("bannerVersion").GetString());
    }

    [Fact]
    public async Task UploadBanner_WithNonImage_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just text pretending to be a picture");

        var res = await organiser.PostAsync($"/api/events/{eventId}/banner", FileContent(notAnImage, "evil.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForSomeoneElsesEvent_ReturnsForbidden()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(owner);

        var res = await other.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await participant.PostAsync($"/api/events/{eventId}/banner", FileContent(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsync("/api/events/1/banner", FileContent(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task EventWithoutBanner_Returns404ForBanner()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.GetAsync($"/api/events/{eventId}/banner");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task User_CanUploadProfilePicture_AndDownloadIt()
    {
        var participant = await factory.LoginAsParticipantAsync();

        var upload = await participant.PostAsync("/api/users/me/picture", FileContent(Jpeg, "me.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var json = await TestData.ReadJsonAsync(upload);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("profilePictureVersion").GetString()));

        var download = await participant.GetAsync("/api/users/me/picture");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/jpeg", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsync("/api/users/me/picture", FileContent(Png, "me.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_WithNonImage_ReturnsBadRequest()
    {
        var participant = await factory.LoginAsParticipantAsync();
        var res = await participant.PostAsync("/api/users/me/picture",
            FileContent(System.Text.Encoding.UTF8.GetBytes("not an image"), "me.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
