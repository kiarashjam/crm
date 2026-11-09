using System.Linq.Expressions;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The deals board, worked out by the database: each stage's cards a page at a time,
/// and its counts and money, so the board no longer downloads every deal to add them up.
/// </summary>
/// <remarks>
/// <para>
/// A board is one pipeline. A deal is on it when its stage is one of the pipeline's, or,
/// for a deal known only by a stage's name (from before deals carried stage ids), when it
/// is in the pipeline, or in none and this is the workspace's first pipeline, which is
/// where the board has always shown such deals. Those deals go to the stage of their name
/// and the rest of them to a last "No stage" column. A workspace without pipelines has
/// the default stages, by name, as the board has always drawn them.
/// </para>
/// <para>
/// Money is totalled per currency and never added across currencies. The weighted value
/// takes each open deal's own probability, or else its stage's default; a deal with
/// neither is counted as missing, not given one. Whether a deal is won, lost or open is
/// its own outcome when it has one, else its stage's: a deal in a won stage is won.
/// </para>
/// <para>
/// The filters narrow the columns. The summary is the whole pipeline whatever the
/// filters, as the figures above the board have always been.
/// </para>
/// </remarks>
public sealed class DealBoardService : IDealBoardService
{
    public const string UnsetKey = "unset";

    /// <summary>
    /// The stages of a workspace without pipelines, as the board has always drawn them
    /// (FALLBACK_STAGES in src/app/pages/pipeline/config.ts).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultStages =
        ["Qualification", "Proposal", "Negotiation", "Contract Sent", "Contract Signed", "Closed Won", "Closed Lost"];

    private static readonly string[] Sorts = ["close", "value", "created", "name"];

    private readonly AppDbContext _db;

    public DealBoardService(AppDbContext db) => _db = db;

    /// <param name="Names">The stage names, upper case, whose name-only deals this column takes.</param>
    private sealed record Column(string Key, Guid? StageId, string Name, int DisplayOrder, bool IsWon, bool IsLost, int? Probability, IReadOnlyList<string> Names)
    {
        public bool Closes => IsWon || IsLost;
    }

    private static readonly Column Unset = new(UnsetKey, null, "No stage", int.MaxValue, false, false, null, Array.Empty<string>());

    private sealed record Layout(
        Guid? PipelineId, string? PipelineName, int StaleAfterDays, IReadOnlyList<Column> Columns,
        Expression<Func<Deal, bool>> InBoard, string Currency, TimeZoneInfo Zone);

    /// <summary>Counts and money of the deals of one stage (or stage name) in one currency, by their own outcome.</summary>
    private sealed class GroupRow
    {
        public Guid? StageId { get; set; }
        public string? Legacy { get; set; }
        public string Currency { get; set; } = RegionalSettings.DefaultCurrency;
        public int Count { get; set; }
        public decimal Value { get; set; }
        public int Unreadable { get; set; }
        public int Open { get; set; }
        public decimal OpenValue { get; set; }
        public int OpenUnreadable { get; set; }
        public int Won { get; set; }
        public decimal WonValue { get; set; }
        public int WonUnreadable { get; set; }
        public int Lost { get; set; }
        public decimal OwnWeighted { get; set; }
        public int OwnCovered { get; set; }
        public decimal NoOwnValue { get; set; }
        public int NoOwnWithAmount { get; set; }
        public int NoOwn { get; set; }
        public int Overdue { get; set; }
        public int ClosingThisWeek { get; set; }
    }

