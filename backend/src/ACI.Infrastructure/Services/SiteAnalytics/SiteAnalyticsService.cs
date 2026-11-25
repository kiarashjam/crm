using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <inheritdoc cref="ISiteAnalytics"/>
/// <remarks>
/// <para>
/// What can be counted in SQL is: the daily series (distinct visitors and sessions per day),
/// the top pages, the events, the pages sessions entered and left on, and one row per session
/// (its first and last event, its page views, its source, device and browser). Only those
/// session rows, at most <see cref="SiteAnalyticsRules.MaxSessionsRead"/> a period, newest first,
/// are folded in memory into bounce rate, durations and the per-session top lists.
/// </para>
/// <para>
/// The queries are built by <see cref="SiteAnalyticsQueries"/>, so a test can check that SQL
/// Server translates each of them without a database.
/// </para>
/// </remarks>
public sealed class SiteAnalyticsService : ISiteAnalytics
{
    /// <summary>How far the browser's clock may be from the server's before its time is ignored.</summary>
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(2);

    /// <summary>Events read for the live view: the last five minutes of a busy site still fit.</summary>
    private const int LiveRead = 5000;

    private readonly AppDbContext _db;
    private readonly SiteAnalyticsKeys _keys;
    private readonly ILogger<SiteAnalyticsService> _logger;

    public SiteAnalyticsService(AppDbContext db, SiteAnalyticsKeys keys, ILogger<SiteAnalyticsService> logger)
    {
        _db = db;
        _keys = keys;
        _logger = logger;
    }

    // ── Writing ──────────────────────────────────────────────────────────────

    public async Task IngestAsync(SiteVisit visit, IReadOnlyList<SiteEventInput> events, DateTime nowUtc, CancellationToken ct = default)
    {
        if (events.Count == 0) return;
        if (visit.Bot is { } bot)
        {
            await CountBotAsync(SiteAnalyticsRules.DayOf(nowUtc), bot, events.Count, ct);
            return;
        }

        var rows = new List<SiteEvent>(events.Count);
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var at = WhenOf(e.AtUtc, nowUtc, i, events.Count);
            var day = SiteAnalyticsRules.DayOf(at);
            rows.Add(new SiteEvent
            {
                OccurredAtUtc = at,
                Day = day,
                Type = e.Type,
                Name = e.Name,
                Path = e.Path,
                Title = e.Title,
                ReferrerHost = e.ReferrerHost,
                UtmSource = e.UtmSource,
                UtmMedium = e.UtmMedium,
                UtmCampaign = e.UtmCampaign,
                UtmTerm = e.UtmTerm,
                UtmContent = e.UtmContent,
                Source = e.Source,
                VisitorHash = await _keys.VisitorAsync(day, visit.Address, visit.UserAgent, ct),
                SessionKey = await _keys.SessionAsync(day, e.SessionId, ct),
                Device = visit.Device,
                Browser = visit.Browser,
                Os = visit.Os,
                Language = e.Language ?? visit.Language,
                ScreenBucket = e.ScreenBucket,
                SignedIn = e.SignedIn,
            });
        }
        _db.SiteEvents.AddRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// When the event happened: the browser's time when it is within a few minutes of the
    /// server's (a batch waits up to five seconds, a beacon on leaving goes at once), else the
    /// server's, a millisecond apart per event so the batch keeps its order.
    /// </summary>
    public static DateTime WhenOf(DateTime? atUtc, DateTime nowUtc, int index, int count)
    {
        if (atUtc is { } at && at >= nowUtc - PastTolerance && at <= nowUtc + FutureTolerance)
            return at > nowUtc ? nowUtc : at;
        return nowUtc.AddMilliseconds(index - (count - 1));
    }

