using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IReportingService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineStageValueDto>> GetPipelineValueByStageAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineValueByAssigneeDto>> GetPipelineValueByAssigneeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
}
