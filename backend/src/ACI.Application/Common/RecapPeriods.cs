using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>
/// One period a recap covers, on a workspace's calendar: its first and last day, the instants it
/// runs between (local midnights, as UTC), and when its recap falls due.
/// </summary>
/// <param name="Frequency"><see cref="RecapFrequencies"/>.</param>
/// <param name="FirstDay">The day, the Monday, or the 1st.</param>
/// <param name="LastDay">The same day, the Sunday, or the month's last day.</param>
/// <param name="FromUtc">The start of <paramref name="FirstDay"/> in the workspace's time zone.</param>
/// <param name="ToUtc">The start of the day after <paramref name="LastDay"/>: the period runs up to it, not including it.</param>
/// <param name="DueUtc">07:00 on the day after <paramref name="LastDay"/>, when its recap may go.</param>
public sealed record RecapPeriod(string Frequency, DateOnly FirstDay, DateOnly LastDay, DateTime FromUtc, DateTime ToUtc, DateTime DueUtc);

/// <summary>
/// When recaps fall due and what each covers: daily from 07:00 for the day before, weekly on
/// Monday from 07:00 for Monday to Sunday just ended, monthly on the 1st from 07:00 for the month
/// just ended, all in the workspace's time zone.
/// </summary>
/// <remarks>
/// Days are counted on the calendar, never as 24 hours: the day the clocks go forward has 23 of
/// them and the day they go back 25, and each is still one day. A zone whose clocks jump at
/// midnight or at seven skips that moment; the time the clocks land on stands for it.
/// </remarks>
public static class RecapPeriods
{
    /// <summary>When a recap falls due, on the workspace's clock.</summary>
    public static readonly TimeSpan DueAt = TimeSpan.FromHours(7);

    /// <summary>
    /// The latest period of <paramref name="frequency"/> whose recap is due at <paramref name="nowUtc"/>:
    /// the one that fell due last, at or before now. Older ones are never sent: a recap missed
    /// while the app was down goes at the next pass, and only the latest of each frequency.
    /// </summary>
    public static RecapPeriod LatestDue(string frequency, DateTime nowUtc, TimeZoneInfo zone)
    {
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var today = RegionalSettings.TodayIn(zone, now);
        // The first day of the period after the one we want: today, this Monday, the 1st.
        var next = frequency switch
        {
            RecapFrequencies.Weekly => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
            RecapFrequencies.Monthly => new DateOnly(today.Year, today.Month, 1),
            RecapFrequencies.Daily => today,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "A recap is daily, weekly or monthly."),
        };
        // Before seven on that day, it is the period before that which is due.
        if (now < LocalUtc(next, DueAt, zone)) next = Step(frequency, next, -1);
        return For(frequency, Step(frequency, next, -1), zone);
    }

    /// <summary>The period of <paramref name="frequency"/> that starts on <paramref name="firstDay"/>.</summary>
    public static RecapPeriod For(string frequency, DateOnly firstDay, TimeZoneInfo zone)
    {
        var after = Step(frequency, firstDay, 1);
        return new RecapPeriod(frequency, firstDay, after.AddDays(-1),
            RegionalSettings.StartOfDayUtc(firstDay, zone), RegionalSettings.StartOfDayUtc(after, zone), LocalUtc(after, DueAt, zone));
    }

    /// <summary>The period just before <paramref name="period"/>: the day, the week or the month before.</summary>
    public static RecapPeriod Previous(RecapPeriod period, TimeZoneInfo zone) =>
        For(period.Frequency, Step(period.Frequency, period.FirstDay, -1), zone);

    /// <summary>A period's first day moved by <paramref name="periods"/> of its length.</summary>
    private static DateOnly Step(string frequency, DateOnly day, int periods) => frequency switch
    {
        RecapFrequencies.Weekly => day.AddDays(7 * periods),
        RecapFrequencies.Monthly => day.AddMonths(periods),
        _ => day.AddDays(periods),
    };

    /// <summary>The instant a local day and time is in <paramref name="zone"/>; a time the clocks skip stands for the one they land on.</summary>
    private static DateTime LocalUtc(DateOnly day, TimeSpan time, TimeZoneInfo zone)
    {
        var at = day.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(at)) at = at.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(at, zone);
    }
}
