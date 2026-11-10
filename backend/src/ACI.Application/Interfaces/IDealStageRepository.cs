using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IDealStageRepository
{
    Task<IReadOnlyList<DealStage>> GetByPipelineIdAsync(Guid pipelineId, CancellationToken ct = default);
    Task<DealStage?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<DealStage> AddAsync(DealStage stage, CancellationToken ct = default);
    Task<DealStage?> UpdateAsync(DealStage stage, Guid organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    /// <summary>Returns true if any Deal references this stage.</summary>
    Task<bool> HasAnyDealsAsync(Guid stageId, CancellationToken ct = default);
    /// <summary>
    /// The deals in this stage whose outcome is <paramref name="outcome"/> (won, lost, or
    /// null for still open), tracked, so what is changed on them is saved with the stage.
    /// </summary>
    Task<IReadOnlyList<Deal>> GetDealsWithOutcomeAsync(Guid stageId, bool? outcome, CancellationToken ct = default);
}
