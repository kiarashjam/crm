using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICompanyRepository
{
    // Paginated methods
    Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Company>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default);
    
    // Single item methods
    Task<Company?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // Existence check (HP-HIGH-2: efficient duplicate detection)
    Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? organizationId, CancellationToken ct = default);

    // CRUD methods
    Task<Company> AddAsync(Company company, CancellationToken ct = default);
    Task<Company?> UpdateAsync(Company company, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    // HP-7: Server-side stats (contact count, deal count, deal value per company)
    Task<IReadOnlyList<(Guid CompanyId, int ContactCount, int DealCount, decimal TotalDealValue)>> GetStatsAsync(
        Guid userId, Guid? organizationId, CancellationToken ct = default);
}
