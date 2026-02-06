using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IDealStageRepository
{
    Task<IReadOnlyList<DealStage>> GetByPipelineIdAsync(Guid pipelineId, CancellationToken ct = default);
    Task<DealStage?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<DealStage> AddAsync(DealStage stage, CancellationToken ct = default);
    Task<DealStage?> UpdateAsync(DealStage stage, Guid organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
