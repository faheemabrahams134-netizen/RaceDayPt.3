using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Api.Tests;

/// <summary>Helpers that create logged-in clients and sample data through the public API.</summary>
public static class TestData
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";
    public const string Password = "Passw0rd!23";

    public static string NewEmail() => $"{Guid.NewGuid():N}@raceday.test";

    public static DateTime FutureDate() => DateTime.UtcNow.AddMonths(2);

    /// <summary>A new HttpClient (own cookie jar) logged in as a brand-new Organiser.</summary>
    public static async Task<HttpClient> LoginAsOrganiserAsync(this RaceDayFactory factory)
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Olivia Organiser", email, password = Password, role = "Organiser", organiserCode = OrganiserCode
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        await LoginAsync(client, email);
        return client;
    }

    /// <summary>A new HttpClient (own cookie jar) logged in as a brand-new Participant.</summary>
    public static async Task<HttpClient> LoginAsParticipantAsync(this RaceDayFactory factory, string dateOfBirth = "1995-05-05")
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Paul Participant", email, password = Password, role = "Participant", dateOfBirth
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        await LoginAsync(client, email);
        return client;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    public static async Task<int> ReadIntAsync(HttpResponseMessage response, string property)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty(property).GetInt32();
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public static object EventBody(DateTime? date = null, decimal distance = 10m, string type = "Run", string name = "City 10K") => new
    {
        name,
        description = "A friendly community race.",
        eventDate = date ?? FutureDate(),
        location = "Cape Town",
        distanceKm = distance,
        eventType = type
    };

    public static async Task<int> CreateEventAsync(HttpClient organiser, DateTime? date = null)
    {
        var res = await organiser.PostAsJsonAsync("/api/events", EventBody(date));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await ReadIntAsync(res, "eventId");
    }

    public static async Task<int> CreateAgeCategoryAsync(HttpClient organiser, int eventId, int? minAge = 18, int? maxAge = null)
    {
        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Senior", categoryType = "Age", minAge, maxAge
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await ReadIntAsync(res, "categoryId");
    }

    public static async Task<int> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var res = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await ReadIntAsync(res, "enrolmentId");
    }

    public static object ResultBody(string finishTime = "00:45:12", int position = 1) => new { finishTime, finishPosition = position };
}
