using System.Globalization;
using System.Text.Json;

namespace ACI.Application.Services;

/// <summary>
/// The days and hours a rule's emails may go out, in the workspace's own time zone: Monday
/// to Friday, 09:00 to 17:30, say.
/// </summary>
public sealed class SendWindow
{
    /// <summary>The days it is open, 1 for Monday to 7 for Sunday.</summary>
    public List<int> Days { get; set; } = new();

    /// <summary>When it opens each of those days, as HH:mm.</summary>
    public string Start { get; set; } = "09:00";

    /// <summary>When it closes, as HH:mm, after <see cref="Start"/>.</summary>
    public string End { get; set; } = "17:00";
}

/// <summary>Reading a rule's send window, and when an email due now may go.</summary>
/// <remarks>
/// Worked out on the workspace's wall clock (Organization.TimeZone): "09:00" is nine in
/// Zurich in winter and in summer alike. On the night the clocks go forward, an opening
/// that falls in the skipped hour is taken at the first minute that exists after it.
/// </remarks>
public static class SendWindows
{
    public static SendWindow? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var window = JsonSerializer.Deserialize<SendWindow>(json, AutomationSteps.Json);
            return window is { Days.Count: > 0 } && Problem(window) == null ? window : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Serialize(SendWindow? window) =>
        window == null ? null : JsonSerializer.Serialize(new SendWindow
        {
            Days = window.Days.Distinct().Order().ToList(),
            Start = Time(window.Start)!.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
            End = Time(window.End)!.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
        }, AutomationSteps.Json);

    /// <summary>What is wrong with a window, as a person reads it; null when nothing is.</summary>
    public static string? Problem(SendWindow window)
    {
        if (window.Days.Count == 0) return "Pick at least one day the emails may go.";
        if (window.Days.Any(d => d is < 1 or > 7)) return "Days run from 1 (Monday) to 7 (Sunday).";
        if (Time(window.Start) is not { } start || Time(window.End) is not { } end) return "Give the hours as HH:mm, such as 09:00.";
        return start < end ? null : "The send window has to close after it opens on the same day.";
    }

    /// <summary>
    /// When an email due at <paramref name="utcNow"/> may go: then, inside the window, or else
    /// at its next opening. Null window: at once.
    /// </summary>
    public static DateTime NextOpening(SendWindow? window, TimeZoneInfo zone, DateTime utcNow)
    {
        if (window == null || Problem(window) != null) return utcNow;
        var start = Time(window.Start)!.Value;
        var end = Time(window.End)!.Value;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var today = DateOnly.FromDateTime(local);
        var now = TimeOnly.FromDateTime(local);
        for (var offset = 0; offset <= 7; offset++)
        {
            var day = today.AddDays(offset);
            if (!window.Days.Contains(IsoDay(day.DayOfWeek))) continue;
            if (offset == 0)
            {
                if (now >= start && now < end) return utcNow;
                if (now >= end) continue;
            }
            return ToUtc(day, start, zone);
        }
        return utcNow;
    }

    /// <summary>Whether an email may go at <paramref name="utcNow"/>.</summary>
    public static bool IsOpen(SendWindow? window, TimeZoneInfo zone, DateTime utcNow) => NextOpening(window, zone, utcNow) <= utcNow;

    /// <summary>The window in words, for the run log: "Mon–Fri 09:00–17:00".</summary>
    public static string Describe(SendWindow window)
    {
        var names = window.Days.Distinct().Order().Select(d => CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.AbbreviatedDayNames[d % 7]).ToList();
        var days = window.Days.Distinct().Order().ToList();
        var consecutive = days.Count > 2 && days.Zip(days.Skip(1)).All(p => p.Second == p.First + 1);
        var dayText = consecutive ? $"{names[0]}–{names[^1]}" : string.Join(", ", names);
        return $"{dayText} {window.Start}–{window.End}";
    }

    private static int IsoDay(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    private static TimeOnly? Time(string? raw) =>
        TimeOnly.TryParseExact((raw ?? "").Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static DateTime ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var at = day.ToDateTime(time, DateTimeKind.Unspecified);
        // The hour the clocks skip has no such time: the window opens when they land.
        while (zone.IsInvalidTime(at)) at = at.AddMinutes(1);
        return TimeZoneInfo.ConvertTimeToUtc(at, zone);
    }
}
