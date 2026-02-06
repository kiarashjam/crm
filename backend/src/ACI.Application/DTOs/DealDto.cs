namespace ACI.Application.DTOs;

public record DealDto(Guid Id, string Name, string Value, string? Currency, string? Stage, Guid? PipelineId, Guid? DealStageId, Guid? CompanyId, Guid? ContactId, Guid? AssigneeId, DateTime? ExpectedCloseDateUtc, bool? IsWon, DateTime? LastActivityAtUtc);
