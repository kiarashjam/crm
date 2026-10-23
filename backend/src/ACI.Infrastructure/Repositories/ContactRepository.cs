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
        organizationId == null
            ? q.Where(c => c.UserId == userId && c.OrganizationId == null)
            : q.Where(c => c.OrganizationId == organizationId);

    private static IQueryable<Contact> ApplySearch(IQueryable<Contact> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(c =>
            c.Name.ToLower().Contains(q) ||
            c.Email.ToLower().Contains(q) ||
            (c.Phone != null && c.Phone.Contains(q)) ||
            (c.JobTitle != null && c.JobTitle.ToLower().Contains(q)) ||
            (c.Company != null && c.Company.Name.ToLower().Contains(q)));
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

    public async Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default, int? take = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            if (take is not > 0)
                return await GetByUserIdAsync(userId, organizationId, includeArchived, ct);
            var all = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), userId, organizationId);
            if (!includeArchived) all = all.Where(c => !c.IsArchived);
            return await all.Include(c => c.Company).OrderBy(c => c.Name).Take(take.Value).ToListAsync(ct);
        }
        var q = query.Trim().ToLowerInvariant();
        var baseQuery = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), userId, organizationId);
        if (!includeArchived) baseQuery = baseQuery.Where(c => !c.IsArchived);
        var matches = baseQuery
            .Where(c =>
                c.Name.ToLower().Contains(q) ||
                c.Email.ToLower().Contains(q) ||
                (c.Phone != null && c.Phone.Contains(q)) ||
                (c.JobTitle != null && c.JobTitle.ToLower().Contains(q)) ||
                (c.Company != null && c.Company.Name.ToLower().Contains(q)))
            .Include(c => c.Company)
            .OrderBy(c => c.Name);
        if (take is > 0)
            return await matches.Take(take.Value).ToListAsync(ct);
        return await matches.ToListAsync(ct);
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

    public async Task EnsurePersonContactAsync(Lead lead, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lead.Name) || string.IsNullOrWhiteSpace(lead.Email))
            return;

        var linked = await _db.Contacts.FirstOrDefaultAsync(c => c.ConvertedFromLeadId == lead.Id, ct);
        if (linked != null)
        {
            await RememberContactOnLeadAsync(lead, linked.Id, ct);
            return;
        }

        if (lead.ConvertedToContactId is Guid existingId)
        {
            var existing = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == existingId && !c.IsArchived, ct);
            if (existing != null)
                return;
        }

        var email = lead.Email.Trim();
        var byEmail = await _db.Contacts.FirstOrDefaultAsync(
            c => !c.IsArchived && c.Email == email && c.OrganizationId == lead.OrganizationId, ct);
        if (byEmail != null)
        {
            if (byEmail.ConvertedFromLeadId == null)
                byEmail.ConvertedFromLeadId = lead.Id;
            await RememberContactOnLeadAsync(lead, byEmail.Id, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = lead.UserId,
            OrganizationId = lead.OrganizationId,
            Name = lead.Name.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(lead.Phone) ? null : lead.Phone.Trim(),
            CompanyId = lead.CompanyId,
            Description = string.IsNullOrWhiteSpace(lead.Description) ? null : lead.Description.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            ConvertedFromLeadId = lead.Id,
        };
        _db.Contacts.Add(contact);
        await RememberContactOnLeadAsync(lead, contact.Id, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task EnsureMissingPersonContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var linkedLeadIds = _db.Contacts
            .Where(c => c.ConvertedFromLeadId != null)
            .Select(c => c.ConvertedFromLeadId!.Value);

        var leads = organizationId == null
            ? _db.Leads.Where(l => l.UserId == userId && l.OrganizationId == null)
            : _db.Leads.Where(l => l.OrganizationId == organizationId);

        for (var pass = 0; pass < 20; pass++)
        {
            var missing = await leads
                .Where(l => l.Name != "" && l.Email != "" && l.ConvertedToContactId == null && !linkedLeadIds.Contains(l.Id))
                .OrderBy(l => l.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);

            if (missing.Count == 0) return;
            foreach (var lead in missing)
                await EnsurePersonContactAsync(lead, ct);
        }
    }

    public async Task<IReadOnlyList<Contact>> FindLookalikesAsync(Contact contact, CancellationToken ct = default)
    {
        var name = NormalizeName(contact.Name);
        var email = contact.Email.Trim().ToLowerInvariant();
        var phone = Digits(contact.Phone);

        var others = await FilterByUserAndOrg(_db.Contacts.AsNoTracking(), contact.UserId, contact.OrganizationId)
            .Where(c => c.Id != contact.Id && !c.IsArchived)
            .ToListAsync(ct);

        return others
            .Where(other => IsLookalike(name, email, phone, other))
            .Take(5)
            .ToList();
    }

    public async Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (keepId == dropId) return false;
        var keep = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == keepId, ct);
        var drop = await FilterByUserAndOrg(_db.Contacts, userId, organizationId).FirstOrDefaultAsync(c => c.Id == dropId, ct);
        if (keep == null || drop == null || keep.IsArchived || drop.IsArchived) return false;

        foreach (var deal in await _db.Deals.Where(d => d.ContactId == dropId).ToListAsync(ct))
            deal.ContactId = keepId;
        foreach (var task in await _db.TaskItems.Where(t => t.ContactId == dropId).ToListAsync(ct))
            task.ContactId = keepId;
        foreach (var activity in await _db.Activities.Where(a => a.ContactId == dropId).ToListAsync(ct))
            activity.ContactId = keepId;
        foreach (var lead in await _db.Leads.Where(l => l.ConvertedToContactId == dropId).ToListAsync(ct))
            lead.ConvertedToContactId = keepId;

        if (drop.ConvertedFromLeadId is Guid leadId && keep.ConvertedFromLeadId == null)
            keep.ConvertedFromLeadId = leadId;
        drop.ConvertedFromLeadId = null;

        if (string.IsNullOrWhiteSpace(keep.Phone) && !string.IsNullOrWhiteSpace(drop.Phone))
            keep.Phone = drop.Phone;
        if (keep.CompanyId == null && drop.CompanyId != null)
            keep.CompanyId = drop.CompanyId;
        if (string.IsNullOrWhiteSpace(keep.JobTitle) && !string.IsNullOrWhiteSpace(drop.JobTitle))
            keep.JobTitle = drop.JobTitle;
        // Either record saying "do not contact" is the person saying it. Merging must not
        // quietly make them reachable again.
        if (drop.DoNotContact)
            keep.DoNotContact = true;

        drop.IsArchived = true;
        drop.ArchivedAtUtc = DateTime.UtcNow;
        drop.ArchivedByUserId = userId;
        keep.UpdatedAtUtc = DateTime.UtcNow;
        keep.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task RememberContactOnLeadAsync(Lead lead, Guid contactId, CancellationToken ct)
    {
        var tracked = await _db.Leads.FirstOrDefaultAsync(l => l.Id == lead.Id, ct);
        if (tracked == null) return;
        tracked.ConvertedToContactId = contactId;
        lead.ConvertedToContactId = contactId;
    }

    private static bool IsLookalike(string name, string email, string phone, Contact other)
    {
        var sameName = name.Length > 1 && NormalizeName(other.Name) == name;
        var sameEmail = email.Length > 0 && other.Email.Trim().ToLowerInvariant() == email;
        var otherPhone = Digits(other.Phone);
        var samePhone = phone.Length >= 7 && otherPhone == phone;
        var score = (sameEmail ? 2 : 0) + (sameName ? 1 : 0) + (samePhone ? 1 : 0);
        return score >= 2;
    }

    private static string NormalizeName(string? value) =>
        string.Join(' ', (value ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Digits(string? value) =>
        new string((value ?? "").Where(char.IsDigit).ToArray());
}
