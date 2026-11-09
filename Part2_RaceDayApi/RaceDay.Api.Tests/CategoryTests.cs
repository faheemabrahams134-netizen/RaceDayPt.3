using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class CategoryTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Organiser_CanAddAgeCategory()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Under 20", categoryType = "Age", maxAge = 19
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal("Age", json.GetProperty("categoryType").GetString());
        Assert.Equal(19, json.GetProperty("maxAge").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanAddDistanceCategory()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "10km", categoryType = "Distance", distanceKm = 10
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task AgeCategory_WithoutAges_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "Senior", categoryType = "Age" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AgeCategory_WithMinAboveMax_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Broken", categoryType = "Age", minAge = 40, maxAge = 20
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DistanceCategory_WithoutDistance_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "21km", categoryType = "Distance" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategory()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Sneaky", categoryType = "Distance", distanceKm = 5
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(owner);

        var res = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Intruder", categoryType = "Distance", distanceKm = 5
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToMissingEvent_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/events/999999/categories", new
        {
            name = "Ghost", categoryType = "Distance", distanceKm = 5
        });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanListCategories()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        await TestData.CreateAgeCategoryAsync(organiser, eventId);

        foreach (var client in new[] { organiser, participant })
        {
            var res = await client.GetAsync($"/api/events/{eventId}/categories");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal(1, (await TestData.ReadJsonAsync(res)).GetArrayLength());
        }
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnCategory_ButOthersCannot()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(owner);
        var categoryId = await TestData.CreateAgeCategoryAsync(owner, eventId);
        var body = new { name = "Veteran", categoryType = "Age", minAge = 50 };

        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/categories/{categoryId}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/categories/{categoryId}", body)).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteCategory_WithoutEnrolments()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_ReturnsConflict()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        await TestData.EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotDeleteCategory()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/categories/{categoryId}")).StatusCode);
    }
}