    /// <summary>A card's columns, read in one query with the names it shows.</summary>
    private sealed class CardRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? Stage { get; set; }
        public Guid? PipelineId { get; set; }
        public Guid? DealStageId { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? ContactId { get; set; }
        public Guid? AssigneeId { get; set; }
        public DateTime? ExpectedCloseDateUtc { get; set; }
        public bool? IsWon { get; set; }
        public string? Description { get; set; }
        public int? Probability { get; set; }
        public string? AssigneeName { get; set; }
        public string? CompanyName { get; set; }
        public string? ContactName { get; set; }
        public string? PipelineName { get; set; }
        public string? DealStageName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string? ClosedReason { get; set; }
        public string? ClosedReasonCategory { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public bool IsPrivate { get; set; }
        public Guid? TeamId { get; set; }
    }

    public async Task<Result<DealBoardDto>> GetBoardAsync(Guid userId, Guid? organizationId, DealBoardQuery query, CancellationToken ct = default)
    {
        if (!SortIsKnown(query.Sort)) return DomainErrors.DealBoard.InvalidSort;
        var layoutResult = await LayoutAsync(organizationId, query.PipelineId, ct);
        if (layoutResult.IsFailure) return layoutResult.Error;
        var layout = layoutResult.Value;
        var now = DateTime.UtcNow;
        var today = RegionalSettings.TodayIn(layout.Zone, now).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var take = Math.Clamp(query.Take, 1, DealBoardQuery.MaxTake);

        var onBoard = _db.Deals.AsNoTracking().VisibleTo(_db, userId, organizationId).Where(layout.InBoard);
        var shown = Narrowed(onBoard, query, userId, organizationId, layout, now);

        var groups = await GroupsAsync(shown, layout.Currency, today, ct);
        var stale = await StaleAsync(shown, layout, userId, organizationId, now, ct);
        // The figures above the board are the whole pipeline's: without filters, the same sums.
        var allGroups = query.Narrows ? await GroupsAsync(onBoard, layout.Currency, today, ct) : groups;
        var allStale = query.Narrows ? await StaleAsync(onBoard, layout, userId, organizationId, now, ct) : stale;

        var columns = new List<DealBoardColumnDto>();
        foreach (var column in layout.Columns.Append(Unset))
        {
            var mine = groups.Where(g => ColumnOf(layout, g.StageId, g.Legacy) == column && !(query.StaleOnly && column.Closes)).ToList();
            var count = mine.Sum(g => g.Count);
            // The last column is only there when some deal is in none of the stages.
            if (column == Unset && count == 0) continue;
            var (cards, hasMore) = count == 0
                ? (new List<DealDto>(), false)
                : await PageAsync(shown, layout, column, query.Sort, 0, take, userId, organizationId, now, ct);
            columns.Add(new DealBoardColumnDto(
                column.Key, column.StageId, column.Name, column.DisplayOrder, column.IsWon, column.IsLost, column.Probability,
                count,
                MoneyRows(mine.Select(g => (g.Currency, g.Count, g.Value, g.Unreadable))),
                column.Closes ? Array.Empty<WeightedRowDto>() : WeightedRows(mine, column.Probability),
                column.Closes ? 0 : stale.Where(s => s.Column == column).Sum(s => s.Count),
                cards,
                hasMore));
        }

        return new DealBoardDto(
            layout.PipelineId, layout.PipelineName, layout.StaleAfterDays, columns,
            Summarise(layout, allGroups, allStale.Where(s => !s.Column.Closes).Sum(s => s.Count)),
            columns.Sum(c => c.Count));
    }

    public async Task<Result<DealBoardColumnPageDto>> GetColumnAsync(Guid userId, Guid? organizationId, DealBoardQuery query, string column, int skip, CancellationToken ct = default)
    {
        if (!SortIsKnown(query.Sort)) return DomainErrors.DealBoard.InvalidSort;
        var layoutResult = await LayoutAsync(organizationId, query.PipelineId, ct);
        if (layoutResult.IsFailure) return layoutResult.Error;
        var layout = layoutResult.Value;
        var key = column?.Trim() ?? string.Empty;
        var wanted = string.Equals(key, UnsetKey, StringComparison.OrdinalIgnoreCase)
            ? Unset
            : layout.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        if (wanted == null) return DomainErrors.DealBoard.ColumnNotFound;

        var now = DateTime.UtcNow;
        var take = Math.Clamp(query.Take, 1, DealBoardQuery.MaxTake);
        if (query.StaleOnly && wanted.Closes) return new DealBoardColumnPageDto(wanted.Key, Array.Empty<DealDto>(), false);
        var onBoard = _db.Deals.AsNoTracking().VisibleTo(_db, userId, organizationId).Where(layout.InBoard);
        var shown = Narrowed(onBoard, query, userId, organizationId, layout, now);
        var (cards, hasMore) = await PageAsync(shown, layout, wanted, query.Sort, Math.Max(0, skip), take, userId, organizationId, now, ct);
        return new DealBoardColumnPageDto(wanted.Key, cards, hasMore);
    }

    private static bool SortIsKnown(string? sort) =>
        string.IsNullOrWhiteSpace(sort) || Sorts.Contains(sort.Trim().ToLowerInvariant());

    private static string NameKey(string name) => name.Trim().ToUpperInvariant();

    /// <summary>The pipeline shown, its columns, and which deals are on it.</summary>
    private async Task<Result<Layout>> LayoutAsync(Guid? organizationId, Guid? pipelineId, CancellationToken ct)
    {
        var org = organizationId is Guid id
            ? await _db.Organizations.AsNoTracking().Where(o => o.Id == id)
                .Select(o => new { o.Currency, o.TimeZone, o.StaleDealDays }).FirstOrDefaultAsync(ct)
            : null;
        var currency = RegionalSettings.TryCurrency(org?.Currency, out var code) ? code : RegionalSettings.DefaultCurrency;
        var zone = RegionalSettings.ZoneOf(org?.TimeZone ?? RegionalSettings.DefaultTimeZone);
        var workspaceDays = org?.StaleDealDays ?? StaleDeals.DefaultDays;

        var pipelines = organizationId is Guid orgId
            ? await _db.Pipelines.AsNoTracking()
                .Where(p => p.OrganizationId == orgId)
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, p.StaleDealDays })
                .ToListAsync(ct)
            : [];
        if (pipelineId is Guid wanted && pipelines.All(p => p.Id != wanted)) return DomainErrors.Pipeline.NotFound;

