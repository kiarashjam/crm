namespace ACI.Application.DTOs;

public record PipelineStageValueDto(string StageId, string StageName, int DealCount, decimal Value);
