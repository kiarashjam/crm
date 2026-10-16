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
        Guid? companyId = null, 
        CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, bool includeArchived = false, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool includeArchived = false, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default, int? take = null);
    
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

    /// <summary>Creates the person contact for a lead when one does not exist yet.</summary>
    Task EnsurePersonContactAsync(Lead lead, CancellationToken ct = default);

    /// <summary>Creates missing person contacts for leads in this organization.</summary>
    Task EnsureMissingPersonContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<IReadOnlyList<Contact>> FindLookalikesAsync(Contact contact, CancellationToken ct = default);

    Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
