using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IReportingService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineStageValueDto>> GetPipelineValueByStageAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineValueByAssigneeDto>> GetPipelineValueByAssigneeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Every figure on the Reports page for one period, scope, pipeline and currency.</summary>
    Task<Result<ReportSummaryDto>> GetSummaryAsync(Guid userId, Guid? organizationId, ReportFilter filter, CancellationToken ct = default);

    /// <summary>Open deals by the month they are expected to close in, beside what was won each month.</summary>
    Task<Result<ForecastDto>> GetForecastAsync(Guid userId, Guid? organizationId, ForecastFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Saves the forecast for a month (the month it is now on the workspace's calendar
    /// when none is given), beside any earlier saves for it, and returns the month's saves
    /// with this one selected.
    /// </summary>
    Task<Result<ForecastSnapshotSetDto>> SaveForecastSnapshotAsync(Guid userId, Guid? organizationId, int? year, int? month, CancellationToken ct = default);

    /// <summary>
    /// A month's saves, the lines of one of them (the latest unless another is named), and
    /// what was won that month. A year or month left out is the one it is now on the
    /// workspace's calendar.
    /// </summary>
    Task<Result<ForecastSnapshotSetDto>> ListForecastSnapshotsAsync(Guid userId, Guid? organizationId, int? year, int? month, Guid? snapshotId = null, CancellationToken ct = default);
}
