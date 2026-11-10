namespace ACI.Application.DTOs;

/// <summary>Whose records a report covers.</summary>
public enum ReportScope
{
    /// <summary>Everything in the workspace the caller can see.</summary>
    Organization,
    /// <summary>A sales team's: deals and leads its members own, activities they logged.</summary>
    Team,
    /// <summary>The caller's own.</summary>
    Me,
}

/// <summary>What a report is narrowed to.</summary>
/// <remarks>
/// The period is two instants rather than two dates. The browser knows where its
/// midnight is and the server does not (there is no organisation time zone yet), so
/// the client sends its own local midnights and the server counts between them.
/// </remarks>
public sealed record ReportFilter
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public ReportScope Scope { get; init; } = ReportScope.Organization;
    /// <summary>With <see cref="ReportScope.Team"/>: which team. Null means the caller's own.</summary>
    public Guid? TeamId { get; init; }
    /// <summary>Deals in this pipeline only. Leads and activities belong to no pipeline, so it does not narrow them.</summary>
    public Guid? PipelineId { get; init; }
    /// <summary>Deals in this currency only.</summary>
    public string? Currency { get; init; }
    /// <summary>IANA zone the day-by-day charts are counted in, e.g. "Europe/Zurich". The workspace's when absent or unknown.</summary>
    public string? TimeZone { get; init; }
}

/// <summary>The forecast window and the same narrowing as a report.</summary>
public sealed record ForecastFilter
{
    /// <summary>First month shown, as its first day (UTC). Null means the month it is now on the workspace's calendar.</summary>
    public DateTime? FirstMonthUtc { get; init; }
    public int Months { get; init; } = 6;
    public ReportScope Scope { get; init; } = ReportScope.Organization;
    public Guid? TeamId { get; init; }
    public Guid? PipelineId { get; init; }
    public string? Currency { get; init; }
}

/// <summary>What the report was actually computed for, after defaults were filled in.</summary>
/// <param name="Bucket">How the time series are grouped: "day", "week" or "month", chosen from the length of the period.</param>
public sealed record ReportPeriodDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string Scope,
    Guid? TeamId,
    string? TeamName,
    Guid? PipelineId,
    string? Currency,
    string TimeZone,
    string Bucket);

/// <summary>Deals in one currency: how many, what they add up to, and how many had no readable value.</summary>
/// <param name="Unreadable">Counted in <paramref name="Count"/> but not in <paramref name="Value"/>.</param>
public sealed record MoneyRowDto(string Currency, int Count, decimal Value, int Unreadable);

/// <summary>A set of deals: the count across currencies, and the money per currency, never added across them.</summary>
public sealed record DealFiguresDto(int Count, IReadOnlyList<MoneyRowDto> ByCurrency);

/// <summary>The probability-weighted value of open deals in one currency.</summary>
/// <param name="Covered">Deals with both a probability and a readable value: the ones in <paramref name="Value"/>.</param>
/// <param name="Missing">Open deals with no probability, their own or their stage's, left out rather than given a made-up one.</param>
public sealed record WeightedRowDto(string Currency, decimal Value, int Covered, int Missing);

/// <summary>Open deals at one stage, in one currency.</summary>
/// <param name="StageKey">The stage's id, or for a deal with only a stage name, that name.</param>
/// <param name="DisplayOrder">The stage's place in its pipeline, so the rows read as a funnel.</param>
public sealed record StageRowDto(
    Guid? PipelineId,
    string PipelineName,
    string StageKey,
    string StageName,
    int DisplayOrder,
    string Currency,
    int Count,
    decimal Value,
    decimal Weighted,
    int Unreadable);

/// <summary>One owner's deals in one currency: open now, and won and lost in the period.</summary>
public sealed record OwnerRowDto(
    Guid? OwnerId,
    string OwnerName,
    string Currency,
    int OpenCount,
    decimal OpenValue,
    decimal Weighted,
    int WonCount,
    decimal WonValue,
    int LostCount);

public sealed record CountRowDto(string Key, int Count);

/// <summary>Leads created in the period with this status today, and how many of them were converted.</summary>
public sealed record LeadStatusRowDto(string Status, int Count, int Converted);

/// <summary>One bucket of a time series.</summary>
/// <param name="Start">The bucket's first day, as yyyy-MM-dd in the report's time zone.</param>
public sealed record SeriesPointDto(string Start, int Count);

