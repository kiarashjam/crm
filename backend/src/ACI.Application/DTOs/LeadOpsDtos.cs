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

public record PublicCaptureRequest(string Name, string Email, string? Phone, string? JobTitle, string? Country, string? Company);

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

public record SequenceWriteRequest(string Name, string? Description, string? Status, IReadOnlyList<SequenceStepDto>? Steps);

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
