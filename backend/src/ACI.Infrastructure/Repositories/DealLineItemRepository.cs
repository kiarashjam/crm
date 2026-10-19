using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DealLineItemRepository : IDealLineItemRepository
{
    private readonly AppDbContext _db;

    public DealLineItemRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DealLineItem>> ListAsync(Guid dealId, CancellationToken ct = default) =>
        await _db.DealLineItems.AsNoTracking()
            .Where(i => i.DealId == dealId)
            .OrderBy(i => i.CreatedAtUtc)
            .ToListAsync(ct);

    // Through the deal, by the same rule as the deal repository, so a line item is
    // exactly as reachable as the deal it belongs to — private deals included.
    public Task<DealLineItem?> FindAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _db.DealLineItems.FirstOrDefaultAsync(i => i.Id == id
            && _db.Deals.VisibleTo(_db, userId, organizationId).Any(d => d.Id == i.DealId), ct);

    public async Task AddAsync(DealLineItem item, CancellationToken ct = default)
    {
        _db.DealLineItems.Add(item);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(DealLineItem item, CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(DealLineItem item, CancellationToken ct = default)
    {
        _db.DealLineItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
