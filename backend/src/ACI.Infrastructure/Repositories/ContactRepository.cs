using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;

    public ContactRepository(AppDbContext db) => _db = db;

    private static IQueryable<Contact> FilterByUserAndOrg(IQueryable<Contact> q, Guid userId, Guid? organizationId) =>
        q.Where(c => c.UserId == userId && (organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId));

    private static IQueryable<Contact> ApplySearch(IQueryable<Contact> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(c =>
            c.Name.ToLower().Contains(q) ||
            c.Email.ToLower().Contains(q) ||
            (c.Phone != null && c.Phone.Contains(q)) ||
            (c.JobTitle != null && c.JobTitle.ToLower().Contains(q)));
    }

    public async Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        bool includeArchived = false,
        Guid? companyId = null, 
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Contacts, userId, organizationId);
        if (!includeArchived) query = query.Where(c => !c.IsArchived);
        if (companyId.HasValue) query = query.Where(c => c.CompanyId == companyId.Value);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Company)
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        
        return (items, totalCount);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, bool includeArchived = false, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Contacts, userId, organizationId);
        if (!includeArchived) query = query.Where(c => !c.IsArchived);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool includeArchived = false, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Contacts, userId, organizationId);
        if (!includeArchived) query = query.Where(c => !c.IsArchived);
        return await query.Include(c => c.Company).OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, includeArchived, ct);
        var q = query.Trim().ToLowerInvariant();
        var baseQuery = FilterByUserAndOrg(_db.Contacts, userId, organizationId);
        if (!includeArchived) baseQuery = baseQuery.Where(c => !c.IsArchived);
        return await baseQuery
            .Where(c =>
                c.Name.ToLower().Contains(q) ||
                c.Email.ToLower().Contains(q) ||
                (c.Phone != null && c.Phone.Contains(q)) ||
                (c.JobTitle != null && c.JobTitle.ToLower().Contains(q)))
            .Include(c => c.Company)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Contact?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Contacts, userId, organizationId)
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Contact?> GetByEmailAsync(string email, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await FilterByUserAndOrg(_db.Contacts, userId, organizationId)
            .Where(c => !c.IsArchived && c.Email.ToLower() == normalizedEmail)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? organizationId, Guid? excludeContactId = null, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var query = _db.Contacts
            .Where(c => !c.IsArchived && c.Email.ToLower() == normalizedEmail)
            .Where(c => organizationId == null ? c.OrganizationId == null : c.OrganizationId == organizationId);
        if (excludeContactId.HasValue)
            query = query.Where(c => c.Id != excludeContactId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<Contact> AddAsync(Contact contact, CancellationToken ct = default)
    {
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task<Contact?> UpdateAsync(Contact contact, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == contact.Id, ct);
        if (existing == null) return null;
        existing.Name = contact.Name;
        existing.Email = contact.Email;
        existing.Phone = contact.Phone;
        existing.CompanyId = contact.CompanyId;
        existing.JobTitle = contact.JobTitle;
        existing.DoNotContact = contact.DoNotContact;
        existing.PreferredContactMethod = contact.PreferredContactMethod;
        existing.Description = contact.Description;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null) return false;
        // Null FKs so delete does not violate referential integrity (Deals, Activities, Tasks reference Contact)
        await _db.Deals.Where(d => d.ContactId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.ContactId, (Guid?)null), ct);
        await _db.Activities.Where(a => a.ContactId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.ContactId, (Guid?)null), ct);
        await _db.TaskItems.Where(t => t.ContactId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.ContactId, (Guid?)null), ct);
        _db.Contacts.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ArchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || entity.IsArchived) return false;
        entity.IsArchived = true;
        entity.ArchivedAtUtc = DateTime.UtcNow;
        entity.ArchivedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnarchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || !entity.IsArchived) return false;
        entity.IsArchived = false;
        entity.ArchivedAtUtc = null;
        entity.ArchivedByUserId = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
