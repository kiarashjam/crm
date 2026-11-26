using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <summary>
/// The site analytics' aggregate queries, each one SQL statement over a range of Zurich days,
/// counting the events of one <see cref="SiteAudience"/>.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from <see cref="SiteAnalyticsService"/> so SiteAnalyticsTests can ask SQL Server's
/// provider to translate every one of them offline: the in-memory provider the HTTP tests run on
/// evaluates anything, so only that shows a query would run on the real database.
/// </para>
/// <para>
/// The rows are classes set by their properties, not positional records: EF can order and
/// take by a property it set, never by a constructor's argument.
/// </para>
/// </remarks>
public static class SiteAnalyticsQueries
{
    public sealed class DayRow
    {
        public DateOnly Day { get; init; }
        public int Visitors { get; init; }
        public int Sessions { get; init; }
        public int PageViews { get; init; }
        public int Signups { get; init; }
    }

    public sealed class SessionRow
    {
        public string Key { get; init; } = "";
        public DateTime Start { get; init; }
        public DateTime End { get; init; }
        public int PageViews { get; init; }

        /// <summary>Clicks and events: a session with one page view and none of these bounced.</summary>
        public int Interactions { get; init; }

        public string? Visitor { get; init; }

        /// <summary>The session's source other than "direct", if any of its events has one; null: direct.</summary>
        public string? Source { get; init; }

        public string? Referrer { get; init; }
        public string? Campaign { get; init; }
        public string? UtmSource { get; init; }
        public string? UtmMedium { get; init; }
        public string? Device { get; init; }
        public string? Browser { get; init; }
        public string? Os { get; init; }
        public string? Language { get; init; }
    }

    public sealed class PageRow
    {
        public string Path { get; init; } = "";
        public int Views { get; init; }
        public int Visitors { get; init; }
    }

    public sealed class EntryRow
    {
        public string Path { get; init; } = "";
        public int Sessions { get; init; }
        public int Visitors { get; init; }
    }

    public sealed class EventRow
    {
        public string Name { get; init; } = "";
        public int Count { get; init; }
    }

    /// <summary>
    /// The events of <paramref name="audience"/>: signed out, plus every sign-up event, for the
    /// website's visitors (a sign-up recorded just after signing in is still the website's);
    /// signed in (the sign-up events sent signed in), for the signed-in; all of them for everyone.
    /// </summary>
    private static IQueryable<SiteEvent> Of(this IQueryable<SiteEvent> events, SiteAudience audience) => audience switch
    {
        SiteAudience.Visitors => events.Where(e => !e.SignedIn || e.Type == SiteEventTypes.Event),
        SiteAudience.SignedIn => events.Where(e => e.SignedIn),
        SiteAudience.Everyone => events,
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Not an audience."),
    };

