using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IContactRepository
{
    // Paginated methods
    Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        bool includeArchived = false, 
        CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, bool includeArchived = false, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool includeArchived = false, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default);
    
    // Single item methods
    Task<Contact?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Contact?> GetByEmailAsync(string email, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? organizationId, Guid? excludeContactId = null, CancellationToken ct = default);
    
    // CRUD methods
    Task<Contact> AddAsync(Contact contact, CancellationToken ct = default);
    Task<Contact?> UpdateAsync(Contact contact, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> UnarchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
