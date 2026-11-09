using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>ProfilePictureVersion is null when the user has no picture; otherwise it changes on every upload (use it to bust caches).</summary>
public record UserDto(int UserId, string FullName, string Email, string Role, string? Phone, DateOnly? DateOfBirth,
    DateTime CreatedAt, string? ProfilePictureVersion);

/// <summary>BannerVersion is null when the event has no banner image.</summary>
public record EventDto(int EventId, int OrganiserId, string Name, string Description, DateTime EventDate,
    string Location, decimal DistanceKm, EventType EventType, string? BannerVersion, int EnrolmentCount);

public record CategoryDto(int CategoryId, int EventId, string Name, string? Description, CategoryType CategoryType,
    int? MinAge, int? MaxAge, decimal? DistanceKm);

public record EventDetailDto(int EventId, int OrganiserId, string Name, string Description, DateTime EventDate,
    string Location, decimal DistanceKm, EventType EventType, string? BannerVersion, int EnrolmentCount,
    IReadOnlyList<CategoryDto> Categories);

public record EnrolmentDto(int EnrolmentId, int EventId, string EventName, DateTime EventDate, string EventLocation,
    int CategoryId, string CategoryName, int ParticipantId, string ParticipantName, DateTime EnrolmentDate,
    EnrolmentStatus Status);

/// <summary>TotalFinishers = number of recorded finishers in the same event and category.</summary>
public record ResultDto(int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    int ParticipantId, string ParticipantName, int CategoryId, string CategoryName, TimeSpan FinishTime,
    int FinishPosition, int TotalFinishers);

public static class Mapper
{
    private static string? Version(string? blobName) =>
        blobName is null ? null : Path.GetFileNameWithoutExtension(blobName);

    public static UserDto ToDto(this AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.Role, u.Phone, u.DateOfBirth, u.CreatedAt, Version(u.ProfilePictureBlob));

    public static EventDto ToDto(this RaceEvent e, int enrolmentCount = 0) =>
        new(e.EventId, e.OrganiserId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType,
            Version(e.BannerBlob), enrolmentCount);

    public static CategoryDto ToDto(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.Description, c.CategoryType, c.MinAge, c.MaxAge, c.DistanceKm);

    public static EventDetailDto ToDetailDto(this RaceEvent e, int enrolmentCount = 0) =>
        new(e.EventId, e.OrganiserId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType,
            Version(e.BannerBlob), enrolmentCount,
            e.Categories.OrderBy(c => c.CategoryId).Select(c => c.ToDto()).ToList());

    /// <summary>Requires Event, Category and Participant to be loaded.</summary>
    public static EnrolmentDto ToDto(this EventEnrolment en) =>
        new(en.EnrolmentId, en.EventId, en.Event!.Name, en.Event.EventDate, en.Event.Location, en.CategoryId,
            en.Category!.Name, en.ParticipantId, en.Participant!.FullName, en.EnrolmentDate, en.Status);

    /// <summary>Requires Enrolment (with Event, Category and Participant) to be loaded.</summary>
    public static ResultDto ToDto(this RaceResult r, int totalFinishers = 0) =>
        new(r.ResultId, r.EnrolmentId, r.Enrolment!.EventId, r.Enrolment.Event!.Name, r.Enrolment.Event.EventDate,
            r.Enrolment.ParticipantId, r.Enrolment.Participant!.FullName, r.Enrolment.CategoryId,
            r.Enrolment.Category!.Name, r.FinishTime, r.FinishPosition, totalFinishers);

    public static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on < dateOfBirth.AddYears(age)) age--;
        return age;
    }
}
