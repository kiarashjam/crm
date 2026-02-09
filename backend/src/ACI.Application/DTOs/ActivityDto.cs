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
    string? LeadName = null);
