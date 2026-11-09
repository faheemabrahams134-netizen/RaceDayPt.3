namespace RaceDay.Web.Models;

// These records mirror the JSON returned by the Part 2 API.

public enum EventType { Run, Walk, Cycle }
public enum CategoryType { Age, Distance }
public enum EnrolmentStatus { Pending, Confirmed }

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public record UserDto(int UserId, string FullName, string Email, string Role, string? Phone, DateOnly? DateOfBirth,
    DateTime CreatedAt, string? ProfilePictureVersion);

public record CategoryDto(int CategoryId, int EventId, string Name, string? Description, CategoryType CategoryType,
    int? MinAge, int? MaxAge, decimal? DistanceKm);

public record EventDto(int EventId, int OrganiserId, string Name, string Description, DateTime EventDate,
    string Location, decimal DistanceKm, EventType EventType, string? BannerVersion, int EnrolmentCount);

public record EventDetailDto(int EventId, int OrganiserId, string Name, string Description, DateTime EventDate,
    string Location, decimal DistanceKm, EventType EventType, string? BannerVersion, int EnrolmentCount,
    List<CategoryDto> Categories);

public record EnrolmentDto(int EnrolmentId, int EventId, string EventName, DateTime EventDate, string EventLocation,
    int CategoryId, string CategoryName, int ParticipantId, string ParticipantName, DateTime EnrolmentDate,
    EnrolmentStatus Status);

public record ResultDto(int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    int ParticipantId, string ParticipantName, int CategoryId, string CategoryName, TimeSpan FinishTime,
    int FinishPosition, int TotalFinishers);

/// <summary>An image streamed back from the API (which reads it from Azure Blob Storage).</summary>
public record ImageFile(byte[] Bytes, string ContentType);
