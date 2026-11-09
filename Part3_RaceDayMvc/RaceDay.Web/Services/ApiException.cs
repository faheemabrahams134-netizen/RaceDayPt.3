namespace RaceDay.Web.Services;

/// <summary>A failed call to the RaceDay API, carrying a message that is safe to show to the user.</summary>
public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
