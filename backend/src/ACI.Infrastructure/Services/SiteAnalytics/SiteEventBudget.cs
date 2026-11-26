using ACI.Application.Configuration;
using ACI.Application.Services;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <summary>
/// How many site analytics events this server may still store today (Zurich): at most
/// SiteAnalytics:DailyEventCap a day, so a flood the rate limits let through fills the table
/// by a bounded amount. Past it, events are dropped until the next day.
/// </summary>
/// <remarks>
/// In memory, per process: a restart starts the day's count again, and several servers each
/// have their own. A singleton.
/// </remarks>
public sealed class SiteEventBudget
{
    private readonly object _gate = new();
    private DateOnly _day;
    private int _taken;
    private bool _told;

    public SiteEventBudget(IOptions<SiteAnalyticsSettings> settings) => Cap = Math.Max(0, settings.Value.DailyEventCap);

    /// <summary>The events one server stores in a day.</summary>
    public int Cap { get; }

    /// <summary>
    /// Takes up to <paramref name="wanted"/> of the day's events. <c>Granted</c> is how many may
    /// be stored; <c>FirstRefusal</c> is true the first time in the day some are refused, so the
    /// cap is logged once a day, not once a post.
    /// </summary>
    public (int Granted, bool FirstRefusal) Take(int wanted, DateTime nowUtc)
    {
        if (wanted <= 0) return (0, false);
        var day = SiteAnalyticsRules.DayOf(nowUtc);
        lock (_gate)
        {
            if (day != _day)
            {
                _day = day;
                _taken = 0;
                _told = false;
            }
            var granted = Math.Min(wanted, Math.Max(0, Cap - _taken));
            _taken += granted;
            var first = granted < wanted && !_told;
            if (first) _told = true;
            return (granted, first);
        }
    }
}