/// <param name="Active">Leads not yet converted, now. The Leads page's "Active" counts the same.</param>
/// <param name="Created">Leads created in the period: the cohort the breakdowns describe.</param>
/// <param name="CreatedConverted">Of those, how many have been converted since.</param>
/// <param name="Converted">Leads converted in the period, whenever they were created.</param>
/// <param name="ConvertedToDeal">Of those, how many became a deal rather than only a contact.</param>
/// <param name="Scored">Leads in the cohort with a score; <paramref name="AverageScore"/> is over these only.</param>
public sealed record LeadFiguresDto(
    int Active,
    int Created,
    int CreatedConverted,
    int Converted,
    int ConvertedToDeal,
    int Scored,
    double? AverageScore,
    IReadOnlyList<LeadStatusRowDto> ByStatus,
    IReadOnlyList<CountRowDto> BySource,
    IReadOnlyList<SeriesPointDto> CreatedSeries);

/// <summary>Activities people logged in the period. The app's own "system" lines are not work anyone did, so they are left out.</summary>
public sealed record ActivityFiguresDto(int Total, IReadOnlyList<CountRowDto> ByType, IReadOnlyList<SeriesPointDto> Series);

/// <summary>Every figure on the Reports page, computed on the server for one filter.</summary>
/// <param name="Currencies">Every currency the scope's deals are in, whatever the currency filter, so the picker can offer them.</param>
/// <param name="HeadlineCurrency">
/// The currency the page leads with: the filter's, or else the one holding most of the
/// open pipeline. Picked from open deals, not from all of them, so years of closed
/// deals in one currency cannot hide a live pipeline in another.
/// </param>
/// <param name="Open">Deals open now. Not limited to the period: open is a state, not an event.</param>
/// <param name="ClosingInPeriod">Open deals whose expected close date falls in the period.</param>
/// <param name="WinRate">Won ÷ (won + lost) among deals closed in the period, by count; null when none closed.</param>
/// <param name="AverageSalesCycleDays">From creation to win, over deals won in the period; null when none were.</param>
public sealed record ReportSummaryDto(
    ReportPeriodDto Period,
    IReadOnlyList<string> Currencies,
    string? HeadlineCurrency,
    DealFiguresDto Open,
    IReadOnlyList<WeightedRowDto> Weighted,
    DealFiguresDto ClosingInPeriod,
    DealFiguresDto Created,
    DealFiguresDto Won,
    DealFiguresDto Lost,
    double? WinRate,
    double? AverageSalesCycleDays,
    IReadOnlyList<StageRowDto> ByStage,
    IReadOnlyList<OwnerRowDto> ByOwner,
    LeadFiguresDto Leads,
    ActivityFiguresDto Activities);

/// <summary>Open deals expected to close in one month, in one currency, and what was won that month.</summary>
/// <param name="Month">yyyy-MM, by each deal's expected close date.</param>
/// <param name="Covered">Deals with a probability and a readable value: the ones in <paramref name="Weighted"/>.</param>
/// <param name="Missing">Deals with no probability.</param>
/// <param name="WonCount">Deals won that month (by the day they were closed, UTC), to set against the forecast.</param>
public sealed record ForecastMonthRowDto(
    string Month,
    string Currency,
    int Count,
    decimal Value,
    decimal Weighted,
    int Covered,
    int Missing,
    int Unreadable,
    int WonCount,
    decimal WonValue);

/// <param name="Overdue">Open deals whose expected close date has passed. They are in no month's forecast after their own.</param>
/// <param name="NoCloseDate">Open deals with no expected close date, so in no month's forecast.</param>
public sealed record ForecastDto(
    string FirstMonth,
    int Months,
    string Scope,
    Guid? TeamId,
    IReadOnlyList<ForecastMonthRowDto> Rows,
    IReadOnlyList<MoneyRowDto> Overdue,
    IReadOnlyList<MoneyRowDto> NoCloseDate);

/// <summary>One save of a month's forecast. Saves are kept; a later one never replaces an earlier one.</summary>
/// <param name="ByCloseDate">
/// False for saves made before forecasts went by close date: those counted every open
/// deal, whatever month it was expected to close in.
/// </param>
public sealed record ForecastSaveDto(Guid SnapshotId, DateTime SavedAtUtc, string SavedByName, bool ByCloseDate);

/// <summary>What one owner won in the month, in one currency: the outcome a saved forecast is set against.</summary>
public sealed record ForecastActualDto(string AssigneeName, string Currency, int WonCount, decimal WonValue);

/// <summary>A month's saved forecasts: every save, the lines of one of them, and what was actually won.</summary>
/// <param name="SnapshotId">The save <paramref name="Rows"/> belong to: the one asked for, or the latest.</param>
public sealed record ForecastSnapshotSetDto(
    string Month,
    IReadOnlyList<ForecastSaveDto> Saves,
    Guid? SnapshotId,
    IReadOnlyList<ForecastSnapshotDto> Rows,
    IReadOnlyList<ForecastActualDto> Actual);
