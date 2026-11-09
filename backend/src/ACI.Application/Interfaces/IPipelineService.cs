using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IPipelineService
{
    Task<Result<IReadOnlyList<PipelineDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<PipelineDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    /// <param name="staleDealDays">Its own stale-deal days (0 never), or null to follow the workspace's.</param>
    Task<Result<PipelineDto>> CreateAsync(Guid organizationId, string name, int displayOrder, int? staleDealDays = null, CancellationToken ct = default);
    /// <param name="staleDealDays">New stale-deal days for it (0 never); null keeps what it has.</param>
    /// <param name="followWorkspaceStaleDays">True drops its own setting, so the workspace's applies.</param>
    Task<Result<PipelineDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, int? staleDealDays = null, bool followWorkspaceStaleDays = false, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