    private static IQueryable<SiteEvent> Range(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        db.SiteEvents.AsNoTracking().Where(e => e.Day >= from && e.Day <= to).Of(audience);

    private static IQueryable<SiteEvent> PageViews(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        Range(db, from, to, audience).Where(e => e.Type == SiteEventTypes.PageView);

    /// <summary>Per day with events: distinct visitors, distinct sessions, page views, sign-ups.</summary>
    public static IQueryable<DayRow> Daily(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        Range(db, from, to, audience)
            .GroupBy(e => e.Day)
            .Select(g => new DayRow
            {
                Day = g.Key,
                Visitors = g.Select(e => e.VisitorHash).Distinct().Count(),
                Sessions = g.Select(e => e.SessionKey).Distinct().Count(),
                PageViews = g.Count(e => e.Type == SiteEventTypes.PageView),
                Signups = g.Count(e => e.Type == SiteEventTypes.Event && e.Name == SiteAnalyticsRules.SignupCompleted),
            });

    /// <summary>
    /// One row per session, newest first. Every event of a session carries the visit's landing
    /// attribution (the tracker sends it with each), so the largest non-direct source, referrer
    /// and campaign are the session's.
    /// </summary>
    public static IQueryable<SessionRow> Sessions(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        Range(db, from, to, audience)
            .GroupBy(e => e.SessionKey)
            .Select(g => new SessionRow
            {
                Key = g.Key,
                Start = g.Min(e => e.OccurredAtUtc),
                End = g.Max(e => e.OccurredAtUtc),
                PageViews = g.Count(e => e.Type == SiteEventTypes.PageView),
                Interactions = g.Count(e => e.Type != SiteEventTypes.PageView),
                Visitor = g.Max(e => e.VisitorHash),
                Source = g.Max(e => e.Source == SiteAnalyticsRules.Direct ? null : e.Source),
                Referrer = g.Max(e => e.ReferrerHost),
                Campaign = g.Max(e => e.UtmCampaign),
                UtmSource = g.Max(e => e.UtmSource),
                UtmMedium = g.Max(e => e.UtmMedium),
                Device = g.Max(e => e.Device),
                Browser = g.Max(e => e.Browser),
                Os = g.Max(e => e.Os),
                Language = g.Max(e => e.Language),
            })
            .OrderByDescending(s => s.Start).ThenBy(s => s.Key);

    /// <summary>The most viewed pages: views and distinct visitors.</summary>
    public static IQueryable<PageRow> TopPages(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        PageViews(db, from, to, audience)
            .GroupBy(e => e.Path)
            .Select(g => new PageRow { Path = g.Key, Views = g.Count(), Visitors = g.Select(e => e.VisitorHash).Distinct().Count() })
            .OrderByDescending(p => p.Views).ThenBy(p => p.Path)
            .Take(SiteAnalyticsRules.TopListSize);

    /// <summary>The pages sessions began on: each session's first page view.</summary>
    public static IQueryable<EntryRow> EntryPages(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience)
    {
        var views = PageViews(db, from, to, audience);
        var firsts = views.GroupBy(e => e.SessionKey).Select(g => new { Key = g.Key, At = g.Min(e => e.OccurredAtUtc) });
        return ByPage(
            from e in views
            join f in firsts on new { Key = e.SessionKey, At = e.OccurredAtUtc } equals new { f.Key, f.At }
            select e);
    }

    /// <summary>The pages sessions ended on: each session's last page view.</summary>
    public static IQueryable<EntryRow> ExitPages(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience)
    {
        var views = PageViews(db, from, to, audience);
        var lasts = views.GroupBy(e => e.SessionKey).Select(g => new { Key = g.Key, At = g.Max(e => e.OccurredAtUtc) });
        return ByPage(
            from e in views
            join l in lasts on new { Key = e.SessionKey, At = e.OccurredAtUtc } equals new { l.Key, l.At }
            select e);
    }

    /// <summary>
    /// Sessions and visitors per page of the page views given: each session's first (or last).
    /// Two at the same instant would put the session under both pages; counting distinct
    /// sessions keeps it from counting twice on one.
    /// </summary>
    private static IQueryable<EntryRow> ByPage(IQueryable<SiteEvent> edges) =>
        edges
            .GroupBy(e => e.Path)
            .Select(g => new EntryRow
            {
                Path = g.Key,
                Sessions = g.Select(e => e.SessionKey).Distinct().Count(),
                Visitors = g.Select(e => e.VisitorHash).Distinct().Count(),
            })
            .OrderByDescending(p => p.Sessions).ThenBy(p => p.Path)
            .Take(SiteAnalyticsRules.TopListSize);

    public sealed class LiveRow
    {
        public string Type { get; init; } = "";
        public string? Name { get; init; }
        public string Path { get; init; } = "";
        public string SessionKey { get; init; } = "";
        public string VisitorHash { get; init; } = "";
    }

    /// <summary>The events of <paramref name="audience"/> since <paramref name="sinceUtc"/>, newest first, at most <paramref name="take"/>.</summary>
    public static IQueryable<LiveRow> Since(AppDbContext db, DateTime sinceUtc, int take, SiteAudience audience) =>
        db.SiteEvents.AsNoTracking()
            .Where(e => e.OccurredAtUtc >= sinceUtc)
            .Of(audience)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new LiveRow { Type = e.Type, Name = e.Name, Path = e.Path, SessionKey = e.SessionKey, VisitorHash = e.VisitorHash });

    public sealed class RecentRow
    {
        public DateTime At { get; init; }
        public string Type { get; init; } = "";
        public string Path { get; init; } = "";
        public string? Name { get; init; }
        public string Source { get; init; } = "";
        public string Device { get; init; } = "";
    }

    /// <summary>The latest <paramref name="take"/> events of <paramref name="audience"/>, newest first.</summary>
    public static IQueryable<RecentRow> Latest(AppDbContext db, int take, SiteAudience audience) =>
        db.SiteEvents.AsNoTracking()
            .Of(audience)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Take(take)
            .Select(e => new RecentRow { At = e.OccurredAtUtc, Type = e.Type, Path = e.Path, Name = e.Name, Source = e.Source, Device = e.Device });

    public sealed class SessionStart
    {
        public string Key { get; init; } = "";
        public DateTime Start { get; init; }
    }

    /// <summary>
    /// The day's sessions with events of <paramref name="audience"/> that started last (by
    /// their first such event), newest first.
    /// </summary>
    public static IQueryable<SessionStart> LatestSessions(AppDbContext db, DateOnly day, int take, SiteAudience audience) =>
        db.SiteEvents.AsNoTracking()
            .Where(e => e.Day == day)
            .Of(audience)
            .GroupBy(e => e.SessionKey)
            .Select(g => new SessionStart { Key = g.Key, Start = g.Min(e => e.OccurredAtUtc) })
            .OrderByDescending(s => s.Start).ThenBy(s => s.Key)
            .Take(take);

    public sealed class StepRow
    {
        public string SessionKey { get; init; } = "";
        public DateTime At { get; init; }
        public string Type { get; init; } = "";
        public string? Name { get; init; }
        public string Path { get; init; } = "";
        public string Source { get; init; } = "";
        public string? ReferrerHost { get; init; }
        public string? Campaign { get; init; }
        public string Device { get; init; } = "";
        public string Browser { get; init; } = "";
        public string? Language { get; init; }
        public bool SignedIn { get; init; }
    }

    /// <summary>
    /// The events of <paramref name="audience"/> of these sessions on the day, in the order they
    /// happened: the same events as chose the sessions (<see cref="LatestSessions"/>).
    /// </summary>
    public static IQueryable<StepRow> Steps(AppDbContext db, DateOnly day, IReadOnlyCollection<string> keys, SiteAudience audience) =>
        db.SiteEvents.AsNoTracking()
            .Where(e => e.Day == day && keys.Contains(e.SessionKey))
            .Of(audience)
            .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id)
            .Select(e => new StepRow
            {
                SessionKey = e.SessionKey,
                At = e.OccurredAtUtc,
                Type = e.Type,
                Name = e.Name,
                Path = e.Path,
                Source = e.Source,
                ReferrerHost = e.ReferrerHost,
                Campaign = e.UtmCampaign,
                Device = e.Device,
                Browser = e.Browser,
                Language = e.Language,
                SignedIn = e.SignedIn,
            });

    /// <summary>Clicks and events by name, most frequent first.</summary>
    public static IQueryable<EventRow> Events(AppDbContext db, DateOnly from, DateOnly to, SiteAudience audience) =>
        Range(db, from, to, audience)
            .Where(e => e.Type != SiteEventTypes.PageView && e.Name != null)
            .GroupBy(e => e.Name!)
            .Select(g => new EventRow { Name = g.Key, Count = g.Count() })
            .OrderByDescending(e => e.Count).ThenBy(e => e.Name)
            .Take(SiteAnalyticsRules.TopListSize);
}
