using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing deal.
/// </summary>
public record UpdateDealRequest
{
    /// <summary>
    /// Deal name/title.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// Deal value/amount.
    /// </summary>
    [StringLength(50, ErrorMessage = "Value cannot exceed 50 characters")]
    [RegularExpression(@"^-?\d+(\.\d{1,2})?$", ErrorMessage = "Value must be a valid number (e.g., 1000 or 1000.50)")]
    public string? Value { get; init; }

    /// <summary>
    /// Currency code (e.g., USD, EUR, CHF).
    /// </summary>
    [StringLength(10, ErrorMessage = "Currency cannot exceed 10 characters")]
    public string? Currency { get; init; }

    /// <summary>
    /// Deal stage name (legacy, prefer using DealStageId).
    /// </summary>
    [StringLength(50, ErrorMessage = "Stage cannot exceed 50 characters")]
    public string? Stage { get; init; }

    /// <summary>
    /// Pipeline ID this deal belongs to.
    /// </summary>
    public Guid? PipelineId { get; init; }

    /// <summary>
    /// Deal stage ID within the pipeline.
    /// </summary>
    public Guid? DealStageId { get; init; }

    /// <summary>
    /// Associated company ID.
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// Primary contact ID.
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Assigned team member ID.
    /// </summary>
    public Guid? AssigneeId { get; init; }

    /// <summary>
    /// Expected close date.
    /// </summary>
    public DateTime? ExpectedCloseDateUtc { get; init; }

    /// <summary>
    /// Whether the deal is won.
    /// </summary>
    public bool? IsWon { get; init; }

    /// <summary>
    /// Deal description / notes.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Win probability (0–100).
    /// </summary>
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100")]
    public int? Probability { get; init; }

    /// <summary>
    /// Reason the deal was closed (won/lost).
    /// </summary>
    [StringLength(500, ErrorMessage = "Closed reason cannot exceed 500 characters")]
    public string? ClosedReason { get; init; }

    /// <summary>
    /// Category of close reason: Price, Timing, Competitor, NoDecision, FeatureGap, ChampionLeft, Other.
    /// </summary>
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string? ClosedReasonCategory { get; init; }
}
