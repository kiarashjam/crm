using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CopyHistoryRepository : ICopyHistoryRepository
{
    private readonly AppDbContext _db;

    public CopyHistoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CopyHistoryItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.CopyHistoryItems
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<CopyHistoryItem> AddAsync(CopyHistoryItem item, CancellationToken ct = default)
    {
        _db.CopyHistoryItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.CopyHistoryItems.CountAsync(h => h.UserId == userId, ct);

    public async Task<int> CountSentThisWeekAsync(Guid userId, CancellationToken ct = default)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        return await _db.CopyHistoryItems.CountAsync(h => h.UserId == userId && h.CreatedAtUtc >= weekAgo, ct);
    }
}