        if (pipelines.Count == 0)
        {
            var defaults = DefaultStages
                .Select((name, i) => new Column(name, null, name, i, name == "Closed Won", name == "Closed Lost", null, new[] { NameKey(name) }))
                .ToList();
            return new Layout(null, null, workspaceDays, defaults, d => true, currency, zone);
        }

        var chosen = pipelineId is Guid w ? pipelines.First(p => p.Id == w) : pipelines[0];
        var stages = await _db.DealStages.AsNoTracking()
            .Where(s => s.PipelineId == chosen.Id)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.DisplayOrder, s.IsWon, s.IsLost, s.Probability })
            .ToListAsync(ct);
        // A deal known only by a stage's name goes to the first stage of that name.
        var owners = new Dictionary<string, Guid>();
        foreach (var s in stages) owners.TryAdd(NameKey(s.Name), s.Id);
        var columns = stages
            .Select(s => new Column(s.Id.ToString(), s.Id, s.Name, s.DisplayOrder, s.IsWon, s.IsLost, s.Probability,
                owners.Where(o => o.Value == s.Id).Select(o => o.Key).ToList()))
            .ToList();

        var pid = chosen.Id;
        Expression<Func<Deal, bool>> inBoard = pid == pipelines[0].Id
            ? d => (d.DealStageId != null && d.DealStage!.PipelineId == pid)
                || (d.DealStageId == null && (d.PipelineId == pid || d.PipelineId == null))
            : d => (d.DealStageId != null && d.DealStage!.PipelineId == pid)
                || (d.DealStageId == null && d.PipelineId == pid);
        return new Layout(pid, chosen.Name, StaleDeals.DaysFor(chosen.StaleDealDays, workspaceDays), columns, inBoard, currency, zone);
    }

    /// <summary>The column a group of deals is in: by stage id, else by stage name, else "No stage".</summary>
    private static Column ColumnOf(Layout layout, Guid? stageId, string? legacy)
    {
        if (stageId is Guid id) return layout.Columns.FirstOrDefault(c => c.StageId == id) ?? Unset;
        if (legacy != null)
            return layout.Columns.FirstOrDefault(c => c.Names.Contains(legacy, StringComparer.OrdinalIgnoreCase)) ?? Unset;
        return Unset;
    }

    /// <summary>The deals of one column, as SQL can tell them.</summary>
    private static Expression<Func<Deal, bool>> Holds(Layout layout, Column column)
    {
        if (column == Unset)
        {
            var named = layout.Columns.SelectMany(c => c.Names).ToList();
            return layout.PipelineId == null
                ? d => d.Stage == null || !named.Contains(d.Stage.Trim().ToUpper())
                : d => d.DealStageId == null && (d.Stage == null || !named.Contains(d.Stage.Trim().ToUpper()));
        }
        var names = column.Names.ToList();
        if (column.StageId is Guid id)
            return d => d.DealStageId == id || (d.DealStageId == null && d.Stage != null && names.Contains(d.Stage.Trim().ToUpper()));
        return d => d.Stage != null && names.Contains(d.Stage.Trim().ToUpper());
    }

    private IQueryable<Deal> Narrowed(IQueryable<Deal> deals, DealBoardQuery query, Guid userId, Guid? organizationId, Layout layout, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var words = query.Search.Trim().ToLower();
            deals = deals.Where(d => d.Name.ToLower().Contains(words) || d.Value.ToLower().Contains(words)
                || (d.Company != null && d.Company.Name.ToLower().Contains(words))
                || (d.Contact != null && d.Contact.Name.ToLower().Contains(words))
                || (d.Assignee != null && d.Assignee.Name.ToLower().Contains(words)));
        }
        if (query.Mine) deals = deals.Where(d => d.AssigneeId == userId || (d.AssigneeId == null && d.UserId == userId));
        if (query.Unassigned) deals = deals.Where(d => d.AssigneeId == null);
        if (query.AssigneeId is Guid assignee) deals = deals.Where(d => d.AssigneeId == assignee);
        if (query.MinAmount is decimal min) deals = deals.Where(d => d.Amount != null && d.Amount >= min);
        if (query.MaxAmount is decimal max) deals = deals.Where(d => d.Amount != null && d.Amount <= max);
        // A close date is the day picked, stored as that day's UTC midnight.
        if (query.CloseFrom is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            deals = deals.Where(d => d.ExpectedCloseDateUtc >= start);
        }
        if (query.CloseTo is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            deals = deals.Where(d => d.ExpectedCloseDateUtc < end);
        }
        if (query.StaleOnly)
        {
            deals = layout.StaleAfterDays > 0
                ? deals.Where(d => d.IsWon == null).IdleSince(_db, now.AddDays(-layout.StaleAfterDays), userId, organizationId)
                : deals.Where(d => false);
        }
        return deals;
    }

    private static async Task<List<GroupRow>> GroupsAsync(IQueryable<Deal> deals, string fallback, DateTime today, CancellationToken ct)
    {
        var weekEnd = today.AddDays(8);
        return await deals
            .GroupBy(d => new
            {
                d.DealStageId,
                Legacy = d.DealStageId == null ? (d.Stage == null ? null : d.Stage.Trim().ToUpper()) : null,
                // The code, trimmed and upper-cased; none means the workspace's, as in the reports.
                Currency = d.Currency == null || d.Currency.Trim() == "" ? fallback : d.Currency.Trim().ToUpper(),
            })
            .Select(g => new GroupRow
            {
                StageId = g.Key.DealStageId,
                Legacy = g.Key.Legacy,
                Currency = g.Key.Currency,
                Count = g.Count(),
                Value = g.Sum(d => d.Amount ?? 0m),
                Unreadable = g.Count(d => d.Amount == null),
                Open = g.Count(d => d.IsWon == null),
                OpenValue = g.Sum(d => d.IsWon == null ? d.Amount ?? 0m : 0m),
                OpenUnreadable = g.Count(d => d.IsWon == null && d.Amount == null),
                Won = g.Count(d => d.IsWon == true),
                WonValue = g.Sum(d => d.IsWon == true ? d.Amount ?? 0m : 0m),
                WonUnreadable = g.Count(d => d.IsWon == true && d.Amount == null),
                Lost = g.Count(d => d.IsWon == false),
                OwnWeighted = g.Sum(d => d.IsWon == null && d.Amount != null && d.Probability != null ? d.Amount.Value * d.Probability.Value / 100m : 0m),
                OwnCovered = g.Count(d => d.IsWon == null && d.Amount != null && d.Probability != null),
                NoOwnValue = g.Sum(d => d.IsWon == null && d.Amount != null && d.Probability == null ? d.Amount.Value : 0m),
                NoOwnWithAmount = g.Count(d => d.IsWon == null && d.Amount != null && d.Probability == null),
                NoOwn = g.Count(d => d.IsWon == null && d.Probability == null),
                Overdue = g.Count(d => d.IsWon == null && d.ExpectedCloseDateUtc < today),
                ClosingThisWeek = g.Count(d => d.IsWon == null && d.ExpectedCloseDateUtc >= today && d.ExpectedCloseDateUtc < weekEnd),
            })
            .ToListAsync(ct);
    }

    /// <summary>How many open deals in each column have gone stale.</summary>
    private async Task<List<(Column Column, int Count)>> StaleAsync(IQueryable<Deal> deals, Layout layout, Guid userId, Guid? organizationId, DateTime now, CancellationToken ct)
    {
        if (layout.StaleAfterDays <= 0) return [];
        var rows = await deals
            .Where(d => d.IsWon == null)
            .IdleSince(_db, now.AddDays(-layout.StaleAfterDays), userId, organizationId)
            .GroupBy(d => new
            {
                d.DealStageId,
                Legacy = d.DealStageId == null ? (d.Stage == null ? null : d.Stage.Trim().ToUpper()) : null,
            })
            .Select(g => new { g.Key.DealStageId, g.Key.Legacy, Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => (ColumnOf(layout, r.DealStageId, r.Legacy), r.Count)).ToList();
    }

    private static IQueryable<Deal> Ordered(IQueryable<Deal> deals, string? sort) => (sort ?? "close").Trim().ToLowerInvariant() switch
    {
        "value" => deals.OrderBy(d => d.Amount == null).ThenByDescending(d => d.Amount).ThenBy(d => d.Name).ThenBy(d => d.Id),
        "created" => deals.OrderByDescending(d => d.CreatedAtUtc).ThenBy(d => d.Id),
        "name" => deals.OrderBy(d => d.Name).ThenBy(d => d.Id),
        // Soonest expected close first, deals without a date after them.
        _ => deals.OrderBy(d => d.ExpectedCloseDateUtc == null).ThenBy(d => d.ExpectedCloseDateUtc).ThenBy(d => d.Name).ThenBy(d => d.Id),
    };

    /// <summary>A page of one column's cards, with how long each has been in its stage and whether it has gone stale.</summary>
    private async Task<(List<DealDto> Cards, bool HasMore)> PageAsync(
        IQueryable<Deal> deals, Layout layout, Column column, string? sort, int skip, int take,
        Guid userId, Guid? organizationId, DateTime now, CancellationToken ct)
    {
        var rows = await Ordered(deals.Where(Holds(layout, column)), sort)
            .Skip(skip)
            .Take(take + 1)
            .Select(d => new CardRow
            {
                Id = d.Id,
                Name = d.Name,
                Value = d.Value,
                Amount = d.Amount,
                Currency = d.Currency,
                Stage = d.Stage,
                PipelineId = d.PipelineId,
                DealStageId = d.DealStageId,
                CompanyId = d.CompanyId,
                ContactId = d.ContactId,
                AssigneeId = d.AssigneeId,
                ExpectedCloseDateUtc = d.ExpectedCloseDateUtc,
                IsWon = d.IsWon,
                Description = d.Description,
                Probability = d.Probability,
                AssigneeName = d.Assignee != null ? d.Assignee.Name : null,
                CompanyName = d.Company != null ? d.Company.Name : null,
                ContactName = d.Contact != null ? d.Contact.Name : null,
                PipelineName = d.Pipeline != null ? d.Pipeline.Name : null,
                DealStageName = d.DealStage != null ? d.DealStage.Name : null,
                CreatedAtUtc = d.CreatedAtUtc,
                UpdatedAtUtc = d.UpdatedAtUtc,
                ClosedReason = d.ClosedReason,
                ClosedReasonCategory = d.ClosedReasonCategory,
                ClosedAtUtc = d.ClosedAtUtc,
                IsPrivate = d.IsPrivate,
                TeamId = d.TeamId,
            })
            .ToListAsync(ct);
        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(take);
        if (rows.Count == 0) return (new List<DealDto>(), false);

        var ids = rows.Select(r => r.Id).ToList();
        var pace = await _db.Deals.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .PaceOf(_db, userId, organizationId)
            .ToDictionaryAsync(p => p.DealId, ct);

        var cards = rows.Select(r =>
        {
            var p = pace.GetValueOrDefault(r.Id);
            var idleSince = p?.IdleSinceUtc ?? r.CreatedAtUtc;
            var open = r.IsWon == null && !column.Closes;
            return new DealDto(
                r.Id, r.Name, r.Value, r.Currency, r.Stage, r.PipelineId, r.DealStageId, r.CompanyId, r.ContactId, r.AssigneeId,
                r.ExpectedCloseDateUtc, r.IsWon, p?.LastActivityAtUtc, r.Description, r.Probability,
                r.AssigneeName, r.CompanyName, r.ContactName, r.PipelineName, r.DealStageName,
                r.CreatedAtUtc, r.UpdatedAtUtc, r.ClosedReason, r.ClosedReasonCategory, r.ClosedAtUtc, r.IsPrivate, r.TeamId,
                r.Amount,
                p?.StageEnteredAtUtc,
                idleSince,
                StaleDeals.IsStale(open, idleSince, layout.StaleAfterDays, now),
                layout.StaleAfterDays);
        }).ToList();
        return (cards, hasMore);
    }

    private static DealBoardSummaryDto Summarise(Layout layout, List<GroupRow> groups, int stale)
    {
        int open = 0, won = 0, lost = 0, overdue = 0, closingThisWeek = 0;
        var openRows = new List<(string, int, decimal, int)>();
        var wonRows = new List<(string, int, decimal, int)>();
        var weighted = new List<(GroupRow Group, int? Probability)>();
        foreach (var g in groups)
        {
            var column = ColumnOf(layout, g.StageId, g.Legacy);
            // A deal's own outcome first; with none, its stage's.
            won += g.Won;
            wonRows.Add((g.Currency, g.Won, g.WonValue, g.WonUnreadable));
            if (column.IsWon)
            {
                won += g.Open;
                lost += g.Lost;
                wonRows.Add((g.Currency, g.Open, g.OpenValue, g.OpenUnreadable));
            }
            else if (column.IsLost)
            {
                lost += g.Open + g.Lost;
            }
            else
            {
                open += g.Open;
                lost += g.Lost;
                openRows.Add((g.Currency, g.Open, g.OpenValue, g.OpenUnreadable));
                weighted.Add((g, column.Probability));
                overdue += g.Overdue;
                closingThisWeek += g.ClosingThisWeek;
            }
        }
        var closed = won + lost;
        return new DealBoardSummaryDto(
            groups.Sum(g => g.Count), open, won, lost,
            MoneyRows(openRows), MoneyRows(wonRows),
            WeightedRows(weighted),
            closed > 0 ? (double)won / closed : null,
            overdue, closingThisWeek, stale);
    }

    /// <summary>Counts and money per currency, largest first, ties by code so the order is stable.</summary>
    private static IReadOnlyList<MoneyRowDto> MoneyRows(IEnumerable<(string Currency, int Count, decimal Value, int Unreadable)> rows) =>
        rows.Where(r => r.Count > 0)
            .GroupBy(r => r.Currency)
            .Select(g => new MoneyRowDto(g.Key, g.Sum(r => r.Count), g.Sum(r => r.Value), g.Sum(r => r.Unreadable)))
            .OrderByDescending(r => r.Value).ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<WeightedRowDto> WeightedRows(IEnumerable<GroupRow> groups, int? probability) =>
        WeightedRows(groups.Select(g => (g, probability)));

    /// <summary>
    /// Open deals weighted per currency: by their own probability, or else their stage's.
    /// A deal with neither is counted as missing.
    /// </summary>
    private static IReadOnlyList<WeightedRowDto> WeightedRows(IEnumerable<(GroupRow Group, int? Probability)> groups) =>
        groups.Where(x => x.Group.Open > 0)
            .GroupBy(x => x.Group.Currency)
            .Select(g => new WeightedRowDto(
                g.Key,
                Math.Round(g.Sum(x => x.Group.OwnWeighted + (x.Probability is int p ? x.Group.NoOwnValue * p / 100m : 0m)), 2),
                g.Sum(x => x.Group.OwnCovered + (x.Probability != null ? x.Group.NoOwnWithAmount : 0)),
                g.Sum(x => x.Probability != null ? 0 : x.Group.NoOwn)))
            .OrderByDescending(r => r.Value).ThenBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();
}
