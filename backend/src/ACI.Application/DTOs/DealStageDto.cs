namespace ACI.Application.DTOs;

public record DealStageDto(Guid Id, Guid PipelineId, string Name, int DisplayOrder, bool IsWon, bool IsLost);
