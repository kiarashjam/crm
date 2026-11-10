using System.Globalization;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// A workspace's currency, locale and time zone, and the rules for reading them.
/// </summary>
/// <remarks>
/// <para>
/// There was no organisation currency or time zone anywhere: a deal saved without a
/// currency was counted and shown as USD for a club that bills in francs, and every
/// month a report or a forecast is cut into began at UTC midnight. The workspace now
/// says what it works in, and new deals, lead conversions and month boundaries follow
/// it. Nothing converts between currencies: a different currency on a deal is a
/// different total, never an addition.
/// </para>
/// <para>
/// A personal workspace has no organisation row to hold these, so it works in the
/// defaults.
/// </para>
/// </remarks>
public static partial class RegionalSettings
{
    public const string DefaultCurrency = "CHF";
    public const string DefaultLocale = "de-CH";
    public const string DefaultTimeZone = "Europe/Zurich";

    // A language, an optional script and an optional region, as browsers name them
    // ("de-CH", "en", "zh-Hant-TW"). Underscores and private tags are refused: .NET
    // reads "de_CH" as a sort order, and a browser would not read it at all.
    [GeneratedRegex("^[a-z]{2,3}(-[A-Z][a-z]{3})?(-([A-Z]{2}|[0-9]{3}))?$")]
    private static partial Regex LocaleShape();

    /// <summary>
    /// An ISO 4217 code ("chf " becomes "CHF"), or false when the text is not three letters.
    /// </summary>
    /// <remarks>
    /// Only the shape is checked. The browser formats any three-letter code, and a list
    /// of "known" codes would differ between the hosts this runs on.
    /// </remarks>
    public static bool TryCurrency(string? raw, out string code)
    {
        code = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 3 && code.All(c => c is >= 'A' and <= 'Z')) return true;
        code = string.Empty;
        return false;
    }

    /// <summary>A locale this host knows by name, written as browsers write it, or false.</summary>
    public static bool TryLocale(string? raw, out string name)
    {
        name = (raw ?? string.Empty).Trim();
        if (!LocaleShape().IsMatch(name))
        {
            name = string.Empty;
            return false;
        }
        try
        {
            name = CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name;
            return true;
        }
        catch (CultureNotFoundException)
        {
            name = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// A time zone by its IANA name ("Europe/Zurich"), or false. A Windows name is read
    /// and given back as its IANA one, because the browser formats dates by IANA names only.
    /// </summary>
    public static bool TryTimeZone(string? raw, out string id)
    {
        id = (raw ?? string.Empty).Trim();
        if (id.Length == 0 || id.Length > 64 || !TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
        {
            id = string.Empty;
            return false;
        }
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana)) id = iana;
        return true;
    }

    /// <summary>
    /// The zone a workspace's days and months are counted in: its own, or the default
    /// when it names none this host knows.
    /// </summary>
    /// <remarks>
    /// A Windows host without ICU (Windows Server 2019 and older) knows zones only by their
    /// Windows names, so the default is also looked up by its Windows name there rather
    /// than falling through to UTC, which is where every month began before.
    /// </remarks>
    public static TimeZoneInfo ZoneOf(string? id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById((id ?? string.Empty).Trim(), out var zone) ? zone
        : TimeZoneInfo.TryFindSystemTimeZoneById(DefaultTimeZone, out var fallback) ? fallback
        : TimeZoneInfo.TryFindSystemTimeZoneById(DefaultTimeZoneWindowsName, out var windows) ? windows
        : TimeZoneInfo.Utc;

    /// <summary>The Windows name of <see cref="DefaultTimeZone"/>.</summary>
    private const string DefaultTimeZoneWindowsName = "W. Europe Standard Time";

    /// <summary>The calendar day it is in <paramref name="zone"/> at <paramref name="utcNow"/>.</summary>
    public static DateOnly TodayIn(TimeZoneInfo zone, DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));

    /// <summary>
    /// The instant a local day starts in <paramref name="zone"/>, as UTC. A zone whose
    /// clocks jump at midnight skips that midnight, so the day starts when the clocks land.
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo zone)
    {
        var at = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(at)) at = at.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(at, zone);
    }
}
