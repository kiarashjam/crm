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

    public async Task<IReadOnlyList<ContactRelationship>> ForContactsAsync(IReadOnlyCollection<Guid> contactIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (contactIds.Count == 0) return [];
        // Both people have to be in the workspace. A lead linked to another personal
        // user's contact (by an email match that ignored whose contact it was) listed
        // that contact's spouse and family, with their names and emails.
        return await _db.ContactRelationships
            .AsNoTracking()
            .Include(r => r.Contact)
            .Include(r => r.RelatedContact)
            .Where(r => contactIds.Contains(r.ContactId) || contactIds.Contains(r.RelatedContactId))
            .Where(r => organizationId == null
                ? r.Contact.OrganizationId == null && r.Contact.UserId == userId
                    && r.RelatedContact.OrganizationId == null && r.RelatedContact.UserId == userId
                : r.Contact.OrganizationId == organizationId && r.RelatedContact.OrganizationId == organizationId)
            .ToListAsync(ct);
    }

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
