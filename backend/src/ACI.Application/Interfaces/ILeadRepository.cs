using ACI.Application.Common;
using ACI.Application.DTOs;
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
        LeadQueryOptions? options = null,
        CancellationToken ct = default);

    Task<LeadStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null);
    
    // Single item methods
    Task<Lead?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // CRUD methods
    Task<Lead> AddAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> UpdateAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Lead?> AssignAsync(Guid id, Guid userId, Guid? organizationId, Guid? assignedToUserId, CancellationToken ct = default);

    /// <summary>Records (or clears, with null) the contact who referred a lead. The contact is checked by the caller.</summary>
    Task<Lead?> SetReferralAsync(Guid id, Guid userId, Guid? organizationId, Guid? referredByContactId, CancellationToken ct = default);

    /// <summary>Leads in this workspace that a contact referred, newest first.</summary>
    Task<IReadOnlyList<Lead>> GetReferredByAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Folds <paramref name="dropId"/> into <paramref name="keepId"/>: its tasks, activities,
    /// history, messages, contracts, files and custom field values move to the lead kept,
    /// the kept lead's empty fields are filled from it, and it is deleted. False when
    /// either lead is not in this workspace, or they are the same lead.
    /// </summary>
    Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
