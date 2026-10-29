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

    /// <summary>
    /// The first of these addresses that belongs to a do-not-contact contact in the
    /// workspace (archived ones included), or null when none does.
    /// </summary>
    Task<string?> FindDoNotContactAddressAsync(Guid organizationId, IReadOnlyCollection<string> addresses, CancellationToken ct = default);

    /// <summary>
    /// Whether this lead or contact of the workspace is do not contact: a contact by
    /// its own flag or its address, a lead through its person contact or its address.
    /// </summary>
    Task<bool> IsDoNotContactAsync(Guid organizationId, Guid? leadId, Guid? contactId, CancellationToken ct = default);

    /// <summary>
    /// Someone at this address asked the workspace to stop: every contact with it,
    /// and the person contact of every lead with it (made first when missing), is
    /// marked do not contact. With no record at all — a Cc on a composed email, say —
    /// a contact is made for the address, owned by <paramref name="ownerUserId"/>, so
    /// that the next email sees the "stop".
    /// </summary>
    Task<DoNotContactMarked> MarkDoNotContactAsync(Guid organizationId, string email, Guid ownerUserId, CancellationToken ct = default);
}

/// <summary>What an unsubscribe touched, so the outreach still scheduled to them can be stopped.</summary>
public sealed record DoNotContactMarked(IReadOnlyList<Guid> ContactIds, IReadOnlyList<Guid> LeadIds);
