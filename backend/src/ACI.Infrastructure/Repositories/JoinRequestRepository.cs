using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class JoinRequestRepository : IJoinRequestRepository
{
    private readonly AppDbContext _db;

    public JoinRequestRepository(AppDbContext db) => _db = db;

    public async Task<JoinRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.JoinRequests
            .Include(j => j.Organization)
            .Include(j => j.User)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IReadOnlyList<JoinRequest>> ListPendingByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.JoinRequests
            .Include(j => j.User)
            .Where(j => j.OrganizationId == organizationId && j.Status == JoinRequestStatus.Pending)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<JoinRequest> AddAsync(JoinRequest joinRequest, CancellationToken ct = default)
    {
        _db.JoinRequests.Add(joinRequest);
        await _db.SaveChangesAsync(ct);
        return joinRequest;
    }

    public async Task UpdateAsync(JoinRequest joinRequest, CancellationToken ct = default)
    {
        _db.JoinRequests.Update(joinRequest);
        await _db.SaveChangesAsync(ct);
    }
}
