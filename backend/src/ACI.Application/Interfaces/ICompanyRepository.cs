using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICompanyRepository
{
    // Paginated methods
    /// <summary>One page of the workspace's companies, filtered and sorted as asked, with the total that match.</summary>
    Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        Common.CompanyQueryOptions options,
        CancellationToken ct = default);

    /// <summary>The figures above the companies list, over the whole workspace.</summary>
    Task<DTOs.CompanySummaryDto> GetSummaryAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Every company the list would show with these filters, in its order, at most <paramref name="max"/>, and how many match.</summary>
    Task<(IReadOnlyList<Company> Rows, int Total)> GetExportRowsAsync(
        Guid userId, Guid? organizationId, Common.CompanyQueryOptions options, int max, CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Company>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null);
    
    // Single item methods
    Task<Company?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // Existence check (HP-HIGH-2: efficient duplicate detection)
    Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? organizationId, CancellationToken ct = default);

    // CRUD methods
    Task<Company> AddAsync(Company company, CancellationToken ct = default);
    Task<Company?> UpdateAsync(Company company, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Moves every record that pointed at <paramref name="dropId"/> onto <paramref name="keepId"/>,
    /// fills blank fields on the company that is kept, and deletes the other company.
    /// </summary>
    Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    // HP-7: Server-side stats (contact count, deal count, deal value per company and currency)
    Task<IReadOnlyList<(Guid CompanyId, int ContactCount, int DealCount, IReadOnlyList<DTOs.CurrencyTotalDto> DealTotals)>> GetStatsAsync(
        Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The same figures for these companies only, such as one page of the list.</summary>
    Task<IReadOnlyList<(Guid CompanyId, int ContactCount, int DealCount, IReadOnlyList<DTOs.CurrencyTotalDto> DealTotals)>> GetStatsAsync(
        IReadOnlyCollection<Guid> companyIds, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
