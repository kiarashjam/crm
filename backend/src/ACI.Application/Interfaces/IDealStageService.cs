using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IDealStageService
{
    Task<Result<IReadOnlyList<DealStageDto>>> GetByPipelineIdAsync(Guid pipelineId, Guid organizationId, CancellationToken ct = default);
    Task<Result<DealStageDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<Result<DealStageDto>> CreateAsync(Guid pipelineId, Guid organizationId, string name, int displayOrder, bool isWon, bool isLost, CancellationToken ct = default);
    Task<Result<DealStageDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, bool? isWon, bool? isLost, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
