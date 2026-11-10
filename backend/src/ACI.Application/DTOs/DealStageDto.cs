namespace ACI.Application.DTOs;

/// <param name="Probability">
/// The chance, 0–100, a deal in this stage is won, used for the deals in it that have no
/// probability of their own. Null when the stage has none.
/// </param>
public record DealStageDto(Guid Id, Guid PipelineId, string Name, int DisplayOrder, bool IsWon, bool IsLost, int? Probability = null);