    /// <summary>Adds <paramref name="hits"/> to the robot's count for the day.</summary>
    private async Task CountBotAsync(DateOnly day, string bot, int hits, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var updated = await _db.SiteBotHits.Where(h => h.Day == day && h.Bot == bot)
                    .ExecuteUpdateAsync(set => set.SetProperty(h => h.Hits, h => h.Hits + hits), ct);
                if (updated > 0) return;
                var row = new SiteBotHit { Day = day, Bot = bot, Hits = hits };
                _db.SiteBotHits.Add(row);
                try
                {
                    await _db.SaveChangesAsync(ct);
                    return;
                }
                catch (DbUpdateException)
                {
                    // Another request made the day's row first: add to it.
                    _db.Entry(row).State = EntityState.Detached;
                }
            }
            return;
        }
        // The in-memory test host has no UPDATE statements; one process, so no race.
        var existing = await _db.SiteBotHits.AsTracking().FirstOrDefaultAsync(h => h.Day == day && h.Bot == bot, ct);
        if (existing == null) _db.SiteBotHits.Add(new SiteBotHit { Day = day, Bot = bot, Hits = hits });
        else existing.Hits += hits;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PurgeAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var cutoff = SiteAnalyticsRules.DayOf(nowUtc).AddDays(-SiteAnalyticsRules.RetentionDays);
        if (_db.Database.IsRelational())
        {
            var events = await _db.SiteEvents.Where(e => e.Day < cutoff).ExecuteDeleteAsync(ct);
            await _db.SiteBotHits.Where(h => h.Day < cutoff).ExecuteDeleteAsync(ct);
            await _db.SiteAnalyticsDigests.Where(d => d.Day < cutoff).ExecuteDeleteAsync(ct);
            return events;
        }
        var old = await _db.SiteEvents.AsTracking().Where(e => e.Day < cutoff).ToListAsync(ct);
        _db.SiteEvents.RemoveRange(old);
        _db.SiteBotHits.RemoveRange(await _db.SiteBotHits.AsTracking().Where(h => h.Day < cutoff).ToListAsync(ct));
        _db.SiteAnalyticsDigests.RemoveRange(await _db.SiteAnalyticsDigests.AsTracking().Where(d => d.Day < cutoff).ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
        return old.Count;
    }

    // ── Reading ──────────────────────────────────────────────────────────────

    public async Task<SiteSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var length = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(length - 1));

        var daily = await DailyAsync(from, to, ct);
        var sessions = await SessionRowsAsync(from, to, ct);
        var totals = Totals(daily, sessions, await BotHitsAsync(from, to, ct));
        var previous = Totals(await DailyAsync(previousFrom, previousTo, ct), await SessionRowsAsync(previousFrom, previousTo, ct),
            await BotHitsAsync(previousFrom, previousTo, ct));

        var top = SiteAnalyticsRules.TopListSize;
        var pages = await SiteAnalyticsQueries.TopPages(_db, from, to).ToListAsync(ct);
        var entries = await SiteAnalyticsQueries.EntryPages(_db, from, to).ToListAsync(ct);
        var exits = await SiteAnalyticsQueries.ExitPages(_db, from, to).ToListAsync(ct);
        var events = await SiteAnalyticsQueries.Events(_db, from, to).ToListAsync(ct);

        return new SiteSummaryDto(
            SiteAnalyticsRules.Format(from),
            SiteAnalyticsRules.Format(to),
            totals,
            previous,
            daily,
            pages.Select(p => new SitePageDto(p.Path, p.Views, p.Visitors)).ToList(),
            entries.Select(p => new SiteEntryPageDto(p.Path, p.Sessions, p.Visitors, p.Sessions)).ToList(),
            exits.Select(p => new SiteEntryPageDto(p.Path, p.Sessions, p.Visitors, p.Sessions)).ToList(),
            sessions.GroupBy(s => s.Source ?? SiteAnalyticsRules.Direct)
                .Select(g => new SiteSourceDto(g.Key, g.Count(), g.Select(s => s.Visitor).Distinct().Count()))
                .OrderByDescending(s => s.Sessions).ThenBy(s => s.Source, StringComparer.Ordinal).Take(top).ToList(),
            sessions.Where(s => s.Referrer != null).GroupBy(s => s.Referrer!)
                .Select(g => new SiteReferrerDto(g.Key, g.Count()))
                .OrderByDescending(r => r.Sessions).ThenBy(r => r.Host, StringComparer.Ordinal).Take(top).ToList(),
            sessions.Where(s => s.Campaign != null).GroupBy(s => (Campaign: s.Campaign!, s.UtmSource, s.UtmMedium))
                .Select(g => new SiteCampaignDto(g.Key.Campaign, g.Key.UtmSource, g.Key.UtmMedium, g.Count()))
                .OrderByDescending(c => c.Sessions).ThenBy(c => c.Campaign, StringComparer.Ordinal).Take(top).ToList(),
            Named(sessions, s => s.Device),
            Named(sessions, s => s.Browser),
            Named(sessions, s => s.Os),
            Named(sessions, s => s.Language),
            events.Select(e => new SiteEventCountDto(e.Name, e.Count)).ToList());
    }

    /// <summary>Every day from <paramref name="from"/> to <paramref name="to"/>, zeros included.</summary>
    private async Task<List<SiteDailyDto>> DailyAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var counted = (await SiteAnalyticsQueries.Daily(_db, from, to).ToListAsync(ct)).ToDictionary(d => d.Day);
        var days = new List<SiteDailyDto>(to.DayNumber - from.DayNumber + 1);
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            days.Add(counted.TryGetValue(day, out var d)
                ? new SiteDailyDto(SiteAnalyticsRules.Format(day), d.Visitors, d.Sessions, d.PageViews, d.Signups)
                : new SiteDailyDto(SiteAnalyticsRules.Format(day), 0, 0, 0, 0));
        }
        return days;
    }

    private async Task<List<SiteAnalyticsQueries.SessionRow>> SessionRowsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await SiteAnalyticsQueries.Sessions(_db, from, to).Take(SiteAnalyticsRules.MaxSessionsRead).ToListAsync(ct);
        if (rows.Count == SiteAnalyticsRules.MaxSessionsRead)
            _logger.LogWarning("Site analytics: more than {Max} sessions from {From} to {To}; the session figures cover the latest {Max}",
                SiteAnalyticsRules.MaxSessionsRead, from, to, SiteAnalyticsRules.MaxSessionsRead);
        return rows;
    }

    private Task<int> BotHitsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        _db.SiteBotHits.AsNoTracking().Where(h => h.Day >= from && h.Day <= to).SumAsync(h => h.Hits, ct);

    private static SiteTotalsDto Totals(IReadOnlyList<SiteDailyDto> daily, IReadOnlyList<SiteAnalyticsQueries.SessionRow> sessions, int botHits)
    {
        // A visitor's hash is made with the day's salt, so each day's distinct visitors are
        // distinct from every other day's: their sum is the period's "visitors". A session's key
        // is salted the same way, so summing the days counts each session once (a visit across
        // midnight is two).
        var visitors = daily.Sum(d => d.Visitors);
        var sessionCount = daily.Sum(d => d.Sessions);
        var pageViews = daily.Sum(d => d.PageViews);
        var signups = daily.Sum(d => d.Signups);
        var read = sessions.Count;
        var bounced = sessions.Count(s => s.PageViews == 1 && s.Interactions == 0);
        return new SiteTotalsDto(
            visitors,
            sessionCount,
            pageViews,
            read == 0 ? 0 : Math.Round((double)bounced / read, 4),
            sessionCount == 0 ? 0 : Math.Round((double)pageViews / sessionCount, 2),
            read == 0 ? 0 : Math.Round(sessions.Average(s => (s.End - s.Start).TotalSeconds), 1),
            signups,
            botHits);
    }

    private static List<SiteNamedCountDto> Named(IEnumerable<SiteAnalyticsQueries.SessionRow> sessions, Func<SiteAnalyticsQueries.SessionRow, string?> of) =>
        sessions.GroupBy(s => of(s) ?? "unknown")
            .Select(g => new SiteNamedCountDto(g.Key, g.Count()))
            .OrderByDescending(n => n.Sessions).ThenBy(n => n.Name, StringComparer.Ordinal)
            .Take(SiteAnalyticsRules.TopListSize)
            .ToList();

    public async Task<SiteLiveDto> LiveAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var now = await SiteAnalyticsQueries.Since(_db, nowUtc - SiteAnalyticsRules.LiveWindow, LiveRead).ToListAsync(ct);

        // Where each session is: the page of its latest event (newest first, so its first here),
        // leaving out a click that left the site.
        var pages = now
            .Where(e => !(e.Type == SiteEventTypes.Click && e.Name == SiteAnalyticsRules.Outbound))
            .GroupBy(e => e.SessionKey)
            .Select(g => g.First())
            .GroupBy(e => e.Path)
            .Select(g => new SiteLivePageDto(g.Key, g.Select(e => e.VisitorHash).Distinct().Count()))
            .OrderByDescending(p => p.Visitors).ThenBy(p => p.Path, StringComparer.Ordinal)
            .Take(SiteAnalyticsRules.TopListSize)
            .ToList();

        var recent = (await SiteAnalyticsQueries.Latest(_db, SiteAnalyticsRules.RecentEvents).ToListAsync(ct))
            .Select(e => new SiteLiveEventDto(e.At, e.Type, e.Path, e.Name, e.Source, e.Device))
            .ToList();

        return new SiteLiveDto(
            now.Select(e => e.VisitorHash).Distinct().Count(),
            now.Select(e => e.SessionKey).Distinct().Count(),
            pages,
            recent);
    }

    public async Task<IReadOnlyList<SiteSessionDto>> SessionsAsync(DateOnly day, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, SiteAnalyticsRules.MaxSessions);
        var keys = (await SiteAnalyticsQueries.LatestSessions(_db, day, limit).ToListAsync(ct)).Select(s => s.Key).ToList();
        if (keys.Count == 0) return [];

        var bySession = (await SiteAnalyticsQueries.Steps(_db, day, keys).ToListAsync(ct))
            .GroupBy(e => e.SessionKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sessions = new List<SiteSessionDto>(keys.Count);
        foreach (var key in keys)
        {
            if (!bySession.TryGetValue(key, out var steps) || steps.Count == 0) continue;
            var first = steps[0];
            var last = steps[^1];
            sessions.Add(new SiteSessionDto(
                // Short and opaque: a prefix of the session's hash, enough to tell a day's sessions apart.
                key.Length > 12 ? key[..12] : key,
                first.At,
                last.At,
                (int)Math.Round((last.At - first.At).TotalSeconds),
                steps.Select(s => s.Source).FirstOrDefault(s => s != SiteAnalyticsRules.Direct) ?? SiteAnalyticsRules.Direct,
                steps.Select(s => s.ReferrerHost).FirstOrDefault(r => r != null),
                steps.Select(s => s.Campaign).FirstOrDefault(c => c != null),
                first.Device,
                first.Browser,
                steps.Select(s => s.Language).FirstOrDefault(l => l != null),
                steps.Any(s => s.SignedIn),
                steps.Count(s => s.Type == SiteEventTypes.PageView),
                steps.Take(SiteAnalyticsRules.MaxSteps).Select(s => new SiteStepDto(s.At, s.Type, s.Path, s.Name)).ToList(),
                steps.Any(s => s.Type == SiteEventTypes.Event && s.Name == SiteAnalyticsRules.SignupCompleted)));
        }
        return sessions;
    }
}
