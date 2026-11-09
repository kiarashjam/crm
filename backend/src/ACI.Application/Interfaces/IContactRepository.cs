using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IContactRepository
{
    // Paginated methods
    /// <summary>One page of the workspace's contacts, filtered and sorted as asked, with the total that match.</summary>
    Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        ContactQueryOptions options,
        CancellationToken ct = default);

    /// <summary>The figures above the contacts list, over the whole workspace.</summary>
    Task<ContactStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Every contact the list would show with these filters, in its order, at most
    /// <paramref name="max"/> of them, and how many match in all.
    /// </summary>
    Task<(IReadOnlyList<ContactExportRow> Rows, int Total)> GetExportRowsAsync(
        Guid userId, Guid? organizationId, ContactQueryOptions options, int max, CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, bool includeArchived = false, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool includeArchived = false, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default, int? take = null);
    
    // Single item methods
    Task<Contact?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Contact?> GetByEmailAsync(string email, Guid userId, Guid? organizationId, CancellationToken ct = default);
    /// <summary>Whether an active contact in the workspace (the organization's, or with none, the user's own) has this email.</summary>
    Task<bool> EmailExistsAsync(string email, Guid userId, Guid? organizationId, Guid? excludeContactId = null, CancellationToken ct = default);
    
    // CRUD methods
    Task<Contact> AddAsync(Contact contact, CancellationToken ct = default);
    Task<Contact?> UpdateAsync(Contact contact, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> UnarchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates the person contact for a lead when one does not exist yet, unless someone
    /// deleted the lead's contact (<see cref="Lead.PersonContactRemovedAtUtc"/>). A write:
    /// it is called when a lead is saved, never to read one.
    /// </summary>
    Task EnsurePersonContactAsync(Lead lead, CancellationToken ct = default);

    /// <summary>
    /// Gives person contacts to leads in every workspace that should have one and do not
    /// (older leads, or ones a tool wrote straight to the database), at most
    /// <paramref name="maxLeads"/> leads a run. Returns how many leads were given one.
    /// </summary>
    Task<int> BackfillPersonContactsAsync(int maxLeads, CancellationToken ct = default);

    /// <summary>
    /// The lead's person contact in the workspace, archived or not; when none is linked, the
    /// active contact with the lead's email that a save would link it to; or null. Reads only.
    /// </summary>
    Task<Contact?> GetPersonContactAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default);

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

/// <summary>One contact as the export writes it.</summary>
public sealed record ContactExportRow(
    string Name,
    string Email,
    string? Phone,
    string? JobTitle,
    string? CompanyName,
    bool DoNotContact,
    bool IsArchived,
    DateTime? LastActivityAtUtc,
    DateTime CreatedAtUtc);
