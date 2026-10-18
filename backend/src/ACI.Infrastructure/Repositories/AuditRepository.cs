using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AuditEvent>> GetAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid? entityId, int take, CancellationToken ct = default)
    {
        var query = _db.AuditEvents.AsNoTracking();
        query = organizationId is { } orgId
            ? query.Where(e => e.OrganizationId == orgId)
            : query.Where(e => e.OrganizationId == null && e.OwnerUserId == userId);
        if (entityType != null) query = query.Where(e => e.EntityType == entityType);
        if (entityId != null) query = query.Where(e => e.EntityId == entityId);
        return await query.OrderByDescending(e => e.CreatedAtUtc).Take(take).ToListAsync(ct);
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _db.AuditEvents.Add(auditEvent);
        await _db.SaveChangesAsync(ct);
    }
}
