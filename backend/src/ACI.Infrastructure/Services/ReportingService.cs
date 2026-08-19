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
/// </remarks>
public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db) => _db = db;

    /// <summary>
    /// Deals in scope, matching <c>DealRepository</c>: an organisation sees all of
    /// its deals; a user outside one sees their own personal deals.
    /// </summary>
    private IQueryable<Deal> ScopedDeals(Guid userId, Guid? organizationId)
        => organizationId == null
            ? _db.Deals.Where(d => d.UserId == userId && d.OrganizationId == null)
            : _db.Deals.Where(d => d.OrganizationId == organizationId);

    /// <summary>Leads in scope, matching <c>LeadRepository</c>.</summary>
    private IQueryable<Lead> ScopedLeads(Guid userId, Guid? organizationId)
        => organizationId == null
            ? _db.Leads.Where(l => l.UserId == userId && l.OrganizationId == null)
            : _db.Leads.Where(l => l.OrganizationId == organizationId);

    /// <summary>
    /// Totals per currency, largest first, plus a count of the deals whose value
    /// could not be read at all.
    /// </summary>
    private static (List<CurrencyTotalDto> Totals, int Unreadable) TotalsByCurrency(IEnumerable<Deal> deals)
    {
        var sums = new Dictionary<string, (decimal Value, int Count)>();
        var unreadable = 0;
        foreach (var d in deals)
        {
            if (!MoneyText.TryParseAmount(d.Value, out var amount))
            {
                unreadable++;
                continue;
            }
            var currency = MoneyText.NormaliseCurrency(d.Currency);
            var current = sums.TryGetValue(currency, out var existing) ? existing : (0m, 0);
            sums[currency] = (current.Value + amount, current.Count + 1);
        }
        var totals = sums
            .Select(kv => new CurrencyTotalDto(kv.Key, kv.Value.Value, kv.Value.Count))
            // Ties break on the currency code, so the order — and therefore which
            // currency is called dominant — is stable between requests.
            .OrderByDescending(t => t.Value)
            .ThenBy(t => t.Currency, StringComparer.Ordinal)
            .ToList();
        return (totals, unreadable);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var leadsCount = await ScopedLeads(userId, organizationId).CountAsync(ct);
        var deals = await ScopedDeals(userId, organizationId).ToListAsync(ct);

        var activeDeals = deals.Where(d => d.IsWon == null).ToList();
        var wonCount = deals.Count(d => d.IsWon == true);
        var lostCount = deals.Count(d => d.IsWon == false);

        var (totals, unreadable) = TotalsByCurrency(activeDeals);
        var dominant = totals.FirstOrDefault();

        return new DashboardStatsDto(
            leadsCount,
            activeDeals.Count,
            dominant?.Value ?? 0m,
            dominant?.Currency ?? "USD",
            totals,
            unreadable,
            wonCount,
            lostCount);
    }

    public async Task<IReadOnlyList<PipelineStageValueDto>> GetPipelineValueByStageAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var deals = await ScopedDeals(userId, organizationId).Where(d => d.IsWon == null).ToListAsync(ct);

        var stageIds = deals.Where(d => d.DealStageId != null).Select(d => d.DealStageId!.Value).Distinct().ToList();
        var stages = await _db.DealStages.Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        // Grouped by stage AND currency: a stage holding CHF and EUR deals yields
        // two rows rather than one meaningless sum.
        var rows = deals
            .GroupBy(d => new
            {
                StageKey = d.DealStageId?.ToString() ?? d.Stage ?? "Unset",
                Currency = MoneyText.NormaliseCurrency(d.Currency),
            })
            .Select(g =>
            {
                var stageKey = g.Key.StageKey;
                var stageName = stageKey == "Unset"
                    ? "Unset"
                    : (Guid.TryParse(stageKey, out var sid) && stages.TryGetValue(sid, out var name) ? name : g.First().Stage ?? stageKey);
                var value = g.Sum(d => MoneyText.TryParseAmount(d.Value, out var v) ? v : 0m);
                return new PipelineStageValueDto(stageKey, stageName, g.Key.Currency, g.Count(), value);
            })
            .OrderBy(x => x.StageName, StringComparer.Ordinal)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
        return rows;
    }

    public async Task<IReadOnlyList<PipelineValueByAssigneeDto>> GetPipelineValueByAssigneeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var deals = await ScopedDeals(userId, organizationId)
            .Include(d => d.Assignee)
            .Where(d => d.IsWon == null)
            .ToListAsync(ct);

        var rows = deals
            .GroupBy(d => new
            {
                AssigneeKey = d.AssigneeId?.ToString() ?? string.Empty,
                Currency = MoneyText.NormaliseCurrency(d.Currency),
            })
            .Select(g =>
            {
                var assigneeId = g.Key.AssigneeKey;
                var assigneeName = string.IsNullOrEmpty(assigneeId)
                    ? "Unassigned"
                    : (g.First().Assignee?.Name ?? "Unknown");
                var value = g.Sum(d => MoneyText.TryParseAmount(d.Value, out var v) ? v : 0m);
                return new PipelineValueByAssigneeDto(assigneeId, assigneeName, g.Key.Currency, g.Count(), value);
            })
            .OrderBy(x => x.AssigneeName, StringComparer.Ordinal)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
        return rows;
    }
}
