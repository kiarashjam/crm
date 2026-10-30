using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The figures behind the dashboard.
/// </summary>
/// <remarks>
/// Two bugs shaped this file, and both produced wrong numbers rather than errors:
///
/// <para>
/// SCOPE. Every query here filtered <c>d.UserId == userId</c> even when an
/// organisation was selected, while <c>DealRepository</c> and
/// <c>LeadRepository</c> filter by organisation ALONE in that case. So the
/// dashboard counted only the signed-in user's records while the Deals and Leads
/// pages listed the whole organisation's — two colleagues saw different pipeline
/// totals and neither matched the Deals page. The scope now matches the
/// repositories exactly; see <see cref="ScopedDeals"/>.
/// </para>
/// <para>
/// MONEY. Values were read with <c>Replace("$","")</c> and fell through to zero
/// on a failed parse, so a deal priced "CHF 85,500" counted as nothing. Amounts
/// now go through <see cref="MoneyText"/>, which reports failure, and totals are
/// grouped by currency instead of added across them.
/// </para>
/// <para>
/// Everything here is added up by the database, over <c>Deal.Amount</c> (the value
/// read once at save time), rather than by loading every deal to parse its text.
/// A deal whose value could not be read has no Amount, and is counted as unreadable.
/// </para>
/// </remarks>
public partial class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db) => _db = db;

    /// <summary>
    /// Deals in scope, matching <c>DealRepository</c>: an organisation sees its deals,
    /// less private ones the caller may not see; a user outside one sees their own.
    /// </summary>
    private IQueryable<Deal> ScopedDeals(Guid userId, Guid? organizationId)
        => _db.Deals.AsNoTracking().VisibleTo(_db, userId, organizationId);

    /// <summary>Leads in scope, matching <c>LeadRepository</c>.</summary>
    private IQueryable<Lead> ScopedLeads(Guid userId, Guid? organizationId)
        => organizationId == null
            ? _db.Leads.AsNoTracking().Where(l => l.UserId == userId && l.OrganizationId == null)
            : _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId);

    /// <summary>
    /// The currency a deal is counted in, as SQL can work it out: the rule of
    /// <see cref="MoneyText.NormaliseCurrency"/>, blank meaning USD.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<Deal, string>> CurrencyOf =
        d => d.Currency == null || d.Currency.Trim() == "" ? "USD" : d.Currency.Trim().ToUpper();

    /// <summary>
    /// Totals per currency, largest first, plus a count of the deals whose value
    /// could not be read at all. Added up by the database, over Deal.Amount.
    /// </summary>
    private static async Task<(List<CurrencyTotalDto> Totals, int Unreadable)> TotalsByCurrencyAsync(IQueryable<Deal> deals, CancellationToken ct)
    {
        var unreadable = await deals.CountAsync(d => d.Amount == null, ct);
        var sums = await deals
            .Where(d => d.Amount != null)
            .GroupBy(CurrencyOf)
            .Select(g => new { Currency = g.Key, Value = g.Sum(d => d.Amount!.Value), Count = g.Count() })
            .ToListAsync(ct);
        var totals = sums
            .Select(x => new CurrencyTotalDto(x.Currency, x.Value, x.Count))
            // Ties break on the currency code, so the order — and therefore which
            // currency is called dominant — is stable between requests.
            .OrderByDescending(t => t.Value)
            .ThenBy(t => t.Currency, StringComparer.Ordinal)
            .ToList();
        return (totals, unreadable);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // A converted lead is a contact or a deal now, not a lead to work. The tile links
        // to the Leads page, whose Active count leaves them out, and counting them here
        // made the two disagree by every lead ever converted.
        var leadsCount = await ScopedLeads(userId, organizationId).CountAsync(l => !l.IsConverted, ct);
        var deals = ScopedDeals(userId, organizationId);
        var outcomes = await deals
            .GroupBy(d => d.IsWon)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int CountOf(bool? outcome) => outcomes.Where(o => o.Outcome == outcome).Sum(o => o.Count);

        var (totals, unreadable) = await TotalsByCurrencyAsync(deals.Where(d => d.IsWon == null), ct);
        var dominant = totals.FirstOrDefault();

        return new DashboardStatsDto(
            leadsCount,
            CountOf(null),
            dominant?.Value ?? 0m,
            dominant?.Currency ?? "USD",
            totals,
            unreadable,
            CountOf(true),
            CountOf(false));
    }

    public async Task<IReadOnlyList<PipelineStageValueDto>> GetPipelineValueByStageAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Grouped by stage AND currency: a stage holding CHF and EUR deals yields
        // two rows rather than one meaningless sum. A deal with only a stage name
        // (from before stages had ids) is grouped by that name.
        var groups = await ScopedDeals(userId, organizationId)
            .Where(d => d.IsWon == null)
            .GroupBy(d => new
            {
                d.DealStageId,
                StageName = d.DealStageId == null ? d.Stage : null,
                Currency = d.Currency == null || d.Currency.Trim() == "" ? "USD" : d.Currency.Trim().ToUpper(),
            })
            .Select(g => new
            {
                g.Key.DealStageId,
                g.Key.StageName,
                g.Key.Currency,
                Count = g.Count(),
                Value = g.Sum(d => d.Amount ?? 0m),
                // The name the deal was saved with, for a stage since deleted.
                SavedName = g.Max(d => d.Stage),
            })
            .ToListAsync(ct);

        var stageIds = groups.Where(g => g.DealStageId != null).Select(g => g.DealStageId!.Value).Distinct().ToList();
        var stages = await _db.DealStages.AsNoTracking().Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return groups
            .Select(g =>
            {
                var stageKey = g.DealStageId?.ToString() ?? g.StageName ?? "Unset";
                var stageName = g.DealStageId is Guid sid && stages.TryGetValue(sid, out var name)
                    ? name
                    : g.StageName ?? g.SavedName ?? (g.DealStageId == null ? "Unset" : stageKey);
                return new PipelineStageValueDto(stageKey, stageName, g.Currency, g.Count, g.Value);
            })
            .OrderBy(x => x.StageName, StringComparer.Ordinal)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<PipelineValueByAssigneeDto>> GetPipelineValueByAssigneeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var groups = await ScopedDeals(userId, organizationId)
            .Where(d => d.IsWon == null)
            .GroupBy(d => new
            {
                d.AssigneeId,
                Currency = d.Currency == null || d.Currency.Trim() == "" ? "USD" : d.Currency.Trim().ToUpper(),
            })
            .Select(g => new { g.Key.AssigneeId, g.Key.Currency, Count = g.Count(), Value = g.Sum(d => d.Amount ?? 0m) })
            .ToListAsync(ct);

        var names = await NamesAsync(groups.Select(g => g.AssigneeId), ct);
        return groups
            .Select(g => new PipelineValueByAssigneeDto(
                g.AssigneeId?.ToString() ?? string.Empty,
                g.AssigneeId is Guid id ? names.GetValueOrDefault(id, "Unknown") : "Unassigned",
                g.Currency,
                g.Count,
                g.Value))
            .OrderBy(x => x.AssigneeName, StringComparer.Ordinal)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Display names of these users, by id.</summary>
    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> userIds, CancellationToken ct)
    {
        var ids = userIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

}
