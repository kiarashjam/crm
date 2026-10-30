namespace ACI.Application.DTOs;

/// <summary>One owner's line, in one currency, of a saved forecast.</summary>
/// <param name="WeightedValue">Weighted value of the open deals expected to close in <paramref name="Month"/>.</param>
/// <param name="OpenValue">Their full value.</param>
/// <param name="Covered">Of those deals, how many had a probability (and so are in the weighted value).</param>
/// <param name="Missing">How many had none.</param>
/// <param name="SnapshotId">The save this line belongs to; every line of one save shares it.</param>
/// <param name="ByCloseDate">False on lines saved before forecasts went by close date, which counted every open deal.</param>
/// <param name="OverdueCount">Open deals whose expected close date was before the month, not in the figures above.</param>
/// <param name="NoCloseDateCount">Open deals with no expected close date, not in the figures above.</param>
/// <param name="UnreadableCount">Deals expected that month whose value could not be read, so in no total.</param>
public record ForecastSnapshotDto(
    Guid Id,
    string Month,
    string Currency,
    string AssigneeName,
    decimal WeightedValue,
    decimal OpenValue,
    int Covered,
    int Missing,
    DateTime SavedAtUtc,
    Guid SnapshotId,
    bool ByCloseDate,
    int OverdueCount,
    decimal OverdueValue,
    int NoCloseDateCount,
    int UnreadableCount);
