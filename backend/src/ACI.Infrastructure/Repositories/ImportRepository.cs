using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ImportRepository : IImportRepository
{
    private const string Edited = "was changed after the import";

    private readonly AppDbContext _db;

    public ImportRepository(AppDbContext db) => _db = db;

    /// <summary>An organization's rows, or with none the user's own.</summary>
    private static IQueryable<T> InWorkspace<T>(IQueryable<T> q, Guid userId, Guid? organizationId, string owner = "UserId")
        where T : class =>
        organizationId is Guid orgId
            ? q.Where(e => EF.Property<Guid?>(e, "OrganizationId") == orgId)
            : q.Where(e => EF.Property<Guid?>(e, "OrganizationId") == null && EF.Property<Guid>(e, owner) == userId);

    /// <summary>The records a batch created, in its own workspace.</summary>
    private static IQueryable<T> OfBatch<T>(IQueryable<T> q, ImportBatch batch) where T : class =>
        InWorkspace(q, batch.UserId, batch.OrganizationId).Where(e => EF.Property<Guid?>(e, "ImportBatchId") == batch.Id);

    public async Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        // The import lets go of everything between rows, the batch included.
        if (_db.Entry(batch).State == EntityState.Detached) _db.ImportBatches.Update(batch);
        await _db.SaveChangesAsync(ct);
    }

    public Task<ImportBatch?> FindBatchAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        InWorkspace(_db.ImportBatches, userId, organizationId).FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<(ImportBatch Batch, string ImportedBy, int Remaining)>> ListBatchesAsync(
        Guid userId, Guid? organizationId, string? entityType, int take, CancellationToken ct = default)
    {
        var query = InWorkspace(_db.ImportBatches.AsNoTracking(), userId, organizationId);
        if (entityType != null) query = query.Where(b => b.EntityType == entityType);
        var batches = await query.OrderByDescending(b => b.StartedAtUtc).Take(take).ToListAsync(ct);
        if (batches.Count == 0) return Array.Empty<(ImportBatch, string, int)>();

        var ids = batches.Select(b => (Guid?)b.Id).ToList();
        var people = batches.Select(b => b.UserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var remaining = new Dictionary<Guid, int>();
        void Count(IEnumerable<(Guid? Batch, int Count)> counts)
        {
            foreach (var (batch, count) in counts)
                if (batch is Guid id) remaining[id] = remaining.GetValueOrDefault(id) + count;
        }
        Count((await _db.Leads.Where(l => ids.Contains(l.ImportBatchId)).GroupBy(l => l.ImportBatchId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).Select(g => (g.Key, g.Count)));
        Count((await _db.Contacts.Where(c => ids.Contains(c.ImportBatchId)).GroupBy(c => c.ImportBatchId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).Select(g => (g.Key, g.Count)));
        Count((await _db.Companies.Where(c => ids.Contains(c.ImportBatchId)).GroupBy(c => c.ImportBatchId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).Select(g => (g.Key, g.Count)));

        return batches
            .Select(b => (b, names.TryGetValue(b.UserId, out var name) ? name : "Someone", remaining.GetValueOrDefault(b.Id)))
            .ToList();
    }

    // Only the fields an import reads, oldest first, so "the oldest" is the one a row updates.
    public async Task<IReadOnlyList<Lead>> LeadsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await InWorkspace(_db.Leads.AsNoTracking(), userId, organizationId)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => new Lead
            {
                Id = l.Id, Name = l.Name, Email = l.Email, Phone = l.Phone, CompanyId = l.CompanyId, JobTitle = l.JobTitle,
                Source = l.Source, Status = l.Status, Country = l.Country, Industry = l.Industry, CompanySize = l.CompanySize,
                Description = l.Description, IsConverted = l.IsConverted, CreatedAtUtc = l.CreatedAtUtc,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Contact>> ContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await InWorkspace(_db.Contacts.AsNoTracking(), userId, organizationId)
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new Contact
            {
                Id = c.Id, Name = c.Name, Email = c.Email, Phone = c.Phone, CompanyId = c.CompanyId, JobTitle = c.JobTitle,
                Description = c.Description, DoNotContact = c.DoNotContact, CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Company>> CompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await InWorkspace(_db.Companies.AsNoTracking(), userId, organizationId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new Company
            {
                Id = c.Id, Name = c.Name, Domain = c.Domain, Website = c.Website, Industry = c.Industry, Size = c.Size,
                Location = c.Location, Description = c.Description, CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> CustomValuesAsync(string entityType, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await InWorkspace(_db.CustomFieldValues.AsNoTracking(), userId, organizationId, owner: "OwnerUserId")
            .Where(v => v.EntityType == entityType)
            .Select(v => new { v.RecordId, v.ValuesJson })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.RecordId).ToDictionary(g => g.Key, g => g.First().ValuesJson);
    }

    public IDisposable StampNewRecords(Guid batchId)
    {
        var previous = _db.ImportBatchId;
        _db.ImportBatchId = batchId;
        return new Restore(() => _db.ImportBatchId = previous);
    }

    public void Forget() => _db.ChangeTracker.Clear();

    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        // The in-memory provider used by the tests has no transactions. Inside one, each
        // save gets a savepoint, so a row whose save fails takes nothing else with it.
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
        {
            await work();
            return;
        }
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await work();
        await transaction.CommitAsync(ct);
    }

    public async Task<ImportUndoPlan> PlanUndoAsync(ImportBatch batch, CancellationToken ct = default)
    {
        var leads = OfBatch(_db.Leads.AsNoTracking(), batch);
        var contacts = OfBatch(_db.Contacts.AsNoTracking(), batch);
        var companies = OfBatch(_db.Companies.AsNoTracking(), batch);
        var leadIds = leads.Select(l => l.Id);
        var contactIds = contacts.Select(c => c.Id);
        var companyIds = companies.Select(c => c.Id);

        var leadRows = await leads
            .Select(l => new { l.Id, l.Name, l.IsConverted, Edited = l.UpdatedAtUtc != null || l.ScoreIsManual, l.CreatedAtUtc })
            .ToListAsync(ct);
        var contactRows = await contacts
            .Select(c => new { c.Id, c.Name, c.ConvertedFromLeadId, c.IsArchived, Edited = c.UpdatedAtUtc != null, c.CreatedAtUtc })
            .ToListAsync(ct);
        var companyRows = await companies
            .Select(c => new { c.Id, c.Name, Edited = c.UpdatedAtUtc != null, c.CreatedAtUtc })
            .ToListAsync(ct);

        // Custom field values the import wrote are its own; one filled in later is a change.
        // An import the server stopped part way has no finishing time, so its last record
        // stands in, with a minute for the values of the row it was on.
        var lastRecord = leadRows.Select(l => l.CreatedAtUtc)
            .Concat(contactRows.Select(c => c.CreatedAtUtc))
            .Concat(companyRows.Select(c => c.CreatedAtUtc))
            .DefaultIfEmpty(batch.StartedAtUtc)
            .Max();
        var since = batch.FinishedAtUtc ?? lastRecord.AddMinutes(1);

        // Why each record stays; one with no reason is removed. The first reason found is the one given.
        var keep = new Dictionary<Guid, string>();
        void Keep(IEnumerable<Guid> ids, string reason)
        {
            foreach (var id in ids) keep.TryAdd(id, reason);
        }

        // Anything a person did to a lead since: an edit, a move, an assignment, work logged on it.
        Keep(leadRows.Where(l => l.IsConverted).Select(l => l.Id), "was converted into a contact");
        Keep(leadRows.Where(l => l.Edited).Select(l => l.Id), Edited);
        Keep(await AuditedSinceCreatedAsync(RecordTypes.Lead, leadIds, ct), Edited);
        Keep(await _db.Activities.Where(a => a.LeadId != null && leadIds.Contains(a.LeadId.Value)).Select(a => a.LeadId!.Value).Distinct().ToListAsync(ct), "has activities logged");
        Keep(await _db.TaskItems.Where(t => t.LeadId != null && leadIds.Contains(t.LeadId.Value)).Select(t => t.LeadId!.Value).Distinct().ToListAsync(ct), "has tasks");
        Keep(await _db.Contracts.Where(c => c.LeadId != null && leadIds.Contains(c.LeadId.Value)).Select(c => c.LeadId!.Value).Distinct().ToListAsync(ct), "has a contract");
        Keep(await _db.SentMessages.Where(m => m.LeadId != null && leadIds.Contains(m.LeadId.Value)).Select(m => m.LeadId!.Value).Distinct().ToListAsync(ct), "has been emailed");
        Keep(await _db.OutreachEnrollments.Where(e => e.TargetType == "lead" && leadIds.Contains(e.TargetId)).Select(e => e.TargetId).Distinct().ToListAsync(ct), "is in a sequence");
        Keep(await AttachedAsync(RecordTypes.Lead, leadIds, ct), "has files attached");
        Keep(await CustomFieldsSinceAsync(RecordTypes.Lead, leadIds, since, ct), "has custom fields filled in after the import");

        Keep(contactRows.Where(c => c.IsArchived).Select(c => c.Id), "was archived");
        Keep(contactRows.Where(c => c.Edited).Select(c => c.Id), Edited);
        Keep(await AuditedSinceCreatedAsync(RecordTypes.Contact, contactIds, ct), Edited);
        Keep(await _db.Deals.Where(d => d.ContactId != null && contactIds.Contains(d.ContactId.Value)).Select(d => d.ContactId!.Value).Distinct().ToListAsync(ct), "has deals");
        Keep(await _db.Activities.Where(a => a.ContactId != null && contactIds.Contains(a.ContactId.Value)).Select(a => a.ContactId!.Value).Distinct().ToListAsync(ct), "has activities logged");
        Keep(await _db.TaskItems.Where(t => t.ContactId != null && contactIds.Contains(t.ContactId.Value)).Select(t => t.ContactId!.Value).Distinct().ToListAsync(ct), "has tasks");
        Keep(await _db.SentMessages.Where(m => m.ContactId != null && contactIds.Contains(m.ContactId.Value)).Select(m => m.ContactId!.Value).Distinct().ToListAsync(ct), "has been emailed");
        Keep(await _db.OutreachEnrollments.Where(e => e.TargetType == "contact" && contactIds.Contains(e.TargetId)).Select(e => e.TargetId).Distinct().ToListAsync(ct), "is in a sequence");
        Keep(await _db.ContactRelationships.Where(r => contactIds.Contains(r.ContactId)).Select(r => r.ContactId).Distinct().ToListAsync(ct), "has family or other relationships recorded");
        Keep(await _db.ContactRelationships.Where(r => contactIds.Contains(r.RelatedContactId)).Select(r => r.RelatedContactId).Distinct().ToListAsync(ct), "has family or other relationships recorded");
        Keep(await _db.Leads.Where(l => l.ReferredByContactId != null && contactIds.Contains(l.ReferredByContactId.Value)).Select(l => l.ReferredByContactId!.Value).Distinct().ToListAsync(ct), "referred someone");
        Keep(await AttachedAsync(RecordTypes.Contact, contactIds, ct), "has files attached");
        Keep(await CustomFieldsSinceAsync(RecordTypes.Contact, contactIds, since, ct), "has custom fields filled in after the import");

        Keep(companyRows.Where(c => c.Edited).Select(c => c.Id), Edited);
        Keep(await AuditedSinceCreatedAsync(RecordTypes.Company, companyIds, ct), Edited);
        Keep(await _db.Deals.Where(d => d.CompanyId != null && companyIds.Contains(d.CompanyId.Value)).Select(d => d.CompanyId!.Value).Distinct().ToListAsync(ct), "has deals");
        Keep(await AttachedAsync(RecordTypes.Company, companyIds, ct), "has files attached");
        Keep(await CustomFieldsSinceAsync(RecordTypes.Company, companyIds, since, ct), "has custom fields filled in after the import");

        // What else points at these contacts and companies, from inside the batch or not.
        var leadsOfContacts = (await _db.Leads
                .Where(l => l.ConvertedToContactId != null && contactIds.Contains(l.ConvertedToContactId.Value))
                .Select(l => new { l.Id, ContactId = l.ConvertedToContactId!.Value })
                .ToListAsync(ct))
            .Select(l => (l.ContactId, LeadId: l.Id))
            .Concat(contactRows.Where(c => c.ConvertedFromLeadId != null).Select(c => (ContactId: c.Id, LeadId: c.ConvertedFromLeadId!.Value)))
            .Distinct()
            .ToList();
        var leadsAtCompanies = (await _db.Leads
                .Where(l => (l.CompanyId != null && companyIds.Contains(l.CompanyId.Value))
                    || (l.ConvertedToCompanyId != null && companyIds.Contains(l.ConvertedToCompanyId.Value)))
                .Select(l => new { l.Id, l.CompanyId, l.ConvertedToCompanyId })
                .ToListAsync(ct))
            .SelectMany(l => new[] { l.CompanyId, l.ConvertedToCompanyId }.OfType<Guid>().Distinct().Select(c => (CompanyId: c, LeadId: l.Id)))
            .ToLookup(l => l.CompanyId, l => l.LeadId);
        var contactsAtCompanies = (await _db.Contacts
                .Where(c => c.CompanyId != null && companyIds.Contains(c.CompanyId.Value))
                .Select(c => new { c.Id, CompanyId = c.CompanyId!.Value })
                .ToListAsync(ct))
            .ToLookup(c => c.CompanyId, c => c.Id);

        var batchLeads = leadRows.Select(l => l.Id).ToHashSet();
        var batchContacts = contactRows.Select(c => c.Id).ToHashSet();
        bool LeadGoes(Guid id) => batchLeads.Contains(id) && !keep.ContainsKey(id);
        bool ContactGoes(Guid id) => batchContacts.Contains(id) && !keep.ContainsKey(id);

        // Until nothing moves: a record that stays can make another stay.
        bool moved;
        do
        {
            moved = false;
            // A lead and the contact made for it go together or stay together: a call
            // logged on the contact is work on that lead.
            foreach (var (contactId, leadId) in leadsOfContacts)
            {
                if (ContactGoes(contactId) && !LeadGoes(leadId))
                    moved |= keep.TryAdd(contactId, batchLeads.Contains(leadId) ? "its lead stays" : "belongs to a lead this import did not add");
                else if (LeadGoes(leadId) && !ContactGoes(contactId))
                    moved |= keep.TryAdd(leadId, "its contact stays");
            }
            // A company stays while anything that stays is at it.
            foreach (var company in companyRows)
            {
                if (keep.ContainsKey(company.Id)) continue;
                if (leadsAtCompanies[company.Id].Any(l => !LeadGoes(l)))
                    moved |= keep.TryAdd(company.Id, "a lead that stays is at this company");
                else if (contactsAtCompanies[company.Id].Any(c => !ContactGoes(c)))
                    moved |= keep.TryAdd(company.Id, "a contact that stays is at this company");
            }
        } while (moved);

        var kept = leadRows.Where(l => keep.ContainsKey(l.Id)).Select(l => new ImportKeptDto(RecordTypes.Lead, l.Id, l.Name, keep[l.Id]))
            .Concat(contactRows.Where(c => keep.ContainsKey(c.Id)).Select(c => new ImportKeptDto(RecordTypes.Contact, c.Id, c.Name, keep[c.Id])))
            .Concat(companyRows.Where(c => keep.ContainsKey(c.Id)).Select(c => new ImportKeptDto(RecordTypes.Company, c.Id, c.Name, keep[c.Id])))
            .ToList();
        return new ImportUndoPlan(
            leadRows.Where(l => !keep.ContainsKey(l.Id)).Select(l => l.Id).ToList(),
            contactRows.Where(c => !keep.ContainsKey(c.Id)).Select(c => c.Id).ToList(),
            companyRows.Where(c => !keep.ContainsKey(c.Id)).Select(c => c.Id).ToList(),
            kept);
    }

    public async Task RemoveAsync(ImportBatch batch, ImportUndoPlan plan, CancellationToken ct = default)
    {
        var leadIds = plan.Leads.ToList();
        var contactIds = plan.Contacts.ToList();
        var companyIds = plan.Companies.ToList();

        var leads = await OfBatch(_db.Leads, batch).Where(l => leadIds.Contains(l.Id)).ToListAsync(ct);
        var contacts = await OfBatch(_db.Contacts, batch).Where(c => contactIds.Contains(c.Id)).ToListAsync(ct);
        var companies = await OfBatch(_db.Companies, batch).Where(c => companyIds.Contains(c.Id)).ToListAsync(ct);
        // A contact that stays but was linked to a removed lead (one already here when the
        // lead arrived) lets go of it, as it does whenever a lead is deleted.
        foreach (var contact in await _db.Contacts
                     .Where(c => c.ConvertedFromLeadId != null && leadIds.Contains(c.ConvertedFromLeadId.Value) && !contactIds.Contains(c.Id))
                     .ToListAsync(ct))
            contact.ConvertedFromLeadId = null;
        _db.LeadStatusEvents.RemoveRange(await _db.LeadStatusEvents.Where(e => leadIds.Contains(e.LeadId)).ToListAsync(ct));

        _db.Leads.RemoveRange(leads);
        _db.Contacts.RemoveRange(contacts);
        _db.Companies.RemoveRange(companies);
        if (_db.Entry(batch).State == EntityState.Detached) _db.ImportBatches.Update(batch);
        // One save: everything goes, with its audit lines, or nothing does.
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Records with an audit line besides their "created" one: someone edited, moved, assigned or archived them.</summary>
    private Task<List<Guid>> AuditedSinceCreatedAsync(string entityType, IQueryable<Guid> ids, CancellationToken ct) =>
        _db.AuditEvents
            .Where(e => e.EntityType == entityType && e.Action != "created" && e.EntityId != null && ids.Contains(e.EntityId.Value))
            .Select(e => e.EntityId!.Value)
            .Distinct()
            .ToListAsync(ct);

    private Task<List<Guid>> AttachedAsync(string entityType, IQueryable<Guid> ids, CancellationToken ct) =>
        _db.Attachments.Where(a => a.EntityType == entityType && ids.Contains(a.RecordId)).Select(a => a.RecordId).Distinct().ToListAsync(ct);

    private Task<List<Guid>> CustomFieldsSinceAsync(string entityType, IQueryable<Guid> ids, DateTime since, CancellationToken ct) =>
        _db.CustomFieldValues
            .Where(v => v.EntityType == entityType && ids.Contains(v.RecordId) && v.UpdatedAtUtc > since)
            .Select(v => v.RecordId)
            .Distinct()
            .ToListAsync(ct);

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
