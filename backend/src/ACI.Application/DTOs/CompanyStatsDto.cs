namespace ACI.Application.DTOs;

/// <summary>
/// Server-computed statistics for a single company (HP-7).
/// </summary>
public record CompanyStatsItemDto(
    Guid CompanyId,
    int ContactCount,
    int DealCount,
    decimal TotalDealValue);

/// <summary>
/// Batch response containing stats for multiple companies.
/// </summary>
public record CompanyStatsBatchDto(
    IReadOnlyList<CompanyStatsItemDto> Items);
