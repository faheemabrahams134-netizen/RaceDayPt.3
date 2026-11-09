using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";

    /// <summary>"Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = "";

    [StringLength(20)] public string? Phone { get; set; }

    /// <summary>Required for Participants (used to check age-category eligibility).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Invite code. Required to register as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [StringLength(20)] public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}

public class EventRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = "";
    [Required, Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
    [Required] public EventType? EventType { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [StringLength(255)] public string? Description { get; set; }
    [Required] public CategoryType? CategoryType { get; set; }
    [Range(0, 120)] public int? MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
}

public class EnrolRequest
{
    [Required] public int? CategoryId { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss (must be under 24 hours).</summary>
    [Required] public TimeSpan? FinishTime { get; set; }
    [Required, Range(1, 100000)] public int? FinishPosition { get; set; }
}

public class EnrolmentStatusRequest
{
    /// <summary>"Pending" or "Confirmed".</summary>
    [Required] public EnrolmentStatus? Status { get; set; }
}
