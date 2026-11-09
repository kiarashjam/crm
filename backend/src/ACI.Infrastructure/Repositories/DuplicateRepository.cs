using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DuplicateRepository : IDuplicateRepository
{
    private readonly AppDbContext _db;

    public DuplicateRepository(AppDbContext db) => _db = db;

    private static IQueryable<T> InWorkspace<T>(IQueryable<T> q, Guid userId, Guid? organizationId) where T : class =>
        organizationId == null
            ? q.Where(e => EF.Property<Guid>(e, "UserId") == userId && EF.Property<Guid?>(e, "OrganizationId") == null)
            : q.Where(e => EF.Property<Guid?>(e, "OrganizationId") == organizationId);

    public async Task<IReadOnlyList<Contact>> ContactCandidatesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Only the people who share a key with someone else, found by the database (the
        // keys are indexed), so a workspace of thousands sends a handful of rows here.
        var active = InWorkspace(_db.Contacts.AsNoTracking(), userId, organizationId).Where(c => !c.IsArchived);
        var emails = active.GroupBy(c => c.Email.Trim().ToLower()).Where(g => g.Count() > 1).Select(g => g.Key);
        var phones = active.Where(c => c.PhoneKey != null).GroupBy(c => c.PhoneKey).Where(g => g.Count() > 1).Select(g => g.Key);
        var names = active.Where(c => c.NameKey != null && c.NameKey != "").GroupBy(c => c.NameKey).Where(g => g.Count() > 1).Select(g => g.Key);
        return await active
            .Where(c => emails.Contains(c.Email.Trim().ToLower()) || phones.Contains(c.PhoneKey) || names.Contains(c.NameKey))
            .Include(c => c.Company)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Company>> CompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await InWorkspace(_db.Companies.AsNoTracking(), userId, organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<DuplicateDecision>> DecisionsAsync(string kind, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await InWorkspace(_db.DuplicateDecisions.AsNoTracking(), userId, organizationId)
            .Where(d => d.Kind == kind)
            .ToListAsync(ct);

    public async Task<int?> CountInWorkspaceAsync(string kind, IReadOnlyCollection<Guid> ids, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        kind switch
        {
            DuplicateKinds.Contacts => await InWorkspace(_db.Contacts, userId, organizationId).CountAsync(c => ids.Contains(c.Id), ct),
            DuplicateKinds.Companies => await InWorkspace(_db.Companies, userId, organizationId).CountAsync(c => ids.Contains(c.Id), ct),
            DuplicateKinds.Leads => await InWorkspace(_db.Leads, userId, organizationId).CountAsync(l => ids.Contains(l.Id), ct),
            DuplicateKinds.Deals => await _db.Deals.VisibleTo(_db, userId, organizationId).CountAsync(d => ids.Contains(d.Id), ct),
            // Club records keep their own shapes; a decision naming one that is not there matches nothing.
            _ => null,
        };

    public async Task<int> AddDecisionsAsync(string kind, IReadOnlyCollection<(Guid A, Guid B)> pairs, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = (await DecisionsAsync(kind, userId, organizationId, ct))
            .Select(d => d.RecordIdA.CompareTo(d.RecordIdB) <= 0 ? (d.RecordIdA, d.RecordIdB) : (d.RecordIdB, d.RecordIdA))
            .ToHashSet();
        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var (a, b) in pairs)
        {
            if (!existing.Add((a, b))) continue;
            _db.DuplicateDecisions.Add(new DuplicateDecision
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = userId, Kind = kind,
                RecordIdA = a, RecordIdB = b, CreatedAtUtc = now,
            });
            added++;
        }
        if (added > 0) await _db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<int> RemoveDecisionsAsync(string kind, IReadOnlyCollection<(Guid A, Guid B)> pairs, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var wanted = pairs.ToHashSet();
        var ids = pairs.SelectMany(p => new[] { p.A, p.B }).Distinct().ToList();
        // Read either way round: a copied workspace (OrganizationCloner) can hold a pair
        // the other way round from how it was written.
        var rows = (await InWorkspace(_db.DuplicateDecisions, userId, organizationId)
                .Where(d => d.Kind == kind && ids.Contains(d.RecordIdA) && ids.Contains(d.RecordIdB))
                .ToListAsync(ct))
            .Where(d => wanted.Contains(d.RecordIdA.CompareTo(d.RecordIdB) <= 0 ? (d.RecordIdA, d.RecordIdB) : (d.RecordIdB, d.RecordIdA)))
            .ToList();
        if (rows.Count == 0) return 0;
        _db.DuplicateDecisions.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
