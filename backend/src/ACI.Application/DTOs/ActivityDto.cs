namespace ACI.Application.DTOs;

public record ActivityDto(
    Guid Id,
    string Type,
    string? Subject,
    string? Body,
    Guid? ContactId,
    Guid? DealId,
    Guid? LeadId,
    string? Participants,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc = null,
    string? ContactName = null,
    string? DealName = null,
    string? LeadName = null,
    Guid? CreatedByUserId = null,
    string? CreatedByName = null,
    string? CreatedByEmail = null,
    // When it happened, which can be before it was logged (CreatedAtUtc).
    DateTime? OccurredAtUtc = null,
    // How a call or meeting went, how long it took, and who reached out (see ActivityFacts).
    string? Outcome = null,
    int? DurationMinutes = null,
    string? Direction = null);
