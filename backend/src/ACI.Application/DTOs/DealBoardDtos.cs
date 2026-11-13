namespace ACI.Application.DTOs;

/// <summary>
/// What the board shows: one pipeline's deals, filtered as asked, and how its cards are
/// ordered in each column. Everything left out narrows nothing.
/// </summary>
public sealed record DealBoardQuery
{
    public const int DefaultTake = 25;
    public const int MaxTake = 100;

    /// <summary>The pipeline; left out, the workspace's first. A workspace without pipelines has the default stages.</summary>
    public Guid? PipelineId { get; init; }

    /// <summary>Cards per column in the first page, 1–100.</summary>
    public int Take { get; init; } = DefaultTake;

    /// <summary>Words in the deal's name, its company's, its contact's or its owner's.</summary>
    public string? Search { get; init; }

    public Guid? AssigneeId { get; init; }

    /// <summary>Only deals nobody owns.</summary>
    public bool Unassigned { get; init; }

    /// <summary>Only the caller's deals: owned by them, or owned by nobody and created by them, as the reports count "me".</summary>
    public bool Mine { get; init; }

    /// <summary>Amounts from this, in the deal's own currency. A deal with no amount is outside any range.</summary>
    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    /// <summary>Expected to close on or after this day.</summary>
    public DateOnly? CloseFrom { get; init; }

    /// <summary>Expected to close on or before this day.</summary>
    public DateOnly? CloseTo { get; init; }

    /// <summary>Only open deals that have gone stale.</summary>
    public bool StaleOnly { get; init; }

    /// <summary>"close" (soonest expected close first, the default), "value" (largest first), "created" (newest first) or "name".</summary>
    public string? Sort { get; init; }

    /// <summary>Deals with any of these tags (all of them with <see cref="TagMatch"/> "all").</summary>
    public Guid[]? Tag { get; init; }

    public string? TagMatch { get; init; }

    /// <summary>Deals with none of these tags.</summary>
    public Guid[]? NotTag { get; init; }

    /// <summary>Custom field conditions, "key:op:value" (see FlexFilter).</summary>
    public string[]? Field { get; init; }

    /// <summary>The tag and custom field conditions, as the lists read them.</summary>
    public Common.FlexFilter Flex() => Common.FlexFilter.Parse(Tag, TagMatch, NotTag, Field);

    /// <summary>Whether anything here narrows the deals shown.</summary>
    public bool Narrows =>
        !string.IsNullOrWhiteSpace(Search) || AssigneeId != null || Unassigned || Mine || MinAmount != null || MaxAmount != null
        || CloseFrom != null || CloseTo != null || StaleOnly || Flex().Filters;
}

/// <summary>A pipeline as the board draws it: its columns with their first cards, and the figures above them.</summary>
/// <param name="PipelineId">The pipeline shown; null for a workspace without pipelines, which has the default stages.</param>
/// <param name="StaleAfterDays">Days without activity after which an open deal here is stale; 0 when deals are never flagged.</param>
/// <param name="Columns">In stage order. A last "unset" column holds deals in none of the stages, when there are any.</param>
/// <param name="Summary">The whole pipeline, whatever the filters.</param>
/// <param name="Count">The deals the filters leave, across the columns.</param>
public sealed record DealBoardDto(
    Guid? PipelineId,
    string? PipelineName,
    int StaleAfterDays,
    IReadOnlyList<DealBoardColumnDto> Columns,
    DealBoardSummaryDto Summary,
    int Count);

/// <summary>One stage on the board, with the deals the filters leave in it.</summary>
/// <param name="Key">What to ask for more of it by: the stage's id, the stage's name where the workspace has no pipelines, or "unset".</param>
/// <param name="Probability">The stage's default probability, which its deals without one of their own are weighted by.</param>
/// <param name="Totals">Every deal in the column, totalled per currency, never across them.</param>
/// <param name="Weighted">The open deals, weighted by their probability or else the stage's, per currency. Empty for a won or lost stage.</param>
/// <param name="Stale">Open deals in it nobody has touched for the stale days.</param>
/// <param name="Deals">The first page of cards, in the order asked for.</param>
/// <param name="HasMore">More cards than the page holds.</param>
public sealed record DealBoardColumnDto(
    string Key,
    Guid? DealStageId,
    string Name,
    int DisplayOrder,
    bool IsWon,
    bool IsLost,
    int? Probability,
    int Count,
    IReadOnlyList<MoneyRowDto> Totals,
    IReadOnlyList<WeightedRowDto> Weighted,
    int Stale,
    IReadOnlyList<DealDto> Deals,
    bool HasMore);

/// <summary>A pipeline's figures, all of its deals counted, whatever the filters.</summary>
/// <param name="Open">Deals still open: neither marked won or lost nor in a won or lost stage.</param>
/// <param name="OpenTotals">The open pipeline per currency.</param>
/// <param name="Weighted">The open pipeline weighted by probability, per currency.</param>
/// <param name="WinRate">Won out of all closed, 0–1; null before anything has closed.</param>
/// <param name="Overdue">Open deals expected to close before today, on the workspace's calendar.</param>
/// <param name="ClosingThisWeek">Open deals expected to close today or in the next seven days.</param>
public sealed record DealBoardSummaryDto(
    int Total,
    int Open,
    int Won,
    int Lost,
    IReadOnlyList<MoneyRowDto> OpenTotals,
    IReadOnlyList<MoneyRowDto> WonTotals,
    IReadOnlyList<WeightedRowDto> Weighted,
    double? WinRate,
    int Overdue,
    int ClosingThisWeek,
    int Stale);

/// <summary>More cards of one column: the next page, in the board's order.</summary>
public sealed record DealBoardColumnPageDto(string Key, IReadOnlyList<DealDto> Deals, bool HasMore);
