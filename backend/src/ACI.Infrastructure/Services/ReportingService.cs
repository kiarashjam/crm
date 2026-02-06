using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var leadsCount = await _db.Leads.CountAsync(l => l.UserId == userId && (organizationId == null ? l.OrganizationId == null : l.OrganizationId == organizationId), ct);
        var deals = await _db.Deals.Where(d => d.UserId == userId && (organizationId == null ? d.OrganizationId == null : d.OrganizationId == organizationId)).ToListAsync(ct);
        var activeDeals = deals.Where(d => d.IsWon == null).ToList();
        var wonCount = deals.Count(d => d.IsWon == true);
        var lostCount = deals.Count(d => d.IsWon == false);
        decimal pipelineValue = 0;
        foreach (var d in activeDeals)
        {
            if (decimal.TryParse((d.Value ?? "").Replace("$", "").Replace(",", "").Trim(), out var v))
                pipelineValue += v;
        }
        return new DashboardStatsDto(leadsCount, activeDeals.Count, pipelineValue, wonCount, lostCount);
    }

    public async Task<IReadOnlyList<PipelineStageValueDto>> GetPipelineValueByStageAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var deals = await _db.Deals
            .Where(d => d.UserId == userId && (organizationId == null ? d.OrganizationId == null : d.OrganizationId == organizationId) && d.IsWon == null)
            .ToListAsync(ct);
        var stageIds = deals.Where(d => d.DealStageId != null).Select(d => d.DealStageId!.Value).Distinct().ToList();
        var stages = await _db.DealStages.Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var byStage = deals
            .GroupBy(d => d.DealStageId?.ToString() ?? d.Stage ?? "Unset")
            .Select(g =>
            {
                var stageId = g.Key;
                var stageName = g.Key == "Unset" ? "Unset" : (Guid.TryParse(g.Key, out var sid) && stages.TryGetValue(sid, out var name) ? name : g.First().Stage ?? g.Key);
                var value = g.Sum(d => decimal.TryParse((d.Value ?? "").Replace("$", "").Replace(",", "").Trim(), out var v) ? v : 0);
                return new PipelineStageValueDto(stageId, stageName, g.Count(), value);
            })
            .OrderBy(x => x.StageName)
            .ToList();
        return byStage;
    }

    public async Task<IReadOnlyList<PipelineValueByAssigneeDto>> GetPipelineValueByAssigneeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var deals = await _db.Deals
            .Include(d => d.Assignee)
            .Where(d => d.UserId == userId && (organizationId == null ? d.OrganizationId == null : d.OrganizationId == organizationId) && d.IsWon == null)
            .ToListAsync(ct);
        var byAssignee = deals
            .GroupBy(d => d.AssigneeId?.ToString() ?? "")
            .Select(g =>
            {
                var assigneeId = g.Key;
                var assigneeName = string.IsNullOrEmpty(g.Key) ? "Unassigned" : (g.First().Assignee?.Name ?? "Unknown");
                var value = g.Sum(d => decimal.TryParse((d.Value ?? "").Replace("$", "").Replace(",", "").Trim(), out var v) ? v : 0);
                return new PipelineValueByAssigneeDto(assigneeId, assigneeName, g.Count(), value);
            })
            .OrderBy(x => x.AssigneeName)
            .ToList();
        return byAssignee;
    }
}
