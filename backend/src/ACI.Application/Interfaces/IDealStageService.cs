using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IDealStageService
{
    Task<Result<IReadOnlyList<DealStageDto>>> GetByPipelineIdAsync(Guid pipelineId, Guid organizationId, CancellationToken ct = default);
    Task<Result<DealStageDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    /// <param name="probability">The stage's default probability, 0–100, or null for none.</param>
    Task<Result<DealStageDto>> CreateAsync(Guid pipelineId, Guid organizationId, string name, int displayOrder, bool isWon, bool isLost, int? probability = null, CancellationToken ct = default);
    /// <param name="probability">A new default probability, 0–100; null keeps the current one.</param>
    /// <param name="clearProbability">True takes the stage's default probability away.</param>
    Task<Result<DealStageDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, bool? isWon, bool? isLost, int? probability = null, bool clearProbability = false, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
