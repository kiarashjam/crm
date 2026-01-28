using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DealRepository : IDealRepository
{
    private readonly AppDbContext _db;

    public DealRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Deal>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Deals.Where(d => d.UserId == userId).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await _db.Deals
            .Where(d => d.UserId == userId && (d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q)))
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task<Deal?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Deals.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);

    public async Task<Deal> AddAsync(Deal deal, CancellationToken ct = default)
    {
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        return deal;
    }
}
