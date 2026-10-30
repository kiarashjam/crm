namespace ACI.Application.DTOs;

/// <summary>
/// Server-computed statistics for a single company (HP-7).
/// </summary>
/// <remarks>
/// The value of the company's deals is one row per currency, largest first. It was one
/// number, every deal's text stripped to digits and added up, so CHF 40,000 and EUR
/// 30,000 came out as "$70K".
/// </remarks>
public record CompanyStatsItemDto(
    Guid CompanyId,
    int ContactCount,
    int DealCount,
    IReadOnlyList<CurrencyTotalDto> DealTotals);

/// <summary>
/// Batch response containing stats for multiple companies.
/// </summary>
public record CompanyStatsBatchDto(
    IReadOnlyList<CompanyStatsItemDto> Items);
