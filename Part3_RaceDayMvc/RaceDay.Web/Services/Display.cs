using System.Globalization;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Formatting helpers used by the views so dates, times and labels look the same on every page.</summary>
public static class Display
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Date(DateTime d) => d.ToString("dd MMM yyyy", Inv);
    public static string DateTimeLong(DateTime d) => d.ToString("ddd, dd MMM yyyy 'at' HH:mm", Inv);
    public static string ShortDate(DateTime d) => d.ToString("dd MMM", Inv);
    public static string Time(TimeSpan t) => t.ToString(@"h\:mm\:ss", Inv);
    public static string Km(decimal km) => km.ToString("0.##", Inv) + " km";

    /// <summary>1 -> 1st, 2 -> 2nd, 11 -> 11th, 47 -> 47th.</summary>
    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 13) return n + "th";
        return (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
    }

    public static string TypeIcon(EventType t) => t switch
    {
        EventType.Run => "bi-lightning-charge-fill",
        EventType.Walk => "bi-person-walking",
        _ => "bi-bicycle"
    };

    public static string TypeClass(EventType t) => "rd-type-" + t.ToString().ToLowerInvariant();

    public static string CategoryRule(CategoryDto c)
    {
        if (c.CategoryType == CategoryType.Distance)
            return c.DistanceKm is { } km ? Km(km) : "Distance";
        return (c.MinAge, c.MaxAge) switch
        {
            ({ } min, { } max) => $"Ages {min} to {max}",
            ({ } min, null) => $"Age {min} and over",
            (null, { } max) => $"Age {max} and under",
            _ => "Age category"
        };
    }

    public static string DaysToGo(DateTime date)
    {
        var days = (int)Math.Ceiling((date - DateTime.UtcNow).TotalDays);
        return days switch
        {
            <= 0 => "Today",
            1 => "Tomorrow",
            _ => $"In {days} days"
        };
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
