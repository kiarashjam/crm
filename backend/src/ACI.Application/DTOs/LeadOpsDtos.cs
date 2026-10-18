namespace ACI.Application.DTOs;

public record ScoringRuleDto(Guid Id, string Field, string? MatchValue, int Points, bool IsActive);

public record SaveScoringRulesRequest(IReadOnlyList<ScoringRuleDto> Rules);

public record AssignmentRuleDto(
    Guid Id,
    string Name,
    string Kind,
    int Priority,
    string? SourceMatch,
    Guid? TerritoryId,
    Guid? QueueId,
    IReadOnlyList<Guid> MemberUserIds,
    bool IsActive);

public record TerritoryDto(Guid Id, string Name, string? Countries, string? Industries);

public record QueueDto(Guid Id, string Name, IReadOnlyList<Guid> MemberUserIds);

public record CaptureFormDto(Guid Id, string Name, string PublicToken, bool IsActive, string? SuccessMessage, string PublicPath);

/// <summary>
/// What a stranger submits on a public capture form.
/// </summary>
/// <remarks>
/// Limits match the Leads columns. Without them, a long country (81+ characters)
/// failed the insert with a 500 — after the company row and the round-robin
/// position had already been written.
/// </remarks>
/// <param name="Website">
/// A honeypot. The form renders it hidden from people; a bot filling every field
/// fills this one too, and is told "thanks" while nothing is saved.
/// </param>
public record PublicCaptureRequest(
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(256, MinimumLength = 1)] string Name,
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(256)] string Email,
    [System.ComponentModel.DataAnnotations.StringLength(64)] string? Phone,
    [System.ComponentModel.DataAnnotations.StringLength(200)] string? JobTitle,
    [System.ComponentModel.DataAnnotations.StringLength(80)] string? Country,
    [System.ComponentModel.DataAnnotations.StringLength(200)] string? Company,
    [System.ComponentModel.DataAnnotations.StringLength(200)] string? Website = null);

/// <summary>The whole of what the public capture endpoint says back: no record, no ids.</summary>
public record PublicCaptureReceipt(string Message);

public record PublicCaptureFormDto(string Name, string? SuccessMessage);

public record BulkLeadUpdateRequest(
    IReadOnlyList<Guid> Ids,
    string? Status = null,
    string? Source = null,
    Guid? AssignedToUserId = null,
    Guid? QueueId = null,
    bool ClearQueue = false);

public record BulkLeadUpdateResult(int Updated, int Missing);

public record LeadStatusEventDto(Guid Id, string? FromStatus, string ToStatus, Guid? ChangedByUserId, DateTime ChangedAtUtc);

public record ConversionSliceDto(
    string Key,
    int Created,
    int Qualified,
    int Converted,
    double ConversionRate,
    double? AvgHoursToFirstResponse,
    double? AvgHoursToConvert);

public record ConversionReportDto(
    IReadOnlyList<ConversionSliceDto> BySource,
    IReadOnlyList<ConversionSliceDto> ByOwner);

public record SlaSettingsDto(int Hours);

public record SequenceStepDto(Guid Id, int Order, string Type, int DayOffset, string? Subject, string? Body, string? TaskTitle, string? Note);

public record SequenceDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<SequenceStepDto> Steps);

/// <param name="Name">Required to create; optional to update. It used to be required on every
/// update too, so Pause / Activate — which sends only the status — was always rejected with
/// 400 "The Name field is required".</param>
public record SequenceWriteRequest(string? Name, string? Description, string? Status, IReadOnlyList<SequenceStepDto>? Steps);

public record EnrollmentDto(
    Guid Id,
    Guid SequenceId,
    string TargetType,
    Guid TargetId,
    string TargetName,
    string? TargetEmail,
    string Status,
    int CurrentStep,
    DateTime EnrolledAtUtc,
    DateTime? NextActionAtUtc);

public record EnrollRequest(Guid SequenceId, string TargetType, Guid TargetId, string TargetName, string? TargetEmail);

public record EnrollmentStatusRequest(string Status);
