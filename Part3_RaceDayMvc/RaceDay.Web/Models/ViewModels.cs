using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

// ---------------------------------------------------------------- account

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repeat your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Choose how you will use RaceDay.")]
    public string Role { get; set; } = Roles.Participant;

    [StringLength(20)]
    public string? Phone { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Organiser invite code")]
    public string? OrganiserCode { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [StringLength(20)]
    public string? Phone { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    // Read-only, filled in by the controller (nullable so they are not treated as required form fields)
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? PictureVersion { get; set; }
}

// ---------------------------------------------------------------- events

public class EventFilter
{
    public EventType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Q { get; set; }
}

public class HomeViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public HashSet<int> EnrolledEventIds { get; set; } = new();
    public EventFilter Filter { get; set; } = new();
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public bool IsPast { get; set; }
    public EnrolmentDto? MyEnrolment { get; set; }
    public int? MyAge { get; set; }
    public bool HasDateOfBirth { get; set; }
    public List<ResultDto> Results { get; set; } = new();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Enter the event name."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter a description."), StringLength(1000)]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Choose the date and time of the event.")]
    [Display(Name = "Date and time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter the location."), StringLength(150)]
    public string Location { get; set; } = "";

    [Required(ErrorMessage = "Enter the distance in kilometres.")]
    [Range(0.1, 1000, ErrorMessage = "The distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Choose the event type.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? ExistingBannerVersion { get; set; }
}

// ---------------------------------------------------------------- organiser

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public List<EventDto> Upcoming { get; set; } = new();
    public int TotalEnrolments { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Enter a category name."), StringLength(50)]
    public string Name { get; set; } = "";

    [StringLength(255)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose a category type.")]
    [Display(Name = "Category type")]
    public CategoryType? CategoryType { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Enter an age between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(0.1, 1000, ErrorMessage = "The distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }
}

public class CategoriesPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public CategoryFormViewModel Form { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public EnrolmentDto Enrolment { get; set; } = null!;
    public ResultDto? Result { get; set; }
}

public class ResultsPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public bool EventHasHappened { get; set; }
    public List<ResultRow> Rows { get; set; } = new();
}

public class ErrorViewModel
{
    public int Status { get; set; }
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "";
}
