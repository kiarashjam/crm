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
        var existing = await _db.ClubDocuments.FirstOrDefaultAsync(d => d.Id == document.Id, ct);
        if (existing == null) _db.ClubDocuments.Add(document);
        else if (existing.OrganizationId != document.OrganizationId || existing.Kind != document.Kind)
            throw new InvalidOperationException("A club document can only be saved over one of the same kind in the same workspace.");
        else
        {
            existing.Payload = document.Payload;
            existing.UpdatedAtUtc = document.UpdatedAtUtc;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default)
    {
        var row = await GetAsync(id, organizationId, kind, ct);
        if (row == null) return false;
        _db.ClubDocuments.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
