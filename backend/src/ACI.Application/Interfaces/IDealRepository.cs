using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

// IDealRepository.cs

public interface IDealRepository
{
    // Paginated methods
    Task<(IReadOnlyList<Deal> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        Guid? companyId = null,
        Guid? contactId = null,
        CancellationToken ct = default);
    
    Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default);
    
    // Non-paginated methods (for backward compatibility)
    Task<IReadOnlyList<Deal>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null);
    
    // Single item methods
    Task<Deal?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // CRUD methods
    Task<Deal> AddAsync(Deal deal, CancellationToken ct = default);
    Task<Deal?> UpdateAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Folds <paramref name="dropId"/> into <paramref name="keepId"/>: its tasks, activities,
    /// stage history, line items, messages, contracts, files and custom field values move
    /// to the deal kept, the kept deal's empty fields are filled from it, and it is deleted.
    /// False when either deal is not one this person can see, or they are the same deal.
    /// </summary>
    Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    
    // Stage change history (HP-11)
    Task AddStageChangeAsync(DealStageChange change, CancellationToken ct = default);
    Task<IReadOnlyList<DealStageChange>> GetStageChangesAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
