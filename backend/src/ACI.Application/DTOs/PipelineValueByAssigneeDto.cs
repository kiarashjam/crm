namespace ACI.Application.DTOs;

public record PipelineValueByAssigneeDto(string AssigneeUserId, string AssigneeName, int DealCount, decimal Value);
