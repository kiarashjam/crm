using ACI.Application.Common;
using ACI.Application.DTOs;
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

    private static IQueryable<Lead> LeadsIn(IQueryable<Lead> q, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? q.Where(l => l.UserId == userId && l.OrganizationId == null)
            : q.Where(l => l.OrganizationId == organizationId);

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

    /// <summary>
    /// The activities the caller can see, which is what a contact's "last activity" counts:
    /// one on a private deal they cannot open is left out, as in ActivityRepository.
    /// </summary>
    private IQueryable<Activity> ActivitiesSeenBy(Guid userId, Guid? organizationId)
    {
        var visibleDeals = _db.Deals.VisibleTo(_db, userId, organizationId);
        var inWorkspace = organizationId == null
            ? _db.Activities.Where(a => a.UserId == userId && a.OrganizationId == null)
            : _db.Activities.Where(a => a.OrganizationId == organizationId);
        return inWorkspace.Where(a => a.DealId == null || visibleDeals.Any(d => d.Id == a.DealId));
    }

    /// <summary>
    /// The contacts list's filters and order, applied to the whole workspace in the database.
    /// The page used to filter and sort the twenty contacts it had been sent, so a company
    /// filter missed everyone on later pages and "recently active" reordered only the
    /// alphabetically first twenty.
    /// </summary>
    private IQueryable<Contact> Query(Guid userId, Guid? organizationId, ContactQueryOptions options)
    {
        var query = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), userId, organizationId);
        query = options.NormalizedStatus switch
        {
            ContactQueryOptions.Archived => query.Where(c => c.IsArchived),
            ContactQueryOptions.All => query,
            _ => query.Where(c => !c.IsArchived),
        };
        if (options.CompanyId is Guid companyId) query = query.Where(c => c.CompanyId == companyId);
        else if (options.HasCompany == true) query = query.Where(c => c.CompanyId != null);
        else if (options.HasCompany == false) query = query.Where(c => c.CompanyId == null);
        query = ApplySearch(query, options.Search);

        var activities = ActivitiesSeenBy(userId, organizationId);
        var now = DateTime.UtcNow;
        switch (options.Activity?.Trim().ToLowerInvariant())
        {
            case ContactQueryOptions.Recent:
                var recentSince = now.AddDays(-ContactQueryOptions.RecentDays);
                query = query.Where(c => activities.Any(a => a.ContactId == c.Id && a.OccurredAtUtc >= recentSince));
                break;
            case ContactQueryOptions.Quiet:
                var quietSince = now.AddDays(-ContactQueryOptions.QuietDays);
                query = query.Where(c => !activities.Any(a => a.ContactId == c.Id && a.OccurredAtUtc >= quietSince));
                break;
        }

        // Every order ends on the id, so a page boundary falls in the same place each time
        // and nobody is shown twice or skipped between pages.
        var desc = string.Equals(options.SortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return (options.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "email" => desc
                ? query.OrderByDescending(c => c.Email).ThenBy(c => c.Id)
                : query.OrderBy(c => c.Email).ThenBy(c => c.Id),
            "createdat" => desc
                ? query.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Name).ThenBy(c => c.Id),
            // Never contacted counts as longest ago: last when the most recent come first.
            "lastactivity" => desc
                ? query.OrderByDescending(c => activities.Where(a => a.ContactId == c.Id).Max(a => (DateTime?)a.OccurredAtUtc))
                    .ThenBy(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => activities.Where(a => a.ContactId == c.Id).Max(a => (DateTime?)a.OccurredAtUtc))
                    .ThenBy(c => c.Name).ThenBy(c => c.Id),
            _ => desc
                ? query.OrderByDescending(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => c.Name).ThenBy(c => c.Id),
        };
    }

    public async Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        ContactQueryOptions options,
        CancellationToken ct = default)
    {
        var query = Query(userId, organizationId, options);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Company)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task<ContactStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var recentSince = now.AddDays(-ContactQueryOptions.RecentDays);
        var quietSince = now.AddDays(-ContactQueryOptions.QuietDays);
        var all = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), userId, organizationId);
        var active = all.Where(c => !c.IsArchived);
        var activities = ActivitiesSeenBy(userId, organizationId);

        // One grouped query for the plain counts; the two that read activities are their own.
        var row = await active
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                WithCompany = g.Count(c => c.CompanyId != null),
                WithPhone = g.Count(c => c.Phone != null && c.Phone.Trim() != ""),
                AddedLast7Days = g.Count(c => c.CreatedAtUtc >= weekAgo),
            })
            .FirstOrDefaultAsync(ct);
        var recentlyActive = await active.CountAsync(c => activities.Any(a => a.ContactId == c.Id && a.OccurredAtUtc >= recentSince), ct);
        var quiet = await active.CountAsync(c => !activities.Any(a => a.ContactId == c.Id && a.OccurredAtUtc >= quietSince), ct);
        var archived = await all.CountAsync(c => c.IsArchived, ct);

        return new ContactStatsDto(
            row?.Total ?? 0, row?.WithCompany ?? 0, row?.WithPhone ?? 0, recentlyActive, quiet, row?.AddedLast7Days ?? 0, archived);
    }

    public async Task<(IReadOnlyList<ContactExportRow> Rows, int Total)> GetExportRowsAsync(
        Guid userId, Guid? organizationId, ContactQueryOptions options, int max, CancellationToken ct = default)
    {
        var query = Query(userId, organizationId, options);
        var total = await query.CountAsync(ct);
        var activities = ActivitiesSeenBy(userId, organizationId);
        var rows = await query
            .Take(max)
            .Select(c => new ContactExportRow(
                c.Name,
                c.Email,
                c.Phone,
                c.JobTitle,
                c.Company != null ? c.Company.Name : null,
                c.DoNotContact,
                c.IsArchived,
                activities.Where(a => a.ContactId == c.Id).Max(a => (DateTime?)a.OccurredAtUtc),
                c.CreatedAtUtc))
            .ToListAsync(ct);
        return (rows, total);
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

    public async Task<bool> EmailExistsAsync(string email, Guid userId, Guid? organizationId, Guid? excludeContactId = null, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // The workspace's contacts, as everywhere else. "OrganizationId IS NULL" alone
        // was every personal user's, so check-email answered whether some other user
        // had that person, and adding them was refused as a duplicate nobody could see.
        var query = FilterByUserAndOrg(_db.Contacts, userId, organizationId)
            .Where(c => !c.IsArchived && c.Email.ToLower() == normalizedEmail);
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
        await _db.Leads.Where(l => l.ReferredByContactId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.ReferredByContactId, (Guid?)null), ct);
        await _db.ContactRelationships.Where(r => r.ContactId == id || r.RelatedContactId == id).ExecuteDeleteAsync(ct);

        // Every lead with a name and an email is given a person contact automatically, so
        // a lead whose contact this was would get a new, empty one the next time anyone
        // saved it, and the delete would quietly not stick. The lead keeps a note that a
        // person deleted its contact instead, and no other is made for it.
        var leadId = entity.ConvertedFromLeadId;
        var now = DateTime.UtcNow;
        foreach (var lead in await LeadsIn(_db.Leads, userId, organizationId)
                     .Where(l => l.ConvertedToContactId == id || l.Id == leadId)
                     .ToListAsync(ct))
        {
            if (lead.ConvertedToContactId == id) lead.ConvertedToContactId = null;
            lead.PersonContactRemovedAtUtc ??= now;
        }

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

        // Only contacts in the lead's own workspace. Matched by organization alone,
        // two users with no organization shared one pool: a lead took another user's
        // contact of the same email, and showed that contact's spouse and family.
        var workspace = FilterByUserAndOrg(_db.Contacts, lead.UserId, lead.OrganizationId);

        var linked = await _db.Contacts.FirstOrDefaultAsync(c => c.ConvertedFromLeadId == lead.Id, ct);
        if (linked != null)
        {
            var sameWorkspace = linked.OrganizationId == lead.OrganizationId
                && (lead.OrganizationId != null || linked.UserId == lead.UserId);
            if (sameWorkspace)
            {
                await RememberContactOnLeadAsync(lead, linked.Id, ct);
                return;
            }
            // Another user's contact, claimed by the old match. It lets go of this lead
            // (one contact per lead), and the lead gets a contact of its own below.
            linked.ConvertedFromLeadId = null;
            await _db.SaveChangesAsync(ct);
        }

        if (lead.ConvertedToContactId is Guid existingId)
        {
            // An archived contact is still this lead's: making a new one would bring back
            // a person someone put away.
            if (await workspace.AnyAsync(c => c.Id == existingId, ct))
                return;
            // Gone altogether, so deleted before a delete left its note on the lead. The
            // note is written now, rather than the contact made again.
            if (!await _db.Contacts.AnyAsync(c => c.Id == existingId, ct))
            {
                var tracked = await _db.Leads.FirstOrDefaultAsync(l => l.Id == lead.Id, ct);
                var removedAt = tracked?.PersonContactRemovedAtUtc ?? DateTime.UtcNow;
                if (tracked != null)
                {
                    tracked.ConvertedToContactId = null;
                    tracked.PersonContactRemovedAtUtc = removedAt;
                    await _db.SaveChangesAsync(ct);
                }
                lead.ConvertedToContactId = null;
                lead.PersonContactRemovedAtUtc = removedAt;
                return;
            }
        }

        var email = lead.Email.Trim();
        var byEmail = await workspace.FirstOrDefaultAsync(c => !c.IsArchived && c.Email == email, ct);
        if (byEmail != null)
        {
            if (byEmail.ConvertedFromLeadId == null)
                byEmail.ConvertedFromLeadId = lead.Id;
            await RememberContactOnLeadAsync(lead, byEmail.Id, ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Someone deleted this lead's contact (see DeleteAsync); making another undoes that.
        // Only making one: a contact someone added since under the lead's email is the
        // person, and the lead is linked to it above, as any lead is.
        if (lead.PersonContactRemovedAtUtc != null)
            return;

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

    public async Task<int> BackfillPersonContactsAsync(int maxLeads, CancellationToken ct = default)
    {
        var linkedLeadIds = _db.Contacts
            .Where(c => c.ConvertedFromLeadId != null)
            .Select(c => c.ConvertedFromLeadId!.Value);
        // Looked at once per run, linked or not: a lead that cannot be given a contact
        // must not be picked first again and again while the others wait.
        var seen = new List<Guid>();
        var linked = 0;
        while (seen.Count < maxLeads)
        {
            var missing = await _db.Leads
                .Where(l => l.Name.Trim() != "" && l.Email.Trim() != "" && l.ConvertedToContactId == null
                    && l.PersonContactRemovedAtUtc == null && !linkedLeadIds.Contains(l.Id) && !seen.Contains(l.Id))
                .OrderBy(l => l.CreatedAtUtc)
                .Take(Math.Min(50, maxLeads - seen.Count))
                .ToListAsync(ct);
            if (missing.Count == 0) break;

            foreach (var lead in missing)
            {
                seen.Add(lead.Id);
                try
                {
                    await EnsurePersonContactAsync(lead, ct);
                    if (lead.ConvertedToContactId != null) linked++;
                }
                catch (DbUpdateException)
                {
                    // One lead whose contact cannot be saved (a clash with a row someone
                    // wrote meanwhile, say) is tried again on the next run; the rest go on.
                    _db.ChangeTracker.Clear();
                }
            }
        }
        return linked;
    }

    public async Task<Contact?> GetPersonContactAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var workspace = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), userId, organizationId);
        if (lead.ConvertedToContactId is Guid id && await workspace.FirstOrDefaultAsync(c => c.Id == id, ct) is { } pointed)
            return pointed;
        if (await workspace.FirstOrDefaultAsync(c => c.ConvertedFromLeadId == lead.Id, ct) is { } linked)
            return linked;
        // Not linked yet: the contact the next save links the lead to (see
        // EnsurePersonContactAsync), so the lead page shows the person someone added again
        // after deleting the lead's contact, instead of saying it was deleted.
        if (string.IsNullOrWhiteSpace(lead.Name) || string.IsNullOrWhiteSpace(lead.Email)) return null;
        var email = lead.Email.Trim();
        return await workspace.FirstOrDefaultAsync(c => !c.IsArchived && c.Email == email, ct);
    }

    public async Task<IReadOnlyList<Contact>> FindLookalikesAsync(Contact contact, CancellationToken ct = default)
    {
        // The same rules as the Duplicates page (DuplicateRules), on the indexed keys: this
        // runs on every contact page, and it used to read the whole workspace each time.
        // It also compared every digit of the phones, so "+41 79 123 45 67" and
        // "079 123 45 67" never matched.
        var self = DuplicateRules.PersonOf(contact.Id, contact.Email, contact.Phone, contact.Name, contact.CompanyId);
        var email = self.Email;
        var phone = self.Phone;
        var name = self.Name;
        var active = FilterByUserAndOrg(_db.Contacts.AsNoTracking(), contact.UserId, contact.OrganizationId).Where(c => !c.IsArchived);
        // A phone or a name more people share than DuplicateRules.MaxShared (an office
        // switchboard, a very common name) says nothing about any two of them, and the
        // Duplicates page leaves it out. Asked here anyway, the page asked about each
        // colleague on the switchboard in turn, one "keep them separate" after another.
        // The value still counts when it disagrees, as it does on the Duplicates page.
        var tooCommon = new HashSet<string>();
        if (phone != null && await active.CountAsync(c => c.PhoneKey == phone, ct) > DuplicateRules.MaxShared)
        {
            phone = null;
            tooCommon.Add(DuplicateRules.SamePhone);
        }
        if (name != null && await active.CountAsync(c => c.NameKey == name, ct) > DuplicateRules.MaxShared)
        {
            name = null;
            tooCommon.Add(DuplicateRules.SameName);
        }
        var others = await active
            .Where(c => c.Id != contact.Id
                && ((email != null && c.Email.Trim().ToLower() == email)
                    || (phone != null && c.PhoneKey == phone)
                    || (name != null && c.NameKey == name)))
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Take(DuplicateRules.MaxShared)
            .ToListAsync(ct);
        if (others.Count == 0) return others;

        // Not offered again once someone in the workspace said they are not the same.
        var decisions = _db.DuplicateDecisions.AsNoTracking().Where(d => d.Kind == DuplicateKinds.Contacts
            && (d.RecordIdA == contact.Id || d.RecordIdB == contact.Id));
        decisions = contact.OrganizationId == null
            ? decisions.Where(d => d.UserId == contact.UserId && d.OrganizationId == null)
            : decisions.Where(d => d.OrganizationId == contact.OrganizationId);
        var notSame = (await decisions.Select(d => d.RecordIdA == contact.Id ? d.RecordIdB : d.RecordIdA).ToListAsync(ct)).ToHashSet();

        return others
            .Where(o => !notSame.Contains(o.Id))
            .Select(o => (Other: o, Reasons: DuplicateRules.Matches(self, DuplicateRules.PersonOf(o.Id, o.Email, o.Phone, o.Name, o.CompanyId))
                .Where(reason => !tooCommon.Contains(reason)).ToList()))
            .Where(x => x.Reasons.Count > 0)
            .OrderByDescending(x => x.Reasons.Count)
            .ThenBy(x => x.Reasons.Contains(DuplicateRules.SameEmail) ? 0 : 1)
            .Take(5)
            .Select(x => x.Other)
            .ToList();
    }

    public Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        RecordMerge.InTransactionAsync(_db, async () =>
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
            foreach (var message in await _db.SentMessages.Where(m => m.ContactId == dropId).ToListAsync(ct))
                message.ContactId = keepId;
            foreach (var message in await _db.OutboundMessages.Where(m => m.ContactId == dropId).ToListAsync(ct))
                message.ContactId = keepId;
            foreach (var lead in await _db.Leads.Where(l => l.ConvertedToContactId == dropId).ToListAsync(ct))
                lead.ConvertedToContactId = keepId;
            // The people the dropped record referred were referred by the person kept.
            foreach (var lead in await _db.Leads.Where(l => l.ReferredByContactId == dropId).ToListAsync(ct))
                lead.ReferredByContactId = keepId;
            await MoveRelationshipsAsync(keepId, dropId, ct);
            await RecordMerge.MoveEnrollmentsAsync(_db, "contact", keepId, dropId, ct);
            // The dropped record is archived rather than deleted, so its files and
            // custom fields would otherwise stay on a contact nobody sees.
            await RecordMerge.MoveDependentsAsync(_db, RecordTypes.Contact, keepId, dropId, ct);

            if (drop.ConvertedFromLeadId is Guid leadId && keep.ConvertedFromLeadId == null)
            {
                keep.ConvertedFromLeadId = leadId;
                keep.ConvertedAtUtc ??= drop.ConvertedAtUtc;
            }
            drop.ConvertedFromLeadId = null;

            keep.Phone = RecordMerge.Fill(keep.Phone, drop.Phone);
            keep.CompanyId ??= drop.CompanyId;
            keep.JobTitle = RecordMerge.Fill(keep.JobTitle, drop.JobTitle);
            keep.Description = RecordMerge.Fill(keep.Description, drop.Description);
            keep.PreferredContactMethod = RecordMerge.Fill(keep.PreferredContactMethod, drop.PreferredContactMethod);
            // Either record saying "do not contact" is the person saying it. Merging must not
            // quietly make them reachable again.
            if (drop.DoNotContact)
                keep.DoNotContact = true;
            keep.CreatedAtUtc = RecordMerge.Earliest(keep.CreatedAtUtc, drop.CreatedAtUtc)!.Value;

            drop.IsArchived = true;
            drop.ArchivedAtUtc = DateTime.UtcNow;
            drop.ArchivedByUserId = userId;
            keep.UpdatedAtUtc = DateTime.UtcNow;
            keep.UpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task<string?> FindDoNotContactAddressAsync(Guid organizationId, IReadOnlyCollection<string> addresses, CancellationToken ct = default)
    {
        var wanted = addresses.Select(DoNotContactRules.Normalize).Where(a => a.Length > 0).Distinct().ToList();
        if (wanted.Count == 0) return null;
        var stopped = await _db.Contacts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.DoNotContact && wanted.Contains(c.Email.Trim().ToLower()))
            .Select(c => c.Email)
            .ToListAsync(ct);
        var stoppedSet = stopped.Select(DoNotContactRules.Normalize).ToHashSet();
        // Named as it was written, so the person sending recognises it.
        return addresses.FirstOrDefault(a => stoppedSet.Contains(DoNotContactRules.Normalize(a)));
    }

    public async Task<bool> IsDoNotContactAsync(Guid organizationId, Guid? leadId, Guid? contactId, CancellationToken ct = default)
    {
        if (contactId is Guid id)
        {
            var contact = await _db.Contacts.AsNoTracking()
                .Where(c => c.Id == id && c.OrganizationId == organizationId)
                .Select(c => new { c.DoNotContact, c.Email })
                .FirstOrDefaultAsync(ct);
            if (contact != null && (contact.DoNotContact || await DoNotContactRules.AddressAsync(_db, organizationId, contact.Email, ct)))
                return true;
        }
        if (leadId is Guid lid)
        {
            var lead = await _db.Leads.AsNoTracking()
                .Where(l => l.Id == lid && l.OrganizationId == organizationId)
                .Select(l => new { l.Email, l.ConvertedToContactId })
                .FirstOrDefaultAsync(ct);
            if (lead != null && await DoNotContactRules.LeadAsync(_db, organizationId, lid, lead.ConvertedToContactId, lead.Email, ct))
                return true;
        }
        return false;
    }

    public async Task<DoNotContactMarked> MarkDoNotContactAsync(Guid organizationId, string email, Guid ownerUserId, CancellationToken ct = default)
    {
        var address = DoNotContactRules.Normalize(email);
        var leads = await _db.Leads
            .Where(l => l.OrganizationId == organizationId && l.Email.Trim().ToLower() == address)
            .ToListAsync(ct);
        // A lead keeps its "stop" on its person contact, so each needs one.
        foreach (var lead in leads)
            await EnsurePersonContactAsync(lead, ct);
        var leadIds = leads.Select(l => l.Id).ToList();
        var personIds = leads.Where(l => l.ConvertedToContactId != null).Select(l => l.ConvertedToContactId!.Value).ToList();

        var contacts = await _db.Contacts
            .Where(c => c.OrganizationId == organizationId
                && (c.Email.Trim().ToLower() == address
                    || personIds.Contains(c.Id)
                    || (c.ConvertedFromLeadId != null && leadIds.Contains(c.ConvertedFromLeadId.Value))))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        if (contacts.Count == 0)
        {
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                OrganizationId = organizationId,
                Name = email.Trim(),
                Email = email.Trim(),
                Description = $"Unsubscribed from this workspace's email on {now:yyyy-MM-dd} (UTC).",
                CreatedAtUtc = now,
            };
            _db.Contacts.Add(contact);
            contacts.Add(contact);
        }
        foreach (var contact in contacts.Where(c => !c.DoNotContact))
        {
            contact.DoNotContact = true;
            contact.UpdatedAtUtc = now;
        }
        await _db.SaveChangesAsync(ct);
        return new DoNotContactMarked(contacts.Select(c => c.Id).ToList(), leadIds);
    }

    /// <summary>
    /// The dropped record's spouse, children and so on are the kept person's. One that
    /// would relate the person to themselves, repeat a relative the kept record already
    /// has, or give them a second spouse or partner is removed instead.
    /// </summary>
    private async Task MoveRelationshipsAsync(Guid keepId, Guid dropId, CancellationToken ct)
    {
        var kept = await _db.ContactRelationships
            .Where(r => r.ContactId == keepId || r.RelatedContactId == keepId)
            .ToListAsync(ct);
        var relatives = kept.Select(r => r.ContactId == keepId ? r.RelatedContactId : r.ContactId).ToHashSet();
        var hasCouple = kept.Any(r => ContactRelationshipTypes.IsCouple(r.Type));

        var moving = await _db.ContactRelationships
            .Where(r => r.ContactId == dropId || r.RelatedContactId == dropId)
            .ToListAsync(ct);
        foreach (var relationship in moving)
        {
            if (relationship.ContactId == dropId) relationship.ContactId = keepId;
            if (relationship.RelatedContactId == dropId) relationship.RelatedContactId = keepId;
            var other = relationship.ContactId == keepId ? relationship.RelatedContactId : relationship.ContactId;
            var couple = ContactRelationshipTypes.IsCouple(relationship.Type);
            if (other == keepId || !relatives.Add(other) || (couple && hasCouple))
            {
                _db.ContactRelationships.Remove(relationship);
                continue;
            }
            hasCouple |= couple;
        }
    }

    private async Task RememberContactOnLeadAsync(Lead lead, Guid contactId, CancellationToken ct)
    {
        var tracked = await _db.Leads.FirstOrDefaultAsync(l => l.Id == lead.Id, ct);
        if (tracked == null) return;
        tracked.ConvertedToContactId = contactId;
        lead.ConvertedToContactId = contactId;
        // The lead has a contact again, so the note that its last one was deleted is
        // history: deleting this one writes it anew.
        tracked.PersonContactRemovedAtUtc = null;
        lead.PersonContactRemovedAtUtc = null;
    }
}
