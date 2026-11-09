using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

public class AuthTests(RaceDayFactory factory) : IClassFixture<RaceDayFactory>
{
    private static object Participant(string email, string password = TestData.Password, string? dob = "1995-05-05") =>
        new { fullName = "Pat Participant", email, password, role = "Participant", dateOfBirth = dob };

    // ---------- registration ----------

    [Fact]
    public async Task Register_Participant_ReturnsCreated_AndNeverReturnsPassword()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", Participant(TestData.NewEmail()));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"role\":\"Participant\"", body);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_ReturnsCreated()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Olive Organiser", email = TestData.NewEmail(), password = TestData.Password,
            role = "Organiser", organiserCode = TestData.OrganiserCode
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_IsForbidden()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky", email = TestData.NewEmail(), password = TestData.Password, role = "Organiser"
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_IsForbidden()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky", email = TestData.NewEmail(), password = TestData.Password,
            role = "Organiser", organiserCode = "nope"
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", Participant(email))).StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", Participant(email.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "X", email = TestData.NewEmail(), password = TestData.Password, role = "Admin"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", Participant(TestData.NewEmail(), password: "short"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", Participant("not-an-email"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_Participant_WithoutDateOfBirth_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", Participant(TestData.NewEmail(), dob: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", Participant(email));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(TestData.Password, user.PasswordHash);
        Assert.DoesNotContain(TestData.Password, user.PasswordHash);
        Assert.True(hasher.Verify(TestData.Password, user.PasswordHash));
        Assert.False(hasher.Verify("wrong-password", user.PasswordHash));
    }

    // ---------- login / session ----------

    [Fact]
    public async Task Login_WithValidCredentials_StartsSession_AndMeReturnsProfile()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", Participant(email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestData.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.Contains("Set-Cookie"));

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var json = await TestData.ReadJsonAsync(me);
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.Equal("Participant", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", Participant(email));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-Pass-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = TestData.NewEmail(), password = TestData.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = await factory.LoginAsParticipantAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- unauthenticated access ----------

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/events")]
    [InlineData("/api/events/mine")]
    [InlineData("/api/events/1")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/events/1/results")]
    [InlineData("/api/enrolments/mine")]
    [InlineData("/api/results/mine")]
    public async Task ProtectedEndpoints_WithoutLogin_ReturnUnauthorized(string url)
    {
        var res = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/events", TestData.EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- profile ----------

    [Fact]
    public async Task UpdateProfile_ChangesOwnDetails()
    {
        var client = await factory.LoginAsParticipantAsync();
        var res = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Renamed Person", phone = "0821112222" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await TestData.ReadJsonAsync(await client.GetAsync("/api/users/me"));
        Assert.Equal("Renamed Person", json.GetProperty("fullName").GetString());
        Assert.Equal("0821112222", json.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task UpdateProfile_WithEmptyName_ReturnsBadRequest()
    {
        var client = await factory.LoginAsParticipantAsync();
        var res = await client.PutAsJsonAsync("/api/users/me", new { fullName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
