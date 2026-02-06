using System.ComponentModel.DataAnnotations;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

// Analytics Dashboard DTOs
public record CopyAnalyticsSummaryDto(
    int TotalGenerations,
    int TotalRewrites,
    int TotalCopied,
    int TotalSent,
    int TotalResponses,
    double OverallResponseRate,
    IReadOnlyList<CopyTypeAnalyticsDto> ByType,
    IReadOnlyList<DailyAnalyticsDto> DailyTrend
);

public record CopyTypeAnalyticsDto(
    string CopyTypeId,
    int GenerationCount,
    int SendCount,
    int ResponseCount,
    double ResponseRate
);

public record DailyAnalyticsDto(
    DateTime Date,
    int GenerationCount,
    int SendCount,
    int ResponseCount
);

/// <summary>
/// Request to track a copy event.
/// </summary>
public record TrackCopyEventRequest
{
    public Guid? CopyHistoryId { get; init; }

    /// <summary>
    /// Event type: copy, send, open, response.
    /// </summary>
    [Required(ErrorMessage = "EventType is required")]
    [StringLength(20, ErrorMessage = "EventType cannot exceed 20 characters")]
    [RegularExpression(@"^(copy|send|open|response)$", ErrorMessage = "EventType must be one of: copy, send, open, response")]
    public required string EventType { get; init; }

    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public string? CopyTypeId { get; init; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "RecipientEmail cannot exceed 254 characters")]
    public string? RecipientEmail { get; init; }
}

// Conversion Tracking DTOs
public record CopyConversionDto(
    Guid Id,
    Guid? CopyHistoryId,
    string CopyTypeId,
    string? RecipientEmail,
    string? RecipientName,
    string ConversionType,
    string? Notes,
    DateTime CreatedAtUtc
);

/// <summary>
/// Request to create a conversion.
/// </summary>
public record CreateConversionRequest
{
    public Guid? CopyHistoryId { get; init; }

    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "RecipientEmail cannot exceed 254 characters")]
    public string? RecipientEmail { get; init; }

    [StringLength(200, ErrorMessage = "RecipientName cannot exceed 200 characters")]
    public string? RecipientName { get; init; }

    [Required(ErrorMessage = "ConversionType is required")]
    [StringLength(50, ErrorMessage = "ConversionType cannot exceed 50 characters")]
    public required string ConversionType { get; init; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; init; }
}

// Email Sequence DTOs
public record EmailSequenceDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    bool IsSharedWithOrganization,
    int StepCount,
    int ActiveEnrollments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<EmailSequenceStepDto>? Steps
);

public record EmailSequenceStepDto(
    Guid Id,
    int StepOrder,
    string Subject,
    string Body,
    string CopyTypeId,
    int DelayDays,
    int DelayHours,
    bool StopOnReply
);

/// <summary>
/// Request to create an email sequence.
/// </summary>
public record CreateEmailSequenceRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    public bool IsSharedWithOrganization { get; init; } = false;

    public IReadOnlyList<CreateSequenceStepRequest>? Steps { get; init; } = null;
}

/// <summary>
/// Request to create a sequence step.
/// </summary>
public record CreateSequenceStepRequest
{
    [Required(ErrorMessage = "Subject is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Subject must be between 1 and 200 characters")]
    public required string Subject { get; init; }

    [Required(ErrorMessage = "Body is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Body must be between 1 and 10000 characters")]
    public required string Body { get; init; }

    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    [Range(0, 365, ErrorMessage = "DelayDays must be between 0 and 365")]
    public int DelayDays { get; init; } = 0;

    [Range(0, 23, ErrorMessage = "DelayHours must be between 0 and 23")]
    public int DelayHours { get; init; } = 0;

    public bool StopOnReply { get; init; } = true;
}

/// <summary>
/// Request to update an email sequence.
/// </summary>
public record UpdateEmailSequenceRequest
{
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }

    public bool? IsSharedWithOrganization { get; init; }
}

/// <summary>
/// Request to enroll in a sequence.
/// </summary>
public record EnrollInSequenceRequest
{
    public Guid SequenceId { get; init; }

    public Guid? ContactId { get; init; }

