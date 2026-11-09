using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class EventTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/events", TestData.EventBody(type: "Cycle", distance: 40m));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal("Cycle", json.GetProperty("eventType").GetString());
        Assert.Equal(40m, json.GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    public async Task Participant_CannotCreateEvent()
    {
        var participant = await factory.LoginAsParticipantAsync();
        var res = await participant.PostAsJsonAsync("/api/events", TestData.EventBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(5000)]
    public async Task CreateEvent_WithInvalidDistance_ReturnsBadRequest(int distance)
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/events", TestData.EventBody(distance: distance));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/events", TestData.EventBody(type: "Swim"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingName_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/events", new
        {
            description = "x", eventDate = TestData.FutureDate(), location = "Y", distanceKm = 5, eventType = "Walk"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEvents_AndEventDetail()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        await TestData.CreateAgeCategoryAsync(organiser, eventId);

        foreach (var client in new[] { organiser, participant })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/events")).StatusCode);

            var detail = await client.GetAsync($"/api/events/{eventId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var json = await TestData.ReadJsonAsync(detail);
            Assert.Equal(1, json.GetProperty("categories").GetArrayLength());
        }
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var participant = await factory.LoginAsParticipantAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await participant.GetAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task GetEvents_CanFilterByEventType()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        await organiser.PostAsJsonAsync("/api/events", TestData.EventBody(type: "Walk", name: "Park Stroll"));

        var json = await TestData.ReadJsonAsync(await organiser.GetAsync("/api/events?eventType=Walk"));
        Assert.True(json.GetArrayLength() >= 1);
        foreach (var e in json.EnumerateArray())
            Assert.Equal("Walk", e.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_SeesOnlyOwnEvents_InMine()
    {
        var a = await factory.LoginAsOrganiserAsync();
        var b = await factory.LoginAsOrganiserAsync();
        var aEvent = await TestData.CreateEventAsync(a);
        await TestData.CreateEventAsync(b);

        var json = await TestData.ReadJsonAsync(await a.GetAsync("/api/events/mine"));
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal(aEvent, json[0].GetProperty("eventId").GetInt32());
    }

    [Fact]
    public async Task Participant_CannotViewOrganiserMine()
    {
        var participant = await factory.LoginAsParticipantAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var id = await TestData.CreateEventAsync(organiser);

        var res = await organiser.PutAsJsonAsync($"/api/events/{id}", TestData.EventBody(name: "Renamed Race", distance: 21.1m));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal("Renamed Race", json.GetProperty("name").GetString());
        Assert.Equal(21.1m, json.GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var id = await TestData.CreateEventAsync(owner);

        var res = await other.PutAsJsonAsync($"/api/events/{id}", TestData.EventBody(name: "Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvents()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var id = await TestData.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PutAsJsonAsync($"/api/events/{id}", TestData.EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PutAsJsonAsync("/api/events/999999", TestData.EventBody());
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_WithoutEnrolments()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var id = await TestData.CreateEventAsync(organiser);
        await TestData.CreateAgeCategoryAsync(organiser, id);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var id = await TestData.CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        await TestData.EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/events/{eventId}")).StatusCode);
    }
}
