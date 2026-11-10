using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// When a lead, contact, company or deal is deleted, its attachments and custom
/// field values are deleted in the same save, and a deal's line items with it.
/// </summary>
/// <remarks>
/// They are keyed by record type and id rather than by a foreign key, because one
/// table serves four kinds of record. Doing it here covers every way a record is
/// deleted, and never reads the bytes of a file it is removing. They are found whatever
/// workspace the save acts for (IgnoreQueryFilters): a record's id is its own, and its
/// files and values must go with it even when the code deleting it acts for none.
/// </remarks>
internal static class RecordDependents
{
    private static List<(string Type, Guid Id)> DeletedRecords(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var deleted = new List<(string Type, Guid Id)>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted))
        {
            switch (entry.Entity)
            {
                case Lead r: deleted.Add((RecordTypes.Lead, r.Id)); break;
                case Contact r: deleted.Add((RecordTypes.Contact, r.Id)); break;
                case Company r: deleted.Add((RecordTypes.Company, r.Id)); break;
                case Deal r: deleted.Add((RecordTypes.Deal, r.Id)); break;
            }
        }
        return deleted;
    }

    public static async Task RemoveAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var group in DeletedRecords(db).GroupBy(r => r.Type))
        {
            var type = group.Key;
            var ids = group.Select(r => r.Id).ToList();
            await UnlinkClubAsync(db, type, ids, ct);
            var attachmentIds = await db.Attachments.IgnoreQueryFilters()
                .Where(a => a.EntityType == type && ids.Contains(a.RecordId)).Select(a => a.Id).ToListAsync(ct);
            var valueSets = await db.CustomFieldValues.IgnoreQueryFilters()
                .Where(v => v.EntityType == type && ids.Contains(v.RecordId)).ToListAsync(ct);
            var lineItems = type == RecordTypes.Deal
                ? await db.DealLineItems.Where(i => ids.Contains(i.DealId)).ToListAsync(ct)
                : new List<DealLineItem>();
            Remove(db, attachmentIds, valueSets, lineItems);
        }
    }

    /// <summary>
    /// A deleted contact or lead is no longer anyone's on the club side: members and applications
    /// stop naming it, and a contact who was in a household only as a contact (not as a member)
    /// leaves it. The member stays a member; only the link goes.
    /// </summary>
    private static async Task UnlinkClubAsync(AppDbContext db, string type, List<Guid> ids, CancellationToken ct)
    {
        if (type == RecordTypes.Contact)
        {
            foreach (var member in await db.ClubMembers.IgnoreQueryFilters().Where(m => m.ContactId != null && ids.Contains(m.ContactId.Value)).ToListAsync(ct))
                member.ContactId = null;
            foreach (var application in await db.ClubApplications.IgnoreQueryFilters().Where(a => a.ContactId != null && ids.Contains(a.ContactId.Value)).ToListAsync(ct))
            {
                application.ContactId = null;
                application.Version = Guid.NewGuid();
            }
            db.ClubHouseholdMembers.RemoveRange(await db.ClubHouseholdMembers.IgnoreQueryFilters()
                .Where(l => l.ContactId != null && ids.Contains(l.ContactId.Value)).ToListAsync(ct));
        }
        else if (type == RecordTypes.Lead)
        {
            foreach (var member in await db.ClubMembers.IgnoreQueryFilters().Where(m => m.LeadId != null && ids.Contains(m.LeadId.Value)).ToListAsync(ct))
                member.LeadId = null;
            foreach (var application in await db.ClubApplications.IgnoreQueryFilters().Where(a => a.LeadId != null && ids.Contains(a.LeadId.Value)).ToListAsync(ct))
            {
                application.LeadId = null;
                application.Version = Guid.NewGuid();
            }
        }
    }

    public static void Remove(AppDbContext db)
    {
        foreach (var group in DeletedRecords(db).GroupBy(r => r.Type))
        {
            var type = group.Key;
            var ids = group.Select(r => r.Id).ToList();
            var attachmentIds = db.Attachments.IgnoreQueryFilters()
                .Where(a => a.EntityType == type && ids.Contains(a.RecordId)).Select(a => a.Id).ToList();
            var valueSets = db.CustomFieldValues.IgnoreQueryFilters()
                .Where(v => v.EntityType == type && ids.Contains(v.RecordId)).ToList();
            var lineItems = type == RecordTypes.Deal
                ? db.DealLineItems.Where(i => ids.Contains(i.DealId)).ToList()
                : new List<DealLineItem>();
            Remove(db, attachmentIds, valueSets, lineItems);
        }
    }

    private static void Remove(AppDbContext db, List<Guid> attachmentIds, List<CustomFieldValueSet> valueSets, List<DealLineItem> lineItems)
    {
        foreach (var id in attachmentIds)
        {
            var tracked = db.ChangeTracker.Entries<Attachment>().FirstOrDefault(e => e.Entity.Id == id);
            if (tracked != null) tracked.State = EntityState.Deleted;
            else db.Entry(new Attachment { Id = id }).State = EntityState.Deleted;
        }
        db.CustomFieldValues.RemoveRange(valueSets);
        db.DealLineItems.RemoveRange(lineItems);
    }
}
