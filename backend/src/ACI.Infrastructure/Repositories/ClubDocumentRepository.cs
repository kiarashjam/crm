using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ClubDocumentRepository : IClubDocumentRepository
{
    private readonly AppDbContext _db;
    public ClubDocumentRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClubDocument>> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default) =>
        await _db.ClubDocuments.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Kind == kind)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(ct);

    public Task<ClubDocument?> GetAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default) =>
        _db.ClubDocuments.FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organizationId && d.Kind == kind, ct);

    public async Task SaveAsync(ClubDocument document, CancellationToken ct = default)
    {
        // A row this request already read comes back as the tracked copy, still holding
        // the version it was read at, so the save below is checked against that read.
        var existing = await _db.ClubDocuments.FirstOrDefaultAsync(d => d.Id == document.Id, ct);
        if (existing == null) _db.ClubDocuments.Add(document);
        else if (existing.OrganizationId != document.OrganizationId || existing.Kind != document.Kind)
            throw new InvalidOperationException("A club document can only be saved over one of the same kind in the same workspace.");
        else
        {
            existing.Payload = document.Payload;
            existing.UpdatedAtUtc = document.UpdatedAtUtc;
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Forget the stale copy, so nothing later in this request saves it again.
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default)
    {
        var row = await GetAsync(id, organizationId, kind, ct);
        if (row == null) return false;
        _db.ClubDocuments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct = default)
    {
        // The in-memory provider the tests use has no transactions. The work is written
        // so its first save is the one that can lose a race, so a refusal there still
        // leaves nothing behind.
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
            return await work();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var result = await work();
        if (result.IsSuccess) await transaction.CommitAsync(ct);
        else await transaction.RollbackAsync(ct);
        return result;
    }
}
