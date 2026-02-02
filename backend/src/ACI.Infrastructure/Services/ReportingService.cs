using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, CancellationToken ct = default)
    {
        var leadsCount = await _db.Leads.CountAsync(l => l.UserId == userId, ct);
        var deals = await _db.Deals.Where(d => d.UserId == userId).ToListAsync(ct);
        var activeDeals = deals.Where(d => d.IsWon == null).ToList();
        var wonCount = deals.Count(d => d.IsWon == true);
        var lostCount = deals.Count(d => d.IsWon == false);
        decimal pipelineValue = 0;
        foreach (var d in activeDeals)
        {
            if (decimal.TryParse(d.Value.Replace("$", "").Replace(",", "").Trim(), out var v))
                pipelineValue += v;
        }
        return new DashboardStatsDto(leadsCount, activeDeals.Count, pipelineValue, wonCount, lostCount);
    }
}
