using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The Reports page's figures, for a period, a scope, a pipeline and a currency.
/// </summary>
/// <remarks>
/// <para>
/// The page used to download every deal, lead and activity and work these out in the
/// browser, with its own money parser and its own idea of an active lead, so it
/// disagreed with the dashboard, and its cost grew with all-time history. Everything
/// here is grouped and counted by the database; only the handful of result rows come
/// back.
/// </para>
/// <para>
/// Money is never added across currencies: every money figure is a list, one row per
/// currency. Private deals are left out for whoever may not see them, by the same rule
/// as everywhere else (<see cref="DealVisibility"/>), and so are activities logged on
/// them.
/// </para>
/// </remarks>
public partial class ReportingService
{
    /// <summary>Long enough for any review; short enough that a mistyped year cannot ask for a century.</summary>
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(5 * 366);

    /// <summary>The columns of a deal the reports read, with its currency worked out once.</summary>
    private sealed class DealFacts
    {
        public Guid? AssigneeId { get; set; }
        public Guid? PipelineId { get; set; }
        public Guid? DealStageId { get; set; }
        public string? Stage { get; set; }
        public bool? IsWon { get; set; }
        public decimal? Amount { get; set; }
        public int? Probability { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public DateTime? ExpectedCloseDateUtc { get; set; }
        public string Currency { get; set; } = RegionalSettings.DefaultCurrency;
    }

    /// <summary>The scope after the team, if any, was worked out.</summary>
    private sealed record Scoping(ReportScope Scope, Guid? TeamId, string? TeamName);

