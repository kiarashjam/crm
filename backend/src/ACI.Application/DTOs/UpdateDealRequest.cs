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
    /// The deal's new amount, rounded to cents. When both this and <see cref="Value"/> are
    /// sent, this one counts.
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// The new amount as text, for clients from before <see cref="Amount"/>. Plain digits only.
    /// </summary>
    [StringLength(50, ErrorMessage = "Value cannot exceed 50 characters")]
    [RegularExpression(@"^-?\d+(\.\d{1,2})?$", ErrorMessage = "Value must be a valid number (e.g., 1000 or 1000.50)")]
    public string? Value { get; init; }

    /// <summary>
    /// ISO 4217 code (e.g. CHF, EUR). Changing it relabels the amount; nothing is converted.
    /// </summary>
    [StringLength(10, ErrorMessage = "Currency cannot exceed 10 characters")]
    public string? Currency { get; init; }

    /// <summary>
    /// Deal stage name (legacy, prefer using DealStageId).
    /// </summary>
    // As long as a stage's name can be: the board sends the name with the stage's id,
    // and capped at 50 a move into a stage with a longer name was refused.
    [StringLength(128, ErrorMessage = "Stage cannot exceed 128 characters")]
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

    /// <summary>When true, only the assignee, the creator, and managers can see the deal.</summary>
    public bool? IsPrivate { get; init; }

    /// <summary>The sales team this deal belongs to.</summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// The deal's version as this edit read it (<c>version</c> on the deal, or its ETag).
    /// When sent, the edit is refused with 409 if anyone has saved the deal since, rather
    /// than written over their changes. Left out, the edit is applied to the deal as it
    /// is now: right for a change to one field that reads nothing else, such as a move to another stage.
    /// </summary>
    public Guid? Version { get; init; }
}
