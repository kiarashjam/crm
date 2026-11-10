using System.Globalization;

namespace ACI.Application.Common;

/// <summary>
/// The periods a subscription bills: calendar months, quarters, half years and years, each
/// starting on the same day of the month.
/// </summary>
/// <remarks>
/// <para>
/// Billing used to step 30, 91, 182 or 365 days, so a "monthly" subscription billed on 1
/// January was billed again on 31 January and 2 March, thirteen times in some years, and
/// drifted off the club's statement month. A period now runs to the same day of the month
/// one, three, six or twelve months later.
/// </para>
/// <para>
/// A subscription that starts on a day some months lack (the 29th, 30th or 31st) bills on
/// the last day of those months and goes back to its own day after: 31 January, 28
/// February, 31 March. That is why the day it started on (its anchor) is kept rather than
/// read from the last date billed.
/// </para>
/// <para>
/// Worked in UTC, by date: a period that starts on 1 December starts at the moment the
/// subscription's first date had (midnight UTC for a date picked on a screen), so summer
/// time never moves it to the 30th.
/// </para>
/// </remarks>
public static class BillingPeriods
{
    /// <summary>The months in one period of a cadence: Monthly 1, Quarterly 3, SemiAnnual 6, Annual 12.</summary>
    public static int MonthsIn(string? cadence) => cadence switch
    {
        "Quarterly" => 3,
        "SemiAnnual" => 6,
        "Annual" => 12,
        _ => 1,
    };

    /// <summary>The start of the period after the one that starts at <paramref name="periodStart"/>.</summary>
    /// <param name="anchorDay">The day of the month the subscription's periods start on (1 to 31).</param>
    public static DateTime Next(DateTime periodStart, string? cadence, int anchorDay)
    {
        var moved = periodStart.AddMonths(MonthsIn(cadence));
        var day = Math.Min(Math.Clamp(anchorDay, 1, 31), DateTime.DaysInMonth(moved.Year, moved.Month));
        return new DateTime(moved.Year, moved.Month, day, 0, 0, 0, DateTimeKind.Utc).Add(periodStart.TimeOfDay);
    }

    /// <summary>
    /// The first period start at or after <paramref name="from"/> in the series that runs
    /// through <paramref name="periodStart"/>: where billing picks up after a pause.
    /// </summary>
    public static DateTime FirstOnOrAfter(DateTime periodStart, string? cadence, int anchorDay, DateTime from)
    {
        var start = periodStart;
        // A hundred years of months is far more than any subscription has been paused for;
        // the bound only stops a date from another century looping for ever.
        for (var i = 0; start < from && i < 1200; i++) start = Next(start, cadence, anchorDay);
        return start;
    }

    /// <summary>A period in words, its first and last day: "1 Dec 2026 – 31 Dec 2026".</summary>
    public static string Label(DateTime start, DateTime next) =>
        string.Create(CultureInfo.InvariantCulture, $"{start:d MMM yyyy} – {next.AddDays(-1):d MMM yyyy}");
}
