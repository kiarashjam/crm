namespace ACI.Application.DTOs;

/// <summary>
/// The dashboard's headline figures.
/// </summary>
/// <param name="PipelineValue">
/// Open pipeline in <paramref name="PipelineCurrency"/> ONLY. It used to be the
/// sum of every open deal regardless of currency, printed by the client with a
/// dollar sign — so a CHF deal and a EUR deal were added together and labelled
/// in a third currency.
/// </param>
/// <param name="PipelineCurrency">
/// The currency carrying the largest share of the open pipeline. Ties break
/// alphabetically so the figure does not flip between requests.
/// </param>
/// <param name="PipelineByCurrency">Every currency present, largest first.</param>
/// <param name="UnreadableValueCount">
/// Open deals whose value column holds no readable number. Reported rather than
/// counted as zero: "we could not read 3 deals" and "3 deals are worth nothing"
/// are different facts, and only one of them is a data-entry problem.
/// </param>
public record DashboardStatsDto(
    int ActiveLeadsCount,
    int ActiveDealsCount,
    decimal PipelineValue,
    string PipelineCurrency,
    IReadOnlyList<CurrencyTotalDto> PipelineByCurrency,
    int UnreadableValueCount,
    int DealsWonCount,
    int DealsLostCount
);