    /// <summary>How a period is cut into a chart's buckets, in the viewer's time zone.</summary>
    private sealed record Window(DateTime FromUtc, DateTime ToUtc, TimeZoneInfo Zone, string Bucket)
    {
        public DateOnly FirstDay => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(FromUtc, Zone));
        public DateOnly LastDay => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ToUtc.AddTicks(-1), Zone));

        public DateOnly BucketOf(DateOnly day) => Bucket switch
        {
            "week" => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            "month" => new DateOnly(day.Year, day.Month, 1),
            _ => day,
        };

        public DateOnly Next(DateOnly bucket) => Bucket switch
        {
            "week" => bucket.AddDays(7),
            "month" => bucket.AddMonths(1),
            _ => bucket.AddDays(1),
        };
    }

    public async Task<Result<ReportSummaryDto>> GetSummaryAsync(Guid userId, Guid? organizationId, ReportFilter filter, CancellationToken ct = default)
    {
        var from = AsUtc(filter.FromUtc);
        var to = AsUtc(filter.ToUtc);
        if (from >= to || to - from > MaxPeriod) return DomainErrors.Reporting.InvalidPeriod;
        var currency = NormaliseFilterCurrency(filter.Currency, out var currencyValid);
        if (!currencyValid) return DomainErrors.Reporting.InvalidCurrency;

        var scoping = await ResolveScopeAsync(userId, organizationId, filter.Scope, filter.TeamId, ct);
        if (scoping.IsFailure) return scoping.Error;
        var pipeline = await CheckPipelineAsync(organizationId, filter.PipelineId, ct);
        if (pipeline.IsFailure) return pipeline.Error;

        // The days and weeks of the charts are the workspace's, in its time zone, unless
        // the caller names another (the weekly digest counts in its reader's). Without
        // one they were UTC's, so a call made at 00:30 in Zurich counted on the day before.
        var (fallback, workspaceZone) = await RegionOfAsync(organizationId, ct);
        var zone = FindZone(filter.TimeZone) ?? workspaceZone;
        var span = to - from;
        var window = new Window(from, to, zone, span <= TimeSpan.FromDays(62) ? "day" : span <= TimeSpan.FromDays(400) ? "week" : "month");

        // An expected close date is a day, stored as that day's UTC midnight, so it is
        // matched to the period's days on the calendar it is read in, not to its instants.
        // West of Greenwich a month starts hours after UTC midnight: a deal due on the
        // 1st fell outside New York's October and one due on 1 November fell inside.
        var closeFrom = window.FirstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var closeTo = window.LastDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Every currency the scope's deals are in, before the currency filter narrows
        // them, so the picker can offer each.
        var everyCurrency = FactsOf(DealsInScope(userId, organizationId, scoping.Value, filter.PipelineId), fallback);
        var currencyRows = await everyCurrency
            .GroupBy(r => r.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Count = g.Count(),
                OpenCount = g.Count(r => r.IsWon == null),
                OpenValue = g.Sum(r => r.IsWon == null ? r.Amount ?? 0m : 0m),
            })
            .ToListAsync(ct);
        var currencies = currencyRows
            .OrderByDescending(c => c.OpenValue).ThenByDescending(c => c.Count).ThenBy(c => c.Currency, StringComparer.Ordinal)
            .Select(c => c.Currency)
            .ToList();
        // The headline is picked from OPEN deals: picked from all of them, two years of
        // won EUR deals hid a live CHF pipeline behind "€0 open".
        var headline = currency
            ?? currencyRows.Where(c => c.OpenCount > 0)
                .OrderByDescending(c => c.OpenValue).ThenBy(c => c.Currency, StringComparer.Ordinal)
                .Select(c => c.Currency).FirstOrDefault()
            ?? currencies.FirstOrDefault();

        var facts = currency == null ? everyCurrency : everyCurrency.Where(r => r.Currency == currency);
        var open = facts.Where(r => r.IsWon == null);
        var won = facts.Where(r => r.IsWon == true && r.ClosedAtUtc >= from && r.ClosedAtUtc < to);
        var lost = facts.Where(r => r.IsWon == false && r.ClosedAtUtc >= from && r.ClosedAtUtc < to);

        var openFigures = await FiguresAsync(open, ct);
        var wonFigures = await FiguresAsync(won, ct);
        var lostFigures = await FiguresAsync(lost, ct);
        var closed = wonFigures.Count + lostFigures.Count;

        // Days from creation to the win. Only the deals won in the period, two dates
        // each; EF has no portable date difference for the in-memory test host.
        var cycles = await won.Select(r => new { r.CreatedAtUtc, r.ClosedAtUtc }).ToListAsync(ct);
        var cycleDays = cycles
            .Where(c => c.ClosedAtUtc >= c.CreatedAtUtc)
            .Select(c => (c.ClosedAtUtc!.Value - c.CreatedAtUtc).TotalDays)
            .ToList();

        var leads = await LeadFiguresAsync(userId, organizationId, scoping.Value, window, ct);
        var activities = await ActivityFiguresAsync(userId, organizationId, scoping.Value, window, ct);

        return new ReportSummaryDto(
            new ReportPeriodDto(from, to, ScopeName(scoping.Value.Scope), scoping.Value.TeamId, scoping.Value.TeamName,
                filter.PipelineId, currency, zone.Id, window.Bucket),
            currencies,
            headline,
            openFigures,
            await WeightedAsync(open, ct),
            await FiguresAsync(open.Where(r => r.ExpectedCloseDateUtc >= closeFrom && r.ExpectedCloseDateUtc < closeTo), ct),
            await FiguresAsync(facts.Where(r => r.CreatedAtUtc >= from && r.CreatedAtUtc < to), ct),
            wonFigures,
            lostFigures,
            closed > 0 ? (double)wonFigures.Count / closed : null,
            cycleDays.Count > 0 ? Math.Round(cycleDays.Average(), 1) : null,
            await StageRowsAsync(open, ct),
            await OwnerRowsAsync(open, won, lost, ct),
            leads,
            activities);
    }

    internal static string ScopeName(ReportScope scope) => scope switch
    {
        ReportScope.Me => "me",
        ReportScope.Team => "team",
        _ => "org",
    };

    /// <summary>"me", "team" or "org" (or "organization"), as the query string has it.</summary>
    public static bool TryParseScope(string? text, out ReportScope scope)
    {
        switch ((text ?? "org").Trim().ToLowerInvariant())
        {
            case "" or "org" or "organization" or "organisation":
                scope = ReportScope.Organization;
                return true;
            case "team":
                scope = ReportScope.Team;
                return true;
            case "me":
                scope = ReportScope.Me;
                return true;
            default:
                scope = ReportScope.Organization;
                return false;
        }
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>The filter's currency as deals are counted in (trimmed, upper case), or null for none.</summary>
    private static string? NormaliseFilterCurrency(string? raw, out bool valid)
    {
        valid = true;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var code = raw.Trim().ToUpperInvariant();
        valid = code.Length <= 8 && code.All(char.IsLetter);
        return valid ? code : null;
    }

    /// <summary>The zone with this IANA (or Windows) id, or null when there is none by that name.</summary>
    private static TimeZoneInfo? FindZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        return null;
    }

    /// <summary>
    /// The team a team report is about: the one asked for, which has to be this
    /// workspace's, or else the caller's own (the first by name, if they are in several).
    /// </summary>
    private async Task<Result<Scoping>> ResolveScopeAsync(Guid userId, Guid? organizationId, ReportScope scope, Guid? teamId, CancellationToken ct)
    {
        if (scope != ReportScope.Team) return new Scoping(scope, null, null);
        // A personal workspace has no teams.
        if (organizationId is not Guid org) return DomainErrors.Reporting.TeamNotFound;

        var teams = _db.SalesTeams.AsNoTracking().Where(t => t.OrganizationId == org);
        var team = teamId is Guid wanted
            ? await teams.Where(t => t.Id == wanted).Select(t => new { t.Id, t.Name }).FirstOrDefaultAsync(ct)
            : await teams.Where(t => _db.SalesTeamMembers.Any(m => m.TeamId == t.Id && m.UserId == userId))
                .OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).FirstOrDefaultAsync(ct);
        if (team == null) return teamId == null ? DomainErrors.Reporting.NoTeam : DomainErrors.Reporting.TeamNotFound;
        return new Scoping(scope, team.Id, team.Name);
    }

    private async Task<Result> CheckPipelineAsync(Guid? organizationId, Guid? pipelineId, CancellationToken ct)
    {
        if (pipelineId is not Guid id) return Result.Success();
        var exists = organizationId is Guid org && await _db.Pipelines.AsNoTracking().AnyAsync(p => p.Id == id && p.OrganizationId == org, ct);
        return exists ? Result.Success() : DomainErrors.Pipeline.NotFound;
    }

    /// <summary>
    /// The deals a report counts. A deal is someone's when it is assigned to them, or,
    /// with nobody assigned, when they created it; a team's when it is tagged with the
    /// team or is one of its members'.
    /// </summary>
    private IQueryable<Deal> DealsInScope(Guid userId, Guid? organizationId, Scoping scoping, Guid? pipelineId)
    {
        var deals = ScopedDeals(userId, organizationId);
        if (pipelineId is Guid pipeline) deals = deals.Where(d => d.PipelineId == pipeline);
        var teamId = scoping.TeamId;
        return scoping.Scope switch
        {
            ReportScope.Me => deals.Where(d => d.AssigneeId == userId || (d.AssigneeId == null && d.UserId == userId)),
            ReportScope.Team => deals.Where(d => d.TeamId == teamId
                || _db.SalesTeamMembers.Any(m => m.TeamId == teamId && m.UserId == (d.AssigneeId ?? d.UserId))),
            _ => deals,
        };
    }

    private static IQueryable<DealFacts> FactsOf(IQueryable<Deal> deals, string fallback) => deals.Select(d => new DealFacts
    {
        AssigneeId = d.AssigneeId,
        PipelineId = d.PipelineId,
        DealStageId = d.DealStageId,
        Stage = d.Stage,
        IsWon = d.IsWon,
        Amount = d.Amount,
        // The deal's own probability, or else its stage's default, as the board weighs it:
        // set on a stage, a probability weights every deal there that has none of its own.
        Probability = d.Probability ?? (d.DealStage != null ? d.DealStage.Probability : null),
        CreatedAtUtc = d.CreatedAtUtc,
        ClosedAtUtc = d.ClosedAtUtc,
        ExpectedCloseDateUtc = d.ExpectedCloseDateUtc,
        // The code, trimmed and upper-cased; none means the workspace's (see RegionOfAsync).
        Currency = d.Currency == null || d.Currency.Trim() == "" ? fallback : d.Currency.Trim().ToUpper(),
    });

    /// <summary>A set of deals counted, and its money totalled per currency, largest first.</summary>
    private static async Task<DealFiguresDto> FiguresAsync(IQueryable<DealFacts> rows, CancellationToken ct)
    {
        var groups = await rows
            .GroupBy(r => r.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Count = g.Count(),
                Value = g.Sum(r => r.Amount ?? 0m),
                Unreadable = g.Count(r => r.Amount == null),
            })
            .ToListAsync(ct);
        var byCurrency = groups
            .Select(g => new MoneyRowDto(g.Currency, g.Count, g.Value, g.Unreadable))
            .OrderByDescending(r => r.Value).ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();
        return new DealFiguresDto(byCurrency.Sum(r => r.Count), byCurrency);
    }

    /// <summary>
    /// Weighted value per currency, from the deals that have a probability, their own or
    /// their stage's. A deal with neither is counted as missing, never given an invented one.
    /// </summary>
    private static async Task<IReadOnlyList<WeightedRowDto>> WeightedAsync(IQueryable<DealFacts> open, CancellationToken ct)
    {
        var groups = await open
            .GroupBy(r => r.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Value = g.Sum(r => r.Amount != null && r.Probability != null ? r.Amount.Value * r.Probability.Value / 100m : 0m),
                Covered = g.Count(r => r.Amount != null && r.Probability != null),
                Missing = g.Count(r => r.Probability == null),
            })
            .ToListAsync(ct);
        return groups
            .Select(g => new WeightedRowDto(g.Currency, Math.Round(g.Value, 2), g.Covered, g.Missing))
            .OrderByDescending(r => r.Value).ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Open deals per stage and currency, in pipeline and stage order, so the rows read as a funnel.</summary>
    private async Task<IReadOnlyList<StageRowDto>> StageRowsAsync(IQueryable<DealFacts> open, CancellationToken ct)
    {
        var groups = await open
            .GroupBy(r => new { r.PipelineId, r.DealStageId, Legacy = r.DealStageId == null ? r.Stage : null, r.Currency })
            .Select(g => new
            {
                g.Key.PipelineId,
                g.Key.DealStageId,
                g.Key.Legacy,
                g.Key.Currency,
                Count = g.Count(),
                Value = g.Sum(r => r.Amount ?? 0m),
                Weighted = g.Sum(r => r.Amount != null && r.Probability != null ? r.Amount.Value * r.Probability.Value / 100m : 0m),
                Unreadable = g.Count(r => r.Amount == null),
                // The name the deal was saved with, for a stage since deleted.
                SavedName = g.Max(r => r.Stage),
            })
            .ToListAsync(ct);

        var stageIds = groups.Where(g => g.DealStageId != null).Select(g => g.DealStageId!.Value).Distinct().ToList();
        var stages = await _db.DealStages.AsNoTracking()
            .Where(s => stageIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.DisplayOrder, s.PipelineId })
            .ToDictionaryAsync(s => s.Id, ct);
        var pipelineIds = groups.Select(g => g.PipelineId)
            .Concat(stages.Values.Select(s => (Guid?)s.PipelineId))
            .OfType<Guid>().Distinct().ToList();
        var pipelines = await _db.Pipelines.AsNoTracking()
            .Where(p => pipelineIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.DisplayOrder })
            .ToDictionaryAsync(p => p.Id, ct);

        return groups
            .Select(g =>
            {
                var stage = g.DealStageId is Guid sid && stages.TryGetValue(sid, out var s) ? s : null;
                var pipelineId = stage?.PipelineId ?? g.PipelineId;
                var pipeline = pipelineId is Guid pid && pipelines.TryGetValue(pid, out var p) ? p : null;
                return new
                {
                    PipelineOrder = pipeline?.DisplayOrder ?? int.MaxValue,
                    Row = new StageRowDto(
                        pipeline?.Id,
                        pipeline?.Name ?? "No pipeline",
                        g.DealStageId?.ToString() ?? g.Legacy ?? "none",
                        stage?.Name ?? g.Legacy ?? g.SavedName ?? "No stage",
                        stage?.DisplayOrder ?? int.MaxValue,
                        g.Currency,
                        g.Count,
                        g.Value,
                        Math.Round(g.Weighted, 2),
                        g.Unreadable),
                };
            })
            .OrderBy(x => x.PipelineOrder)
            .ThenBy(x => x.Row.PipelineName, StringComparer.Ordinal)
            .ThenBy(x => x.Row.DisplayOrder)
            .ThenBy(x => x.Row.StageName, StringComparer.Ordinal)
            .ThenBy(x => x.Row.Currency, StringComparer.Ordinal)
            .Select(x => x.Row)
            .ToList();
    }

    /// <summary>Each owner's open pipeline, and what they won and lost in the period, per currency.</summary>
    private async Task<IReadOnlyList<OwnerRowDto>> OwnerRowsAsync(
        IQueryable<DealFacts> open, IQueryable<DealFacts> won, IQueryable<DealFacts> lost, CancellationToken ct)
    {
        var openRows = await open
            .GroupBy(r => new { r.AssigneeId, r.Currency })
            .Select(g => new
            {
                g.Key.AssigneeId,
                g.Key.Currency,
                Count = g.Count(),
                Value = g.Sum(r => r.Amount ?? 0m),
                Weighted = g.Sum(r => r.Amount != null && r.Probability != null ? r.Amount.Value * r.Probability.Value / 100m : 0m),
            })
            .ToListAsync(ct);
        var wonRows = await won
            .GroupBy(r => new { r.AssigneeId, r.Currency })
            .Select(g => new { g.Key.AssigneeId, g.Key.Currency, Count = g.Count(), Value = g.Sum(r => r.Amount ?? 0m) })
            .ToListAsync(ct);
        var lostRows = await lost
            .GroupBy(r => new { r.AssigneeId, r.Currency })
            .Select(g => new { g.Key.AssigneeId, g.Key.Currency, Count = g.Count() })
            .ToListAsync(ct);

        var keys = openRows.Select(r => (r.AssigneeId, r.Currency))
            .Concat(wonRows.Select(r => (r.AssigneeId, r.Currency)))
            .Concat(lostRows.Select(r => (r.AssigneeId, r.Currency)))
            .Distinct()
            .ToList();
        var names = await NamesAsync(keys.Select(k => k.AssigneeId), ct);

        return keys
            .Select(k =>
            {
                var o = openRows.FirstOrDefault(r => r.AssigneeId == k.AssigneeId && r.Currency == k.Currency);
                var w = wonRows.FirstOrDefault(r => r.AssigneeId == k.AssigneeId && r.Currency == k.Currency);
                var l = lostRows.FirstOrDefault(r => r.AssigneeId == k.AssigneeId && r.Currency == k.Currency);
                return new OwnerRowDto(
                    k.AssigneeId,
                    k.AssigneeId is Guid id ? names.GetValueOrDefault(id, "Unknown") : "Unassigned",
                    k.Currency,
                    o?.Count ?? 0,
                    o?.Value ?? 0m,
                    Math.Round(o?.Weighted ?? 0m, 2),
                    w?.Count ?? 0,
                    w?.Value ?? 0m,
                    l?.Count ?? 0);
            })
            .OrderByDescending(r => r.OpenValue + r.WonValue)
            .ThenBy(r => r.OwnerName, StringComparer.Ordinal)
            .ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Leads: the active count now, and the period's cohort (leads created in it) by
    /// where each is today. A lead is someone's when it is assigned to them, or with
    /// nobody assigned, when they created it.
    /// </summary>
    private async Task<LeadFiguresDto> LeadFiguresAsync(Guid userId, Guid? organizationId, Scoping scoping, Window window, CancellationToken ct)
    {
        var leads = ScopedLeads(userId, organizationId);
        var teamId = scoping.TeamId;
        leads = scoping.Scope switch
        {
            ReportScope.Me => leads.Where(l => (l.AssignedToUserId ?? l.UserId) == userId),
            ReportScope.Team => leads.Where(l => _db.SalesTeamMembers.Any(m => m.TeamId == teamId && m.UserId == (l.AssignedToUserId ?? l.UserId))),
            _ => leads,
        };
        var from = window.FromUtc;
        var to = window.ToUtc;
        var cohort = leads.Where(l => l.CreatedAtUtc >= from && l.CreatedAtUtc < to);
        var convertedInPeriod = leads.Where(l => l.IsConverted && l.ConvertedAtUtc >= from && l.ConvertedAtUtc < to);

        var scores = cohort.Where(l => l.LeadScore != null);
        var scored = await scores.CountAsync(ct);
        double? averageScore = scored > 0
            ? Math.Round(await scores.AverageAsync(l => (double)l.LeadScore!.Value, ct), 1)
            : null;

        var byStatus = await cohort
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Converted = g.Count(l => l.IsConverted) })
            .ToListAsync(ct);
        var bySource = await cohort
            .GroupBy(l => l.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new LeadFiguresDto(
            await leads.CountAsync(l => !l.IsConverted, ct),
            await cohort.CountAsync(ct),
            await cohort.CountAsync(l => l.IsConverted, ct),
            await convertedInPeriod.CountAsync(ct),
            await convertedInPeriod.CountAsync(l => l.ConvertedToDealId != null, ct),
            scored,
            averageScore,
            byStatus
                .Select(s => new LeadStatusRowDto(string.IsNullOrWhiteSpace(s.Status) ? "Unknown" : s.Status, s.Count, s.Converted))
                .OrderByDescending(s => s.Count).ThenBy(s => s.Status, StringComparer.Ordinal)
                .ToList(),
            bySource
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Source) ? "Unknown" : s.Source.Trim())
                .Select(g => new CountRowDto(g.Key, g.Sum(s => s.Count)))
                .OrderByDescending(s => s.Count).ThenBy(s => s.Key, StringComparer.Ordinal)
                .ToList(),
            await SeriesAsync(cohort.Select(l => l.CreatedAtUtc), window, ct));
    }

    /// <summary>
    /// The workspace's activities, less the app's own "system" lines and those on a
    /// private deal the caller cannot see (they carry the deal's name and what was said).
    /// </summary>
    private async Task<ActivityFiguresDto> ActivityFiguresAsync(Guid userId, Guid? organizationId, Scoping scoping, Window window, CancellationToken ct)
    {
        var visibleDeals = _db.Deals.VisibleTo(_db, userId, organizationId);
        var activities = organizationId == null
            ? _db.Activities.AsNoTracking().Where(a => a.UserId == userId && a.OrganizationId == null)
            : _db.Activities.AsNoTracking().Where(a => a.OrganizationId == organizationId);
        activities = activities.Where(a => a.DealId == null || visibleDeals.Any(d => d.Id == a.DealId));
        var teamId = scoping.TeamId;
        activities = scoping.Scope switch
        {
            ReportScope.Me => activities.Where(a => a.UserId == userId),
            ReportScope.Team => activities.Where(a => _db.SalesTeamMembers.Any(m => m.TeamId == teamId && m.UserId == a.UserId)),
            _ => activities,
        };
        var from = window.FromUtc;
        var to = window.ToUtc;
        var logged = activities.Where(a => a.Type.ToLower() != "system" && a.OccurredAtUtc >= from && a.OccurredAtUtc < to);

        var byType = await logged
            .GroupBy(a => a.Type.ToLower())
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new ActivityFiguresDto(
            byType.Sum(t => t.Count),
            byType.Select(t => new CountRowDto(t.Type, t.Count))
                .OrderByDescending(t => t.Count).ThenBy(t => t.Key, StringComparer.Ordinal)
                .ToList(),
            await SeriesAsync(logged.Select(a => a.OccurredAtUtc), window, ct));
    }

    /// <summary>
    /// How many instants fall in each bucket of the period, every bucket included.
    /// </summary>
    /// <remarks>
    /// The database counts per UTC hour (per UTC day for periods longer than a couple
    /// of months, when a few hours either side of midnight no longer show), and the
    /// counts are then moved onto the viewer's calendar here: the day an evening call
    /// was made is the viewer's day, not Greenwich's.
    /// </remarks>
    private static async Task<IReadOnlyList<SeriesPointDto>> SeriesAsync(IQueryable<DateTime> instants, Window window, CancellationToken ct)
    {
        var counts = new Dictionary<DateOnly, int>();
        void Add(DateOnly day, int count)
        {
            // Read by UTC day, something from the period's first or last hours can land
            // on the day before or after it, in a bucket the chart does not draw, and it
            // vanished from the chart while the total still counted it. It happened in
            // the period, so it goes on the period's first or last day.
            if (day < window.FirstDay) day = window.FirstDay;
            else if (day > window.LastDay) day = window.LastDay;
            var bucket = window.BucketOf(day);
            counts[bucket] = counts.GetValueOrDefault(bucket) + count;
        }

        if (window.Bucket == "day")
        {
            var hours = await instants
                .GroupBy(t => new { t.Year, t.Month, t.Day, t.Hour })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var h in hours)
            {
                var utc = new DateTime(h.Year, h.Month, h.Day, h.Hour, 0, 0, DateTimeKind.Utc);
                Add(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, window.Zone)), h.Count);
            }
        }
        else
        {
            var days = await instants
                .GroupBy(t => new { t.Year, t.Month, t.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var d in days) Add(new DateOnly(d.Year, d.Month, d.Day), d.Count);
        }

        var points = new List<SeriesPointDto>();
        for (var bucket = window.BucketOf(window.FirstDay); bucket <= window.LastDay; bucket = window.Next(bucket))
            points.Add(new SeriesPointDto(bucket.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), counts.GetValueOrDefault(bucket)));
        return points;
    }
}
