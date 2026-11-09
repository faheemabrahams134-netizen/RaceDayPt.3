using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class EnrolmentTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task Participant_CanEnrol_AndLinkIsRecorded()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        var res = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal(eventId, json.GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, json.GetProperty("categoryId").GetInt32());
        Assert.Equal("Paul Participant", json.GetProperty("participantName").GetString());

        var mine = await TestData.ReadJsonAsync(await participant.GetAsync("/api/enrolments/mine"));
        Assert.Equal(1, mine.GetArrayLength());
    }

    [Fact]
    public async Task Organiser_CannotEnrol()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/events/1/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_ReturnsConflict()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        await TestData.EnrolAsync(participant, eventId, categoryId);

        var again = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_InMissingEvent_ReturnsNotFound()
    {
        var participant = await factory.LoginAsParticipantAsync();
        var res = await participant.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventA = await TestData.CreateEventAsync(organiser);
        var eventB = await TestData.CreateEventAsync(organiser);
        var categoryOfB = await TestData.CreateAgeCategoryAsync(organiser, eventB);

        var res = await participant.PostAsJsonAsync($"/api/events/{eventA}/enrolments", new { categoryId = categoryOfB });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithMissingCategoryId_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        var res = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_InAgeCategory_WhenTooYoung_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var junior = await factory.LoginAsParticipantAsync(dateOfBirth: "2015-01-01");
        var eventId = await TestData.CreateEventAsync(organiser);
        var seniorCategory = await TestData.CreateAgeCategoryAsync(organiser, eventId, minAge: 18);

        var res = await junior.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = seniorCategory });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_InAgeCategory_WhenTooOld_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var veteran = await factory.LoginAsParticipantAsync(dateOfBirth: "1960-01-01");
        var eventId = await TestData.CreateEventAsync(organiser);
        var under20 = await TestData.CreateAgeCategoryAsync(organiser, eventId, minAge: null, maxAge: 19);

        var res = await veteran.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = under20 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Enrol_InDistanceCategory_HasNoAgeRestriction()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var junior = await factory.LoginAsParticipantAsync(dateOfBirth: "2015-01-01");
        var eventId = await TestData.CreateEventAsync(organiser);
        var cat = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "5km", categoryType = "Distance", distanceKm = 5
        });
        var categoryId = await TestData.ReadIntAsync(cat, "categoryId");

        await TestData.EnrolAsync(junior, eventId, categoryId);
    }

    [Fact]
    public async Task Enrol_InEventThatAlreadyHappened_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser, date: DateTime.UtcNow.AddDays(-5));
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        var res = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolments_ForOwnEvent()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        await TestData.EnrolAsync(participant, eventId, categoryId);

        var res = await organiser.GetAsync($"/api/events/{eventId}/enrolments");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(1, (await TestData.ReadJsonAsync(res)).GetArrayLength());
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolments_ForAnotherOrganisersEvent()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var eventId = await TestData.CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{eventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentList()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/events/{eventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantOnlyEndpoint()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/enrolments/mine")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanWithdraw_OwnEnrolment()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await participant.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);
        var mine = await TestData.ReadJsonAsync(await participant.GetAsync("/api/enrolments/mine"));
        Assert.Equal(0, mine.GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotWithdraw_SomeoneElsesEnrolment()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var owner = await factory.LoginAsParticipantAsync();
        var other = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(owner, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);
    }

    [Fact]
    public async Task Withdraw_MissingEnrolment_ReturnsNotFound()
    {
        var participant = await factory.LoginAsParticipantAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await participant.DeleteAsync("/api/enrolments/999999")).StatusCode);
    }

    [Fact]
    public async Task Withdraw_AfterResultRecorded_ReturnsConflict()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", TestData.ResultBody());

        Assert.Equal(HttpStatusCode.Conflict, (await participant.DeleteAsync($"/api/enrolments/{enrolmentId}")).StatusCode);
    }
}

public class EnrolmentStatusTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    [Fact]
    public async Task NewEnrolment_IsPending_AndOrganiserCanConfirmIt()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);

        var created = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        var json = await TestData.ReadJsonAsync(created);
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        var enrolmentId = json.GetProperty("enrolmentId").GetInt32();

        var res = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Confirmed", (await TestData.ReadJsonAsync(res)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Participant_CannotChangeEnrolmentStatus()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);

        var res = await participant.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotChangeStatus_ForSomeoneElsesEvent()
    {
        var owner = await factory.LoginAsOrganiserAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(owner);
        var categoryId = await TestData.CreateAgeCategoryAsync(owner, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);

        var res = await other.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SetStatus_WithInvalidValue_ReturnsBadRequest()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);

        var res = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Maybe" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetStatus_ForMissingEnrolment_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PutAsJsonAsync("/api/enrolments/999999/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
