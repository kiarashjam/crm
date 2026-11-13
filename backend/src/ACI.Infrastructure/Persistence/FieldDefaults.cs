using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// A new lead, contact, company, deal or member starts with the default of each of its
/// workspace's fields that has one, in the save that creates it.
/// </summary>
/// <remarks>
/// Doing it here covers every way a record is made: a form, an import, the website, a lead
/// converted, an application approved. A value written for the same field in the same
/// save (an import's column) is kept instead. The definitions are found whatever workspace
/// the save acts for (IgnoreQueryFilters), by the record's own workspace, since a job or a
/// public form may act for none. An organization being copied brings its own values, so the
/// copy sets <see cref="AppDbContext.AppliesFieldDefaults"/> off.
/// </remarks>
internal static class FieldDefaults
{
    private sealed record NewRecord(string Type, Guid Id, Guid? OrganizationId, Guid OwnerUserId);

    private static List<NewRecord> NewRecords(AppDbContext db)
    {
        var added = new List<NewRecord>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case Lead r: added.Add(new(RecordTypes.Lead, r.Id, r.OrganizationId, r.UserId)); break;
                case Contact r: added.Add(new(RecordTypes.Contact, r.Id, r.OrganizationId, r.UserId)); break;
                case Company r: added.Add(new(RecordTypes.Company, r.Id, r.OrganizationId, r.UserId)); break;
                case Deal r: added.Add(new(RecordTypes.Deal, r.Id, r.OrganizationId, r.UserId)); break;
                case ClubMember r when r.OrganizationId != null: added.Add(new(RecordTypes.Member, r.Id, r.OrganizationId, r.OwnerUserId)); break;
            }
        }
        return added;
    }

    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.AppliesFieldDefaults) return;
        var added = NewRecords(db);
        if (added.Count == 0) return;
        foreach (var group in added.GroupBy(r => (r.Type, r.OrganizationId, Owner: r.OrganizationId == null ? r.OwnerUserId : Guid.Empty)))
        {
            var (type, org, owner) = group.Key;
            var fields = await Defaulted(db, type, org, owner).ToListAsync(ct);
            Add(db, group, fields);
        }
    }

    public static void Apply(AppDbContext db)
    {
        if (!db.AppliesFieldDefaults) return;
        var added = NewRecords(db);
        if (added.Count == 0) return;
        foreach (var group in added.GroupBy(r => (r.Type, r.OrganizationId, Owner: r.OrganizationId == null ? r.OwnerUserId : Guid.Empty)))
        {
            var (type, org, owner) = group.Key;
            Add(db, group, Defaulted(db, type, org, owner).ToList());
        }
    }

    private static IQueryable<CustomFieldDefinition> Defaulted(AppDbContext db, string type, Guid? org, Guid owner) =>
        db.CustomFieldDefinitions.IgnoreQueryFilters()
            .Where(d => d.EntityType == type && d.DefaultValueJson != null
                && (org != null ? d.OrganizationId == org : d.OrganizationId == null && d.OwnerUserId == owner));

    private static void Add(AppDbContext db, IEnumerable<NewRecord> records, List<CustomFieldDefinition> fields)
    {
        if (fields.Count == 0) return;
        var written = db.ChangeTracker.Entries<CustomFieldEntry>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity.RecordId, e.Entity.FieldId))
            .ToHashSet();
        foreach (var record in records)
        {
            foreach (var field in fields)
            {
                if (written.Contains((record.Id, field.Id))) continue;
                List<CustomFieldEntry> rows;
                try
                {
                    rows = CustomFieldValues.Rows(field, JsonDocument.Parse(field.DefaultValueJson!).RootElement);
                }
                catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
                {
                    // A default the field no longer reads is left off rather than failing the save of the record.
                    continue;
                }
                foreach (var row in rows)
                {
                    row.RecordId = record.Id;
                    row.OrganizationId = record.OrganizationId;
                    row.OwnerUserId = record.OwnerUserId;
                    db.CustomFieldEntries.Add(row);
                }
            }
        }
    }
}
