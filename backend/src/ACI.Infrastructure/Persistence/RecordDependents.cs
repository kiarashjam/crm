using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// When a lead, contact, company or deal is deleted, its attachments and custom
/// field values are deleted in the same save.
/// </summary>
/// <remarks>
/// They are keyed by record type and id rather than by a foreign key, because one
/// table serves four kinds of record. Doing it here covers every way a record is
/// deleted, and never reads the bytes of a file it is removing.
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
            var attachmentIds = await db.Attachments
                .Where(a => a.EntityType == type && ids.Contains(a.RecordId)).Select(a => a.Id).ToListAsync(ct);
            var valueSets = await db.CustomFieldValues
                .Where(v => v.EntityType == type && ids.Contains(v.RecordId)).ToListAsync(ct);
            Remove(db, attachmentIds, valueSets);
        }
    }

    public static void Remove(AppDbContext db)
    {
        foreach (var group in DeletedRecords(db).GroupBy(r => r.Type))
        {
            var type = group.Key;
            var ids = group.Select(r => r.Id).ToList();
            var attachmentIds = db.Attachments
                .Where(a => a.EntityType == type && ids.Contains(a.RecordId)).Select(a => a.Id).ToList();
            var valueSets = db.CustomFieldValues
                .Where(v => v.EntityType == type && ids.Contains(v.RecordId)).ToList();
            Remove(db, attachmentIds, valueSets);
        }
    }

    private static void Remove(AppDbContext db, List<Guid> attachmentIds, List<CustomFieldValueSet> valueSets)
    {
        foreach (var id in attachmentIds)
        {
            var tracked = db.ChangeTracker.Entries<Attachment>().FirstOrDefault(e => e.Entity.Id == id);
            if (tracked != null) tracked.State = EntityState.Deleted;
            else db.Entry(new Attachment { Id = id }).State = EntityState.Deleted;
        }
        db.CustomFieldValues.RemoveRange(valueSets);
    }
}
