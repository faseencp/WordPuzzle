namespace TheCallAttendanceApi.Models;

/// <summary>
/// Fixed list of countries for "THE CALL" -- validated server-side against
/// this same list so a submission can't record a country outside it (typos,
/// tampering, or a stale client). Keep this in sync with the frontend's list.
/// </summary>
public static class Countries
{
    public static readonly string[] Allowed =
    {
        "Bahrain", "Kuwait", "Oman", "Qatar", "Saudi East", "Saudi West", "UAE", "Egypt",
        "Canada", "England", "Georgia", "Germany", "Ireland", "Japan", "Kazakhstan",
        "Kyrgystan", "Malaysia", "Maldives", "Poland", "Russia", "Scotland", "Spain",
        "Turkey", "USA", "Uzbekistan"
    };

    public static bool IsValid(string? country) =>
        country != null && Allowed.Contains(country, StringComparer.OrdinalIgnoreCase);
}

public class AttendanceEntry
{
    public int Id { get; set; }
    public string Kind { get; set; } = ""; // "confirmation" or "attendance"
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public DateTime SubmittedAtUtc { get; set; }
}

public record SubmitRequest(string Name, string Country, string DeviceId);

public record SubmitResponse(bool Accepted);

public record CountrySummary(string Country, int Count);

public record SummaryResponse(int Total, CountrySummary[] ByCountry);
