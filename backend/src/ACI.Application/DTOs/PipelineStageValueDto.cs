namespace ACI.Application.DTOs;

/// <summary>
/// Open pipeline value for one stage in one currency. A stage holding deals in
/// two currencies produces two rows, so nothing is ever silently added across
/// currencies.
/// </summary>
public record PipelineStageValueDto(string StageId, string StageName, string Currency, int DealCount, decimal Value);
