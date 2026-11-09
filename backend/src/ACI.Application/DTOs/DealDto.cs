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
    DateTime? ClosedAtUtc,
    bool IsPrivate = false,
    Guid? TeamId = null,
    // The amount as a number, in Currency. Null only for a deal saved before amounts
    // were typed whose text holds no number; Value is the same amount as text.
    decimal? Amount = null,
    // How the deal is moving, where it is worked out (the board, a deal, the full list):
    // when it came into its stage (null when no history says so), the last time anything
    // happened to it, whether it has gone stale, and after how many quiet days it would.
    DateTime? StageEnteredAtUtc = null,
    DateTime? IdleSinceUtc = null,
    bool IsStale = false,
    int? StaleAfterDays = null,
    // The deal as of this read. An edit that sends it back is refused once anyone has
    // saved the deal since (see UpdateDealRequest.Version).
    Guid? Version = null);

/// <summary>The deal to fold into the one named in the route.</summary>
public record MergeDealsRequest(Guid OtherDealId);
