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
            ? _db.Deals.AsNoTracking().Where(d => d.UserId == userId && d.OrganizationId == null)
            : _db.Deals.AsNoTracking().Where(d => d.OrganizationId == organizationId);

    /// <summary>Leads in scope, matching <c>LeadRepository</c>.</summary>
    private IQueryable<Lead> ScopedLeads(Guid userId, Guid? organizationId)
        => organizationId == null
            ? _db.Leads.AsNoTracking().Where(l => l.UserId == userId && l.OrganizationId == null)
            : _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId);

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
            // `out var existing` yields default((decimal, int)) when the key is
            // absent, which is exactly the zero starting point wanted here. The
            // previous form used a ternary with a bare `(0m, 0)` fallback, and
            // because that literal carries no element names the whole expression
            // unified to an unnamed `(decimal, int)` — losing `.Value` and
            // `.Count` and failing to compile.
            sums.TryGetValue(currency, out var existing);
            sums[currency] = (existing.Value + amount, existing.Count + 1);
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
        var deals = await ScopedDeals(userId, organizationId)
            .Select(d => new Deal { IsWon = d.IsWon, Value = d.Value, Currency = d.Currency })
            .ToListAsync(ct);

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
        var deals = await ScopedDeals(userId, organizationId)
            .Where(d => d.IsWon == null)
            .Select(d => new { d.DealStageId, d.Stage, d.Value, d.Currency })
            .ToListAsync(ct);

        var stageIds = deals.Where(d => d.DealStageId != null).Select(d => d.DealStageId!.Value).Distinct().ToList();
        var stages = await _db.DealStages.AsNoTracking().Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

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
            .Where(d => d.IsWon == null)
            .Select(d => new
            {
                d.AssigneeId,
                AssigneeName = d.Assignee != null ? d.Assignee.Name : null,
                d.Value,
                d.Currency,
            })
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
                    : (g.First().AssigneeName ?? "Unknown");
                var value = g.Sum(d => MoneyText.TryParseAmount(d.Value, out var v) ? v : 0m);
                return new PipelineValueByAssigneeDto(assigneeId, assigneeName, g.Key.Currency, g.Count(), value);
            })
            .OrderBy(x => x.AssigneeName, StringComparer.Ordinal)
            .ThenBy(x => x.Currency, StringComparer.Ordinal)
            .ToList();
        return rows;
    }

    public async Task<IReadOnlyList<ForecastSnapshotDto>> SaveForecastSnapshotAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var deals = await ScopedDeals(userId, organizationId)
            .Where(d => d.IsWon == null)
            .Select(d => new
            {
                d.AssigneeId,
                AssigneeName = d.Assignee != null ? d.Assignee.Name : null,
                d.Value,
                d.Currency,
                d.Probability,
            })
            .ToListAsync(ct);

        var rows = deals
            .GroupBy(d => new
            {
                AssigneeKey = d.AssigneeId?.ToString() ?? string.Empty,
                Currency = MoneyText.NormaliseCurrency(d.Currency),
            })
            .Select(g =>
            {
                decimal open = 0, weighted = 0;
                var covered = 0;
                var missing = 0;
                foreach (var deal in g)
                {
                    if (!MoneyText.TryParseAmount(deal.Value, out var amount)) continue;
                    open += amount;
                    if (deal.Probability is int probability)
                    {
                        weighted += amount * probability / 100m;
                        covered++;
                    }
                    else missing++;
                }
                return new ForecastSnapshot
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    SavedByUserId = userId,
                    MonthUtc = month,
                    Currency = g.Key.Currency,
                    AssigneeId = string.IsNullOrEmpty(g.Key.AssigneeKey) ? null : Guid.Parse(g.Key.AssigneeKey),
                    AssigneeName = string.IsNullOrEmpty(g.Key.AssigneeKey) ? "Unassigned" : (g.First().AssigneeName ?? "Unknown"),
                    WeightedValue = weighted,
                    OpenValue = open,
                    Covered = covered,
                    Missing = missing,
                    SavedAtUtc = now,
                };
            })
            .ToList();

        var previous = await _db.ForecastSnapshots
            .Where(s => s.OrganizationId == organizationId && s.MonthUtc == month)
            .ToListAsync(ct);
        _db.ForecastSnapshots.RemoveRange(previous);
        _db.ForecastSnapshots.AddRange(rows);
        await _db.SaveChangesAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ForecastSnapshotDto>> ListForecastSnapshotsAsync(Guid userId, Guid? organizationId, int year, int month, CancellationToken ct = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = await _db.ForecastSnapshots.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.MonthUtc == start)
            .OrderBy(s => s.AssigneeName)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static ForecastSnapshotDto ToDto(ForecastSnapshot row) =>
        new(row.Id, row.MonthUtc.ToString("yyyy-MM"), row.Currency, row.AssigneeName, row.WeightedValue, row.OpenValue, row.Covered, row.Missing, row.SavedAtUtc);
}
