namespace RaceDay.Api.Models;

/// <summary>The two roles a RaceDay user can have.</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

/// <summary>The kind of event: a run, a walk or a cycle.</summary>
public enum EventType
{
    Run,
    Walk,
    Cycle
}

/// <summary>Whether an enrolment has been confirmed by the Organiser yet.</summary>
public enum EnrolmentStatus
{
    Pending,
    Confirmed
}

/// <summary>Whether a category is defined by age bracket or by distance.</summary>
public enum CategoryType
{
    Age,
    Distance
}
