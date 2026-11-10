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

/// <summary>
/// A company in the list, with its people and deals counted on the server: active contacts,
/// and the deals the caller may see, totalled per currency.
/// </summary>
public record CompanyListItemDto(
    Guid Id,
    string Name,
    string? Domain,
    string? Industry,
    string? Size,
    string? Description,
    string? Website,
    string? Location,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int ContactCount,
    int DealCount,
    IReadOnlyList<CurrencyTotalDto> DealTotals,
    Guid? Version = null)
    : CompanyDto(Id, Name, Domain, Industry, Size, Description, Website, Location, CreatedAtUtc, UpdatedAtUtc, Version);

/// <summary>
/// The figures above the companies list, over the whole workspace. Most were worked out in
/// the browser from the twenty companies on screen.
/// </summary>
/// <param name="DealTotals">Every visible deal at the workspace's companies, one total per currency, largest first.</param>
/// <param name="Industries">The industries the workspace's companies are in, as typed, most companies first.</param>
public record CompanySummaryDto(
    int Total,
    int WithContacts,
    int WithDeals,
    int AddedLast7Days,
    IReadOnlyList<CurrencyTotalDto> DealTotals,
    IReadOnlyList<IndustryCountDto> Industries,
    int WithoutIndustry);

public record IndustryCountDto(string Industry, int Count);
