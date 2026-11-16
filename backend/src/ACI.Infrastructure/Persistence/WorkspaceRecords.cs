using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Whether a record exists in the caller's workspace, by the same rule the record
/// repositories use: in an organization, its records; with none, the user's own.
/// </summary>
public sealed class WorkspaceRecords : IWorkspaceRecords
{
    private readonly AppDbContext _db;

    public WorkspaceRecords(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(string entityType, Guid recordId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        entityType switch
        {
            RecordTypes.Lead => _db.Leads.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Contact => _db.Contacts.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Company => _db.Companies.AnyAsync(r => r.Id == recordId
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId), ct),
            RecordTypes.Deal => _db.Deals.VisibleTo(_db, userId, organizationId).AnyAsync(r => r.Id == recordId, ct),
            // Members are an organization's alone; one erased keeps a row for the books but is gone for everything else.
            RecordTypes.Member => organizationId == null
                ? Task.FromResult(false)
                : _db.ClubMembers.AnyAsync(m => m.Id == recordId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct),
            _ => Task.FromResult(false),
        };

    public async Task<IReadOnlySet<Guid>> VisibleAsync(string entityType, IReadOnlyCollection<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (recordIds.Count == 0 || Named(entityType, recordIds.Distinct().ToList(), userId, organizationId) is not { } named)
            return new HashSet<Guid>();
        var found = await named.Select(r => r.Id).ToListAsync(ct);
        return found.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, IReadOnlyCollection<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (recordIds.Count == 0 || Named(entityType, recordIds.Distinct().ToList(), userId, organizationId) is not { } named)
            return new Dictionary<Guid, string>();
        return await named.ToDictionaryAsync(r => r.Id, r => r.Name, ct);
    }

    private sealed record RecordName(Guid Id, string Name);

    /// <summary>
    /// The records of <paramref name="ids"/> the caller can see, with their names, by the rule of
    /// <see cref="ExistsAsync"/>; null for a kind of record that has none here.
    /// </summary>
    private IQueryable<RecordName>? Named(string entityType, List<Guid> ids, Guid userId, Guid? organizationId) => entityType switch
    {
        RecordTypes.Lead => _db.Leads.Where(r => ids.Contains(r.Id)
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId))
            .Select(r => new RecordName(r.Id, r.Name)),
        RecordTypes.Contact => _db.Contacts.Where(r => ids.Contains(r.Id)
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId))
            .Select(r => new RecordName(r.Id, r.Name)),
        RecordTypes.Company => _db.Companies.Where(r => ids.Contains(r.Id)
                && (organizationId == null ? r.OrganizationId == null && r.UserId == userId : r.OrganizationId == organizationId))
            .Select(r => new RecordName(r.Id, r.Name)),
        RecordTypes.Deal => _db.Deals.VisibleTo(_db, userId, organizationId).Where(r => ids.Contains(r.Id))
            .Select(r => new RecordName(r.Id, r.Name)),
        RecordTypes.Member when organizationId != null => _db.ClubMembers
            .Where(m => ids.Contains(m.Id) && m.OrganizationId == organizationId && m.ErasedAtUtc == null)
            .Select(m => new RecordName(m.Id, (m.FirstName + " " + m.LastName).Trim())),
        _ => null,
    };

    public Task<bool> IsAssignableAsync(Guid assigneeId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? Task.FromResult(assigneeId == userId)
            : _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == assigneeId, ct);

    public Task<bool> PipelineExistsAsync(Guid pipelineId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? Task.FromResult(false)
            : _db.Pipelines.AnyAsync(p => p.Id == pipelineId && p.OrganizationId == organizationId, ct);

    public async Task<Guid?> StagePipelineAsync(Guid stageId, Guid? organizationId, CancellationToken ct = default) =>
        organizationId == null
            ? null
            : await _db.DealStages
                .Where(s => s.Id == stageId && s.Pipeline.OrganizationId == organizationId)
                .Select(s => (Guid?)s.PipelineId)
                .FirstOrDefaultAsync(ct);
}
