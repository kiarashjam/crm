namespace ACI.Application.DTOs;

/// <summary>
/// Open pipeline value for one person in one currency. See PipelineStageValueDto
/// on why currency is part of the grouping rather than a display detail.
/// </summary>
public record PipelineValueByAssigneeDto(string AssigneeUserId, string AssigneeName, string Currency, int DealCount, decimal Value);