    public Guid? LeadId { get; init; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "RecipientEmail cannot exceed 254 characters")]
    public string? RecipientEmail { get; init; }

    [StringLength(200, ErrorMessage = "RecipientName cannot exceed 200 characters")]
    public string? RecipientName { get; init; }
}

public record EmailSequenceEnrollmentDto(
    Guid Id,
    Guid SequenceId,
    string? SequenceName,
    Guid? ContactId,
    Guid? LeadId,
    string? RecipientEmail,
    string? RecipientName,
    int CurrentStep,
    string Status,
    DateTime EnrolledAtUtc,
    DateTime? LastSentAtUtc,
    DateTime? NextSendAtUtc,
    DateTime? CompletedAtUtc
);

// A/B Testing DTOs
public record ABTestDto(
    Guid Id,
    string Name,
    string? Description,
    string CopyTypeId,
    string Goal,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? EndedAtUtc,
    Guid? WinningVariantId,
    IReadOnlyList<ABTestVariantDto>? Variants
);

public record ABTestVariantDto(
    Guid Id,
    string Name,
    string Subject,
    string Body,
    int SendCount,
    int OpenCount,
    int ClickCount,
    int ReplyCount,
    double OpenRate,
    double ClickRate,
    double ReplyRate
);

/// <summary>
/// Request to create an A/B test.
/// </summary>
public record CreateABTestRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    [Required(ErrorMessage = "Variants are required")]
    [MinLength(2, ErrorMessage = "At least 2 variants are required")]
    public required IReadOnlyList<CreateABTestVariantRequest> Variants { get; init; }
}

/// <summary>
/// Request to create an A/B test variant.
/// </summary>
public record CreateABTestVariantRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public required string Name { get; init; }

    [Required(ErrorMessage = "Subject is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Subject must be between 1 and 200 characters")]
    public required string Subject { get; init; }

    [Required(ErrorMessage = "Body is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Body must be between 1 and 10000 characters")]
    public required string Body { get; init; }
}

/// <summary>
/// Request to update an A/B test.
/// </summary>
public record UpdateABTestRequest
{
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Status: start, complete, cancel.
    /// </summary>
    [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters")]
    [RegularExpression(@"^(start|complete|cancel)$", ErrorMessage = "Status must be one of: start, complete, cancel")]
    public string? Status { get; init; }
}

/// <summary>
/// Request to generate copy in multiple languages.
/// </summary>
public record GenerateCopyMultiLanguageRequest
{
    [Required(ErrorMessage = "CopyTypeId is required")]
    [StringLength(50, ErrorMessage = "CopyTypeId cannot exceed 50 characters")]
    public required string CopyTypeId { get; init; }

    [Required(ErrorMessage = "Goal is required")]
    [StringLength(500, ErrorMessage = "Goal cannot exceed 500 characters")]
    public required string Goal { get; init; }

    [StringLength(50, ErrorMessage = "BrandTone cannot exceed 50 characters")]
    public string? BrandTone { get; init; }

    [StringLength(20, ErrorMessage = "Length cannot exceed 20 characters")]
    public string? Length { get; init; }

    /// <summary>
    /// Target language code (e.g., "es", "fr", "de", "zh").
    /// </summary>
    [Required(ErrorMessage = "TargetLanguage is required")]
    [StringLength(10, ErrorMessage = "TargetLanguage cannot exceed 10 characters")]
    public required string TargetLanguage { get; init; }

    public RecipientDto? Recipient { get; init; }

    public CrmObjectDto? CrmObject { get; init; }
}

/// <summary>
/// Request to check for spam.
/// </summary>
public record SpamCheckRequest
{
    [Required(ErrorMessage = "Subject is required")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
    public required string Subject { get; init; }

    [Required(ErrorMessage = "Body is required")]
    [StringLength(10000, ErrorMessage = "Body cannot exceed 10000 characters")]
    public required string Body { get; init; }
}

public record SpamCheckResponse(
    double Score, // 0-100, lower is better
    string Rating, // "Good", "Warning", "Spam Risk"
    IReadOnlyList<SpamIssueDto> Issues
);

public record SpamIssueDto(
    string Type, // "spam_words", "caps", "links", etc.
    string Description,
    string Severity // "low", "medium", "high"
);
