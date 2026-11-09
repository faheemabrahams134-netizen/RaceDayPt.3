namespace RaceDay.Api.Models;

/// <summary>An Organiser or a Participant (table: User).</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Participant;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Name of the profile picture blob in Azure Blob Storage (null = no picture).</summary>
    public string? ProfilePictureBlob { get; set; }

    public List<RaceEvent> OrganisedEvents { get; set; } = new();
    public List<EventEnrolment> Enrolments { get; set; } = new();
}

/// <summary>A run, walk or cycle event created by an Organiser (table: Event).</summary>
public class RaceEvent
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Name of the banner image blob in Azure Blob Storage (null = no banner).</summary>
    public string? BannerBlob { get; set; }

    public AppUser? Organiser { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<EventEnrolment> Enrolments { get; set; } = new();
}

/// <summary>An age or distance category within an event.</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public CategoryType CategoryType { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? DistanceKm { get; set; }

    public RaceEvent? Event { get; set; }
    public List<EventEnrolment> Enrolments { get; set; } = new();
}

/// <summary>Links a Participant, an Event and the Category they entered.</summary>
public class EventEnrolment
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Pending;

    public RaceEvent? Event { get; set; }
    public AppUser? Participant { get; set; }
    public Category? Category { get; set; }
    public RaceResult? Result { get; set; }
}

/// <summary>Finish time and position for one enrolment (table: Result).</summary>
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public EventEnrolment? Enrolment { get; set; }
}
