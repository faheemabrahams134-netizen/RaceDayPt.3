using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Typed HttpClient for the Part 2 API.
/// Session handling: the API issues an HttpOnly session cookie on login. We keep that cookie in the
/// user's server-side session and send it back on every call, so the API sees the same authenticated user.
/// </summary>
public class RaceDayApi(HttpClient http, IHttpContextAccessor accessor) : IRaceDayApi
{
    private const string ApiCookieName = "RaceDay.Session";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private ISession? Session => accessor.HttpContext?.Session;

    // ------------------------------------------------------------------ core plumbing

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        var cookie = Session?.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "The RaceDay service is not reachable right now. Please try again in a moment.");
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(504, "The RaceDay service took too long to respond. Please try again.");
        }

        CaptureSessionCookie(response);
        return response;
    }

    /// <summary>Keep the API's (sliding) session cookie up to date in our session.</summary>
    private void CaptureSessionCookie(HttpResponseMessage response)
    {
        if (Session is null || !response.Headers.TryGetValues("Set-Cookie", out var headers)) return;
        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0].Trim();
            if (!pair.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;
            if (pair.Length > ApiCookieName.Length + 1) Session.SetString(SessionKeys.ApiCookie, pair);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw await ToExceptionAsync(response);
    }

    private static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        var message = status switch
        {
            401 => "Please log in to continue.",
            403 => "You do not have permission to do that.",
            404 => "That item could not be found.",
            409 => "That action conflicts with existing data.",
            >= 500 => "Something went wrong on our side. Please try again.",
            _ => "That request could not be completed. Please check what you entered and try again."
        };

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (status < 500 && root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(detail.GetString()))
                    {
                        message = detail.GetString()!;
                    }
                    else if (status < 500 && root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var field in errors.EnumerateObject())
                        {
                            // Keys starting with "$" are JSON-parsing problems: their text is technical, so don't show it.
                            if (field.Name.StartsWith('$') || field.Value.ValueKind != JsonValueKind.Array) continue;
                            foreach (var item in field.Value.EnumerateArray())
                                if (item.ValueKind == JsonValueKind.String) messages.Add(item.GetString()!);
                        }
                        message = messages.Count > 0
                            ? string.Join(" ", messages.Distinct())
                            : "Some of the information entered is not valid.";
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: keep the generic message.
        }

        return new ApiException(status, message);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object body)
    {
        using var response = await SendAsync(method, url, JsonContent.Create(body, mediaType: null, options: Json));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task SendNoContentAsync(HttpMethod method, string url)
    {
        using var response = await SendAsync(method, url);
        await EnsureSuccessAsync(response);
    }

    private async Task<T> UploadAsync<T>(string url, IFormFile file)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
            fileContent.Headers.ContentType = mediaType;
        form.Add(fileContent, "file", Path.GetFileName(file.FileName));

        using var response = await SendAsync(HttpMethod.Post, url, form);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<ImageFile?> DownloadImageAsync(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return new ImageFile(bytes, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }

    // ------------------------------------------------------------------ auth

    public Task<UserDto> LoginAsync(string email, string password) =>
        SendJsonAsync<UserDto>(HttpMethod.Post, "api/auth/login", new { email, password });

    public Task<UserDto> RegisterAsync(string fullName, string email, string password, string role, string? phone,
        DateOnly? dateOfBirth, string? organiserCode) =>
        SendJsonAsync<UserDto>(HttpMethod.Post, "api/auth/register",
            new { fullName, email, password, role, phone, dateOfBirth, organiserCode });

    public async Task LogoutAsync()
    {
        using var response = await SendAsync(HttpMethod.Post, "api/auth/logout");
        // 401 simply means the API session had already ended: that is fine on logout.
        if (response.StatusCode != HttpStatusCode.Unauthorized) await EnsureSuccessAsync(response);
    }

    // ------------------------------------------------------------------ profile

    public Task<UserDto> GetMeAsync() => GetAsync<UserDto>("api/users/me");

    public Task<UserDto> UpdateMeAsync(string fullName, string? phone, DateOnly? dateOfBirth) =>
        SendJsonAsync<UserDto>(HttpMethod.Put, "api/users/me", new { fullName, phone, dateOfBirth });

    public Task<UserDto> UploadProfilePictureAsync(IFormFile file) => UploadAsync<UserDto>("api/users/me/picture", file);

    public Task<ImageFile?> GetProfilePictureAsync() => DownloadImageAsync("api/users/me/picture");

    // ------------------------------------------------------------------ events

    public Task<List<EventDto>> GetEventsAsync(EventType? type, DateTime? from, DateTime? to)
    {
        var query = new List<string>();
        if (type is { } t) query.Add("eventType=" + t);
        if (from is { } f) query.Add("from=" + Uri.EscapeDataString(f.ToString("yyyy-MM-ddTHH:mm:ss")));
        if (to is { } e) query.Add("to=" + Uri.EscapeDataString(e.ToString("yyyy-MM-ddTHH:mm:ss")));
        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return GetAsync<List<EventDto>>(url);
    }

    public Task<List<EventDto>> GetMyEventsAsync() => GetAsync<List<EventDto>>("api/events/mine");

    public Task<EventDetailDto> GetEventAsync(int id) => GetAsync<EventDetailDto>($"api/events/{id}");

    public Task<EventDto> CreateEventAsync(string name, string description, DateTime eventDate, string location,
        decimal distanceKm, EventType eventType) =>
        SendJsonAsync<EventDto>(HttpMethod.Post, "api/events",
            new { name, description, eventDate, location, distanceKm, eventType });

    public Task<EventDto> UpdateEventAsync(int id, string name, string description, DateTime eventDate, string location,
        decimal distanceKm, EventType eventType) =>
        SendJsonAsync<EventDto>(HttpMethod.Put, $"api/events/{id}",
            new { name, description, eventDate, location, distanceKm, eventType });

    public Task DeleteEventAsync(int id) => SendNoContentAsync(HttpMethod.Delete, $"api/events/{id}");

    public Task<EventDto> UploadBannerAsync(int eventId, IFormFile file) =>
        UploadAsync<EventDto>($"api/events/{eventId}/banner", file);

    public Task<ImageFile?> GetBannerAsync(int eventId) => DownloadImageAsync($"api/events/{eventId}/banner");

    // ------------------------------------------------------------------ categories

    public Task<CategoryDto> CreateCategoryAsync(int eventId, string name, string? description, CategoryType type,
        int? minAge, int? maxAge, decimal? distanceKm) =>
        SendJsonAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories",
            new { name, description, categoryType = type, minAge, maxAge, distanceKm });

    public Task<CategoryDto> UpdateCategoryAsync(int id, string name, string? description, CategoryType type,
        int? minAge, int? maxAge, decimal? distanceKm) =>
        SendJsonAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{id}",
            new { name, description, categoryType = type, minAge, maxAge, distanceKm });

    public Task DeleteCategoryAsync(int id) => SendNoContentAsync(HttpMethod.Delete, $"api/categories/{id}");

    // ------------------------------------------------------------------ enrolments

    public Task<EnrolmentDto> EnrolAsync(int eventId, int categoryId) =>
        SendJsonAsync<EnrolmentDto>(HttpMethod.Post, $"api/events/{eventId}/enrolments", new { categoryId });

    public Task<List<EnrolmentDto>> GetEnrolmentsForEventAsync(int eventId) =>
        GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");

    public Task<List<EnrolmentDto>> GetMyEnrolmentsAsync() => GetAsync<List<EnrolmentDto>>("api/enrolments/mine");

    public Task WithdrawAsync(int enrolmentId) => SendNoContentAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    public Task<EnrolmentDto> SetEnrolmentStatusAsync(int enrolmentId, EnrolmentStatus status) =>
        SendJsonAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", new { status });

    // ------------------------------------------------------------------ results

    public Task<ResultDto> CreateResultAsync(int enrolmentId, TimeSpan finishTime, int position) =>
        SendJsonAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result",
            new { finishTime, finishPosition = position });

    public Task<ResultDto> UpdateResultAsync(int resultId, TimeSpan finishTime, int position) =>
        SendJsonAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}",
            new { finishTime, finishPosition = position });

    public Task<List<ResultDto>> GetResultsForEventAsync(int eventId) =>
        GetAsync<List<ResultDto>>($"api/events/{eventId}/results");

    public Task<List<ResultDto>> GetMyResultsAsync() => GetAsync<List<ResultDto>>("api/results/mine");
}
