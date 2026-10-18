using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IDealLineItemRepository
{
    /// <summary>A deal's line items in the order they were added. The caller has checked the deal is theirs.</summary>
    Task<IReadOnlyList<DealLineItem>> ListAsync(Guid dealId, CancellationToken ct = default);

    /// <summary>A line item whose deal is in the caller's workspace.</summary>
    Task<DealLineItem?> FindAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task AddAsync(DealLineItem item, CancellationToken ct = default);

    /// <summary>Persist changes made to an item loaded by <see cref="FindAsync"/>.</summary>
    Task SaveAsync(DealLineItem item, CancellationToken ct = default);

    Task DeleteAsync(DealLineItem item, CancellationToken ct = default);
}
