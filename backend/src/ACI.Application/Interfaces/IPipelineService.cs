using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IPipelineService
{
    Task<Result<IReadOnlyList<PipelineDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<PipelineDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<Result<PipelineDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default);
    Task<Result<PipelineDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
