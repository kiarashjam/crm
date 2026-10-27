using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ContactRelationshipRepository : IContactRelationshipRepository
{
    private readonly AppDbContext _db;

    public ContactRelationshipRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContactRelationship>> ForContactAsync(Guid contactId, CancellationToken ct = default) =>
        await _db.ContactRelationships
            .Include(r => r.Contact)
            .Include(r => r.RelatedContact)
            .Where(r => r.ContactId == contactId || r.RelatedContactId == contactId)
            .ToListAsync(ct);

    public Task<ContactRelationship?> BetweenAsync(Guid contactId, Guid otherContactId, CancellationToken ct = default) =>
        _db.ContactRelationships.FirstOrDefaultAsync(r =>
            (r.ContactId == contactId && r.RelatedContactId == otherContactId)
            || (r.ContactId == otherContactId && r.RelatedContactId == contactId), ct);

    public Task<bool> HasCoupleAsync(Guid contactId, Guid? exceptId, CancellationToken ct = default) =>
        _db.ContactRelationships.AnyAsync(r =>
            (r.ContactId == contactId || r.RelatedContactId == contactId)
            && (r.Type == ContactRelationshipTypes.Spouse || r.Type == ContactRelationshipTypes.Partner)
            && r.Id != exceptId, ct);

    public async Task AddAsync(ContactRelationship relationship, CancellationToken ct = default)
    {
        _db.ContactRelationships.Add(relationship);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task RemoveAsync(ContactRelationship relationship, CancellationToken ct = default)
    {
        _db.ContactRelationships.Remove(relationship);
        await _db.SaveChangesAsync(ct);
    }
}
