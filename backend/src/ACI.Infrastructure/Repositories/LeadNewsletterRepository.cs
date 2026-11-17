using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadNewsletterRepository : ILeadNewsletterRepository
{
    /// <summary>SQL Server takes at most 2,100 parameters in one query; a book of leads can hold more.</summary>
    private const int Slice = 500;

    private readonly AppDbContext _db;

    public LeadNewsletterRepository(AppDbContext db) => _db = db;

    private sealed record Row(Guid Id, string Kind, string Name, string? Email, string? ContactPersonName, string? ContactPersonEmail,
        string Status, Guid? ConvertedToContactId, DateTime CreatedAtUtc);

    public async Task<IReadOnlyList<NewsletterLead>> LeadsAsync(Guid organizationId, NewsletterAudienceRequest filters, int take, CancellationToken ct = default)
    {
        var query = _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId);
        if (!filters.IncludeConverted) query = query.Where(l => !l.IsConverted);
        if (Names(filters.IncludeStatuses) is { Count: > 0 } include)
            query = query.Where(l => include.Contains(l.Status.Trim().ToLower()));
        if (Names(filters.ExcludeStatuses) is { Count: > 0 } exclude)
            query = query.Where(l => !exclude.Contains(l.Status.Trim().ToLower()));
        if (Names(filters.Sources) is { Count: > 0 } sources)
            query = query.Where(l => (l.Source != null && sources.Contains(l.Source.Trim().ToLower()))
                || (l.LeadSource != null && sources.Contains(l.LeadSource.Name.Trim().ToLower())));
        var owners = (filters.OwnerIds ?? []).Distinct().ToList();
        if (owners.Count > 0 || filters.Unassigned)
        {
            var unassigned = filters.Unassigned;
            query = query.Where(l => (l.AssignedToUserId != null && owners.Contains(l.AssignedToUserId.Value)) || (unassigned && l.AssignedToUserId == null));
        }
        if (filters.CreatedFrom is DateOnly from)
        {
            var since = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAtUtc >= since);
        }
        if (filters.CreatedTo is DateOnly to)
        {
            var before = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAtUtc < before);
        }
        var rows = await query
            .OrderBy(l => l.CreatedAtUtc).ThenBy(l => l.Id)
            .Take(take)
            .Select(l => new Row(l.Id, l.Kind, l.Name, l.Email, l.ContactPersonName, l.ContactPersonEmail, l.Status, l.ConvertedToContactId, l.CreatedAtUtc))
            .ToListAsync(ct);
        return rows.Select(ToLead).ToList();
    }

    public async Task<NewsletterLead?> LeadAsync(Guid organizationId, Guid leadId, CancellationToken ct = default)
    {
        var row = await _db.Leads.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.Id == leadId)
            .Select(l => new Row(l.Id, l.Kind, l.Name, l.Email, l.ContactPersonName, l.ContactPersonEmail, l.Status, l.ConvertedToContactId, l.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
        return row == null ? null : ToLead(row);
    }

    /// <summary>The person to greet and the address to write to: an establishment's contact person, as every email about a lead does.</summary>
    private static NewsletterLead ToLead(Row r)
    {
        var establishment = r.Kind == LeadKinds.Establishment;
        var name = establishment && !string.IsNullOrWhiteSpace(r.ContactPersonName) ? r.ContactPersonName.Trim() : r.Name.Trim();
        return new NewsletterLead(r.Id, name, LeadKindRules.EmailOf(r.Kind, r.Email, r.ContactPersonEmail), r.Status, r.ConvertedToContactId, r.CreatedAtUtc);
    }

    public async Task<NewsletterUnreachable> UnreachableAsync(Guid organizationId, IReadOnlyCollection<NewsletterLead> leads, CancellationToken ct = default)
    {
        var suppressed = new HashSet<string>(StringComparer.Ordinal);
        var stopped = new HashSet<Guid>();
        var byAddress = leads
            .Where(l => !string.IsNullOrWhiteSpace(l.Email))
            .GroupBy(l => DoNotContactRules.Normalize(l.Email))
            .ToDictionary(g => g.Key, g => g.Select(l => l.Id).ToList(), StringComparer.Ordinal);

        foreach (var slice in byAddress.Keys.Chunk(Slice))
        {
            // As the outbox reads them (OutboxService.RefusalAsync): the suppression list keeps
            // addresses normalised, and an erased person's as a fingerprint, matched back here.
            var keys = slice.ToDictionary(ErasedAddresses.Key, a => a, StringComparer.Ordinal);
            var lookup = slice.Concat(keys.Keys).ToList();
            suppressed.UnionWith((await _db.EmailSuppressions.AsNoTracking()
                    .Where(s => s.OrganizationId == organizationId && lookup.Contains(s.Email))
                    .Select(s => s.Email)
                    .ToListAsync(ct))
                .Select(e => keys.TryGetValue(e, out var address) ? address : e));

            // Any contact of the workspace with the address, archived or not, marked do not contact.
            var held = await _db.Contacts.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.DoNotContact && slice.Contains(c.Email.Trim().ToLower()))
                .Select(c => c.Email)
                .ToListAsync(ct);
            foreach (var address in held.Select(DoNotContactRules.Normalize))
                if (byAddress.TryGetValue(address, out var ids)) stopped.UnionWith(ids);
        }

        // A lead is do not contact through its person contact too (DoNotContactRules.LeadAsync).
        foreach (var slice in leads.Chunk(Slice))
        {
            var leadIds = slice.Select(l => l.Id).ToList();
            var contactIds = slice.Where(l => l.ConvertedToContactId != null).Select(l => l.ConvertedToContactId!.Value).ToList();
            var marked = await _db.Contacts.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.DoNotContact
                    && ((c.ConvertedFromLeadId != null && leadIds.Contains(c.ConvertedFromLeadId.Value)) || contactIds.Contains(c.Id)))
                .Select(c => new { c.Id, c.ConvertedFromLeadId })
                .ToListAsync(ct);
            var markedContacts = marked.Select(c => c.Id).ToHashSet();
            var markedLeads = marked.Where(c => c.ConvertedFromLeadId != null).Select(c => c.ConvertedFromLeadId!.Value).ToHashSet();
            foreach (var lead in slice)
                if (markedLeads.Contains(lead.Id) || (lead.ConvertedToContactId is Guid contact && markedContacts.Contains(contact)))
                    stopped.Add(lead.Id);
        }
        return new NewsletterUnreachable(stopped, suppressed);
    }

    public Task<LeadNewsletter?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.LeadNewsletters.FirstOrDefaultAsync(n => n.OrganizationId == organizationId && n.Id == id, ct);

    public async Task<bool> TryAddAsync(LeadNewsletter newsletter, CancellationToken ct = default)
    {
        // Across workspaces, on purpose: the id is the key of the whole table, so one taken in
        // any workspace cannot be added again. Only whether it exists is read.
        if (await _db.LeadNewsletters.IgnoreQueryFilters().AnyAsync(n => n.Id == newsletter.Id, ct)) return false;
        _db.LeadNewsletters.Add(newsletter);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            // Another request with the same id saved first, between the look and the save.
            _db.Entry(newsletter).State = EntityState.Detached;
            if (await _db.LeadNewsletters.IgnoreQueryFilters().AnyAsync(n => n.Id == newsletter.Id, CancellationToken.None)) return false;
            throw;
        }
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static List<string> Names(IEnumerable<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim().ToLowerInvariant()).Distinct().ToList();
}

public sealed class MeetingBookingRepository : IMeetingBookingRepository
{
    private readonly AppDbContext _db;

    public MeetingBookingRepository(AppDbContext db) => _db = db;

    public Task<MeetingBooking?> FindAsync(Guid organizationId, string bookingId, CancellationToken ct = default) =>
        _db.MeetingBookings.FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.BookingId == bookingId, ct);

    public void Add(MeetingBooking booking) => _db.MeetingBookings.Add(booking);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A booking that lost the race for its id: let it go, so what is read next is the winner's.
            foreach (var entry in _db.ChangeTracker.Entries<MeetingBooking>().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            throw;
        }
    }

    public Task<Lead?> LeadByEmailAsync(Guid organizationId, string email, CancellationToken ct = default)
    {
        var address = DoNotContactRules.Normalize(email);
        return _db.Leads.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId
                && ((l.Email != null && l.Email.Trim().ToLower() == address)
                    || (l.ContactPersonEmail != null && l.ContactPersonEmail.Trim().ToLower() == address)))
            .OrderBy(l => l.IsConverted).ThenByDescending(l => l.CreatedAtUtc).ThenBy(l => l.Id)
            .FirstOrDefaultAsync(ct);
    }
}
