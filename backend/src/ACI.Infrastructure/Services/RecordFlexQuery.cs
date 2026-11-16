using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <inheritdoc cref="IRecordFlexQuery"/>
/// <remarks>Reads the workspace the request acts for, through the query filters.</remarks>
public sealed class RecordFlexQuery : IRecordFlexQuery
{
    private readonly AppDbContext _db;
    private readonly IWorkspaceRecords _records;

    public RecordFlexQuery(AppDbContext db, IWorkspaceRecords records)
    {
        _db = db;
        _records = records;
    }

    public async Task<IReadOnlySet<Guid>?> MatchingAsync(string entityType, FlexFilter filter, CancellationToken ct = default)
    {
        if (!filter.Filters) return null;
        var plan = await FlexQueryable.PlanAsync(_db, entityType, filter, ct);
        var ids = entityType switch
        {
            RecordTypes.Lead => FlexQueryable.Filter(_db.Leads.AsNoTracking(), _db, plan).Select(r => r.Id),
            RecordTypes.Contact => FlexQueryable.Filter(_db.Contacts.AsNoTracking(), _db, plan).Select(r => r.Id),
            RecordTypes.Company => FlexQueryable.Filter(_db.Companies.AsNoTracking(), _db, plan).Select(r => r.Id),
            RecordTypes.Deal => FlexQueryable.Filter(_db.Deals.AsNoTracking(), _db, plan).Select(r => r.Id),
            RecordTypes.Member => FlexQueryable.Filter(_db.ClubMembers.AsNoTracking(), _db, plan).Select(r => r.Id),
            _ => null,
        };
        return ids == null ? new HashSet<Guid>() : (await ids.ToListAsync(ct)).ToHashSet();
    }

    public async Task<FlexColumns> ExportColumnsAsync(
        Guid userId, Guid? organizationId, string entityType, IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var fields = await _db.CustomFieldDefinitions.AsNoTracking()
            .Where(d => d.EntityType == entityType)
            .OrderBy(d => d.Order).ThenBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        // A workspace that uses no tags exports the columns it always did.
        var withTags = await _db.Tags.AsNoTracking().AnyAsync(ct);
        var headers = fields.Select(f => f.Label).ToList();
        if (withTags) headers.Add("Tags");
        if (recordIds.Count == 0) return new FlexColumns(headers, new Dictionary<Guid, IReadOnlyList<string>>());

        // Read in slices: an export can hold tens of thousands of records.
        var entries = new List<CustomFieldEntry>();
        var tagRows = new List<(Guid RecordId, string Name)>();
        foreach (var slice in recordIds.Distinct().Chunk(2000))
        {
            var ids = slice.ToList();
            entries.AddRange(await _db.CustomFieldEntries.AsNoTracking().Where(e => e.EntityType == entityType && ids.Contains(e.RecordId)).ToListAsync(ct));
            if (withTags) tagRows.AddRange((await _db.RecordTags.AsNoTracking()
                    .Where(t => ids.Contains(t.RecordId))
                    .Select(t => new { t.RecordId, t.Tag.Name })
                    .ToListAsync(ct))
                .Select(t => (t.RecordId, t.Name)));
        }

        var names = new Dictionary<Guid, string>();
        var byField = fields.ToDictionary(f => f.Id);
        foreach (var group in entries.Where(e => e.LinkId != null && byField.ContainsKey(e.FieldId)).GroupBy(e => byField[e.FieldId].LinkEntityType ?? ""))
            foreach (var (id, name) in await _records.NamesAsync(group.Key, group.Select(e => e.LinkId!.Value).ToList(), userId, organizationId, ct))
                names[id] = name;

        var valuesOf = entries.GroupBy(e => e.RecordId).ToDictionary(g => g.Key, g => g.ToList());
        var tagsOf = tagRows.GroupBy(t => t.RecordId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(t => t.Name).OrderBy(n => n)));
        var cells = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var id in recordIds.Distinct())
        {
            var mine = valuesOf.GetValueOrDefault(id) ?? new List<CustomFieldEntry>();
            var row = fields.Select(f => CustomFieldValues.Text(f, mine.Where(e => e.FieldId == f.Id).ToList(), names)).ToList();
            if (withTags) row.Add(tagsOf.GetValueOrDefault(id) ?? string.Empty);
            cells[id] = row;
        }
        return new FlexColumns(headers, cells);
    }
}
