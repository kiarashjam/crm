using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IReportingService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, CancellationToken ct = default);
}
