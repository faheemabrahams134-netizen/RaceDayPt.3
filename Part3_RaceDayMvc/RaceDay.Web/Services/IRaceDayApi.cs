using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Everything the MVC app can do is a call to the Part 2 RESTful API.</summary>
public interface IRaceDayApi
{
    // Auth
    Task<UserDto> LoginAsync(string email, string password);
    Task<UserDto> RegisterAsync(string fullName, string email, string password, string role, string? phone,
        DateOnly? dateOfBirth, string? organiserCode);
    Task LogoutAsync();

    // Profile
    Task<UserDto> GetMeAsync();
    Task<UserDto> UpdateMeAsync(string fullName, string? phone, DateOnly? dateOfBirth);
    Task<UserDto> UploadProfilePictureAsync(IFormFile file);
    Task<ImageFile?> GetProfilePictureAsync();

    // Events
    Task<List<EventDto>> GetEventsAsync(EventType? type, DateTime? from, DateTime? to);
    Task<List<EventDto>> GetMyEventsAsync();
    Task<EventDetailDto> GetEventAsync(int id);
    Task<EventDto> CreateEventAsync(string name, string description, DateTime eventDate, string location,
        decimal distanceKm, EventType eventType);
    Task<EventDto> UpdateEventAsync(int id, string name, string description, DateTime eventDate, string location,
        decimal distanceKm, EventType eventType);
    Task DeleteEventAsync(int id);
    Task<EventDto> UploadBannerAsync(int eventId, IFormFile file);
    Task<ImageFile?> GetBannerAsync(int eventId);

    // Categories
    Task<CategoryDto> CreateCategoryAsync(int eventId, string name, string? description, CategoryType type,
        int? minAge, int? maxAge, decimal? distanceKm);
    Task<CategoryDto> UpdateCategoryAsync(int id, string name, string? description, CategoryType type,
        int? minAge, int? maxAge, decimal? distanceKm);
    Task DeleteCategoryAsync(int id);

    // Enrolments
    Task<EnrolmentDto> EnrolAsync(int eventId, int categoryId);
    Task<List<EnrolmentDto>> GetEnrolmentsForEventAsync(int eventId);
    Task<List<EnrolmentDto>> GetMyEnrolmentsAsync();
    Task WithdrawAsync(int enrolmentId);
    Task<EnrolmentDto> SetEnrolmentStatusAsync(int enrolmentId, EnrolmentStatus status);

    // Results
    Task<ResultDto> CreateResultAsync(int enrolmentId, TimeSpan finishTime, int position);
    Task<ResultDto> UpdateResultAsync(int resultId, TimeSpan finishTime, int position);
    Task<List<ResultDto>> GetResultsForEventAsync(int eventId);
    Task<List<ResultDto>> GetMyResultsAsync();
}
