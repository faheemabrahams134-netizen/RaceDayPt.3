using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Api.Tests;

public class ResultTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private record Setup(HttpClient Organiser, HttpClient Participant, int EventId, int CategoryId, int EnrolmentId);

    private async Task<Setup> ArrangeAsync()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var participant = await factory.LoginAsParticipantAsync();
        var eventId = await TestData.CreateEventAsync(organiser);
        var categoryId = await TestData.CreateAgeCategoryAsync(organiser, eventId);
        var enrolmentId = await TestData.EnrolAsync(participant, eventId, categoryId);
        return new Setup(organiser, participant, eventId, categoryId, enrolmentId);
    }

    [Fact]
    public async Task Organiser_CanRecordResult()
    {
        var s = await ArrangeAsync();

        var res = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody("01:02:03", 3));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal("01:02:03", json.GetProperty("finishTime").GetString());
        Assert.Equal(3, json.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_CannotRecordResult()
    {
        var s = await ArrangeAsync();
        var res = await s.Participant.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RecordResult_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/enrolments/1/result", TestData.ResultBody());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotRecordResult_ForAnotherOrganisersEvent()
    {
        var s = await ArrangeAsync();
        var other = await factory.LoginAsOrganiserAsync();

        var res = await other.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForMissingEnrolment_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", TestData.ResultBody());
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task RecordResult_Twice_ReturnsConflict()
    {
        var s = await ArrangeAsync();
        await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody());

        var again = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody(position: 2));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task RecordResult_WithPositionAlreadyTakenInCategory_ReturnsConflict()
    {
        var s = await ArrangeAsync();
        var second = await factory.LoginAsParticipantAsync();
        var secondEnrolment = await TestData.EnrolAsync(second, s.EventId, s.CategoryId);
        await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody(position: 1));

        var res = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{secondEnrolment}/result", TestData.ResultBody("00:50:00", 1));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Theory]
    [InlineData("00:00:00", 1)]
    [InlineData("25:00:00", 1)]
    [InlineData("00:45:00", 0)]
    [InlineData("00:45:00", -3)]
    public async Task RecordResult_WithInvalidValues_ReturnsBadRequest(string time, int position)
    {
        var s = await ArrangeAsync();
        var res = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody(time, position));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectResult()
    {
        var s = await ArrangeAsync();
        var created = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody("00:45:00", 5));
        var resultId = await TestData.ReadIntAsync(created, "resultId");

        var res = await s.Organiser.PutAsJsonAsync($"/api/results/{resultId}", TestData.ResultBody("00:44:30", 4));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal("00:44:30", json.GetProperty("finishTime").GetString());
        Assert.Equal(4, json.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_And_OtherOrganiser_CannotCorrectResult()
    {
        var s = await ArrangeAsync();
        var other = await factory.LoginAsOrganiserAsync();
        var created = await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody());
        var resultId = await TestData.ReadIntAsync(created, "resultId");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await s.Participant.PutAsJsonAsync($"/api/results/{resultId}", TestData.ResultBody("00:40:00", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PutAsJsonAsync($"/api/results/{resultId}", TestData.ResultBody("00:40:00", 1))).StatusCode);
    }

    [Fact]
    public async Task CorrectResult_ThatDoesNotExist_ReturnsNotFound()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        var res = await organiser.PutAsJsonAsync("/api/results/999999", TestData.ResultBody());
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnResults()
    {
        var s = await ArrangeAsync();
        await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody("00:39:59", 1));

        var res = await s.Participant.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await TestData.ReadJsonAsync(res);
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal("00:39:59", json[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyResultsEndpoint()
    {
        var organiser = await factory.LoginAsOrganiserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/results/mine")).StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEventResults_OrderedByPosition()
    {
        var s = await ArrangeAsync();
        var second = await factory.LoginAsParticipantAsync();
        var secondEnrolment = await TestData.EnrolAsync(second, s.EventId, s.CategoryId);
        await s.Organiser.PostAsJsonAsync($"/api/enrolments/{s.EnrolmentId}/result", TestData.ResultBody("00:50:00", 2));
        await s.Organiser.PostAsJsonAsync($"/api/enrolments/{secondEnrolment}/result", TestData.ResultBody("00:40:00", 1));

        foreach (var client in new[] { s.Organiser, s.Participant })
        {
            var res = await client.GetAsync($"/api/events/{s.EventId}/results");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var json = await TestData.ReadJsonAsync(res);
            Assert.Equal(2, json.GetArrayLength());
            Assert.Equal(1, json[0].GetProperty("finishPosition").GetInt32());
            Assert.Equal(2, json[1].GetProperty("finishPosition").GetInt32());
        }
    }

    [Fact]
    public async Task EventResults_ForMissingEvent_ReturnsNotFound()
    {
        var participant = await factory.LoginAsParticipantAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await participant.GetAsync("/api/events/999999/results")).StatusCode);
    }
}
