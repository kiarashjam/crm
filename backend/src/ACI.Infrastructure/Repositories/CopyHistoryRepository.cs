using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CopyHistoryRepository : ICopyHistoryRepository
{
    private readonly AppDbContext _db;

    public CopyHistoryRepository(AppDbContext db) => _db = db;

    private static IQueryable<CopyHistoryItem> FilterByUserAndOrg(IQueryable<CopyHistoryItem> q, Guid userId, Guid? organizationId) =>
        q.Where(h => h.UserId == userId && (organizationId == null ? h.OrganizationId == null : h.OrganizationId == organizationId));

    public async Task<IReadOnlyList<CopyHistoryItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.CopyHistoryItems
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CopyHistoryItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.CopyHistoryItems, userId, organizationId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<CopyHistoryItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.CopyHistoryItems.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<CopyHistoryItem> AddAsync(CopyHistoryItem item, CancellationToken ct = default)
    {
        _db.CopyHistoryItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<CopyHistoryItem> UpdateAsync(CopyHistoryItem item, CancellationToken ct = default)
    {
        _db.CopyHistoryItems.Update(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<int> CountByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.CopyHistoryItems, userId, organizationId).CountAsync(ct);

    public async Task<int> CountSentThisWeekAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        return await FilterByUserAndOrg(_db.CopyHistoryItems, userId, organizationId)
            .CountAsync(h => h.CreatedAtUtc >= weekAgo, ct);
    }
}
