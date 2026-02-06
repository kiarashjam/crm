using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class InviteRepository : IInviteRepository
{
    private readonly AppDbContext _db;

    public InviteRepository(AppDbContext db) => _db = db;

    public async Task<Invite?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        await _db.Invites
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == token, ct);

    public async Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Invites.Include(i => i.Organization).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<Invite>> ListPendingByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Invites
            .Where(i => i.OrganizationId == organizationId && i.AcceptedByUserId == null && i.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Invite>> ListPendingByEmailAsync(string email, CancellationToken ct = default) =>
        await _db.Invites
            .Include(i => i.Organization)
            .Where(i => i.Email == email && i.AcceptedByUserId == null && i.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Invite> AddAsync(Invite invite, CancellationToken ct = default)
    {
        _db.Invites.Add(invite);
        await _db.SaveChangesAsync(ct);
        return invite;
    }

    public async Task UpdateAsync(Invite invite, CancellationToken ct = default)
    {
        _db.Invites.Update(invite);
        await _db.SaveChangesAsync(ct);
    }
}
