using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ConsentRepository : IConsentRepository
{
    private readonly AppDbContext _db;

    public ConsentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConsentRecord>> CurrentAsync(
        Guid organizationId, string subjectType, IReadOnlyCollection<Guid> subjectIds,
        string? channel = null, string? purpose = null, CancellationToken ct = default)
    {
        if (subjectIds.Count == 0) return [];
        var query = _db.ConsentRecords.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.SubjectType == subjectType && c.IsCurrent && subjectIds.Contains(c.SubjectId));
        if (channel != null) query = query.Where(c => c.Channel == channel);
        if (purpose != null) query = query.Where(c => c.Purpose == purpose);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConsentRecord>> HistoryAsync(Guid organizationId, string subjectType, Guid subjectId, int take, CancellationToken ct = default) =>
        await _db.ConsentRecords.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.SubjectType == subjectType && c.SubjectId == subjectId)
            .OrderByDescending(c => c.RecordedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<bool> AddCurrentAsync(ConsentRecord record, CancellationToken ct = default)
    {
        var previous = await _db.ConsentRecords
            .Where(c => c.OrganizationId == record.OrganizationId && c.SubjectType == record.SubjectType && c.SubjectId == record.SubjectId
                && c.Channel == record.Channel && c.Purpose == record.Purpose && c.IsCurrent)
            .ToListAsync(ct);
        foreach (var row in previous) row.IsCurrent = false;
        record.IsCurrent = true;
        _db.ConsentRecords.Add(record);
        try
        {
            // One save: the old answer stops standing exactly when the new one starts.
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another answer for the same person, channel and purpose was saved first, and
            // the unique index on the standing answer refused this one. Nothing of it is kept.
            _db.Entry(record).State = EntityState.Detached;
            foreach (var row in previous) await _db.Entry(row).ReloadAsync(ct);
            return false;
        }
    }
}
