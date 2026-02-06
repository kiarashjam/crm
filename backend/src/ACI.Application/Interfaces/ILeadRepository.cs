using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ILeadRepository
{
    // Paginated methods
    Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default);
    
    // Single item methods
    Task<Lead?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // CRUD methods
    Task<Lead> AddAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> UpdateAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
