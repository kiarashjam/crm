namespace ACI.Application.DTOs;

public record DealDto(
    Guid Id,
    string Name,
    string Value,
    string? Currency,
    string? Stage,
    Guid? PipelineId,
    Guid? DealStageId,
    Guid? CompanyId,
    Guid? ContactId,
    Guid? AssigneeId,
    DateTime? ExpectedCloseDateUtc,
    bool? IsWon,
    DateTime? LastActivityAtUtc,
    // New fields — HP-1
    string? Description,
    int? Probability,
    // Enriched names — HP-4
    string? AssigneeName,
    string? CompanyName,
    string? ContactName,
    string? PipelineName,
    string? DealStageName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    // Close reason — HP-8
    string? ClosedReason,
    string? ClosedReasonCategory,
    DateTime? ClosedAtUtc);
