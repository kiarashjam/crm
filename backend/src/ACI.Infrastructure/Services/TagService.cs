using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The workspace's tags (one list for every kind of record, each with a colour) and the
/// tags on its leads, contacts, companies, deals and members.
/// </summary>
/// <remarks>
/// Anyone who can edit records can make a tag by using it and put tags on records. Renaming,
/// recolouring or deleting a tag changes every record that carries it, so it takes the
/// right to manage the workspace's settings. A record's tags are read and changed only
/// through records the caller can see, so a private deal's tags stay with it.
/// </remarks>
public sealed class TagService : ITagService
{
    /// <summary>Most records, and most tags, one bulk change takes.</summary>
    public const int MaxBulkRecords = 1000;
    public const int MaxBulkTags = 20;

    private readonly AppDbContext _db;
    private readonly IWorkspaceRecords _records;
    private readonly IOrganizationRepository _organizations;

    public TagService(AppDbContext db, IWorkspaceRecords records, IOrganizationRepository organizations)
    {
        _db = db;
        _records = records;
        _organizations = organizations;
    }

    private IQueryable<Tag> TagsIn(Guid userId, Guid? organizationId) =>
        organizationId is { } org
            ? _db.Tags.Where(t => t.OrganizationId == org)
            : _db.Tags.Where(t => t.OrganizationId == null && t.OwnerUserId == userId);

    private IQueryable<RecordTag> RecordTagsIn(Guid userId, Guid? organizationId) =>
        organizationId is { } org
            ? _db.RecordTags.Where(t => t.OrganizationId == org)
            : _db.RecordTags.Where(t => t.OrganizationId == null && t.OwnerUserId == userId);

    public async Task<IReadOnlyList<TagDto>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var uses = await RecordTagsIn(userId, organizationId).AsNoTracking()
            .GroupBy(t => t.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TagId, g => g.Count, ct);
        var tags = await TagsIn(userId, organizationId).AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        return tags.Select(t => new TagDto(t.Id, t.Name, t.Color, uses.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<Result<TagDto>> CreateAsync(Guid userId, Guid? organizationId, CreateTagRequest request, CancellationToken ct = default)
    {
        var name = Clean(request.Name);
        if (name == null) return Result.Failure<TagDto>(DomainErrors.Tag.NameRequired);
        var color = string.IsNullOrWhiteSpace(request.Color) ? TagColors.Slate : request.Color.Trim().ToLowerInvariant();
        if (!TagColors.IsColor(color)) return Result.Failure<TagDto>(DomainErrors.Tag.InvalidColor);
        if (await NameTakenAsync(userId, organizationId, name, null, ct)) return Result.Failure<TagDto>(DomainErrors.Tag.NameTaken(name));

        var now = DateTime.UtcNow;
        var tag = new Tag { Id = Guid.NewGuid(), OrganizationId = organizationId, OwnerUserId = userId, Name = name, Color = color, CreatedAtUtc = now, UpdatedAtUtc = now };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(ct);
        return Result.Success(new TagDto(tag.Id, tag.Name, tag.Color, 0));
    }

    public async Task<Result<TagDto>> UpdateAsync(Guid userId, Guid? organizationId, Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct)) return Result.Failure<TagDto>(DomainErrors.Tag.NotOwnerOrManager);
        var tag = await TagsIn(userId, organizationId).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag == null) return Result.Failure<TagDto>(DomainErrors.Tag.NotFound);
        if (request.Name != null)
        {
            var name = Clean(request.Name);
            if (name == null) return Result.Failure<TagDto>(DomainErrors.Tag.NameRequired);
            if (await NameTakenAsync(userId, organizationId, name, id, ct)) return Result.Failure<TagDto>(DomainErrors.Tag.NameTaken(name));
            tag.Name = name;
        }
        if (request.Color != null)
        {
            var color = request.Color.Trim().ToLowerInvariant();
            if (!TagColors.IsColor(color)) return Result.Failure<TagDto>(DomainErrors.Tag.InvalidColor);
            tag.Color = color;
        }
        tag.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var uses = await RecordTagsIn(userId, organizationId).CountAsync(t => t.TagId == id, ct);
        return Result.Success(new TagDto(tag.Id, tag.Name, tag.Color, uses));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct)) return Result.Failure(DomainErrors.Tag.NotOwnerOrManager);
        var tag = await TagsIn(userId, organizationId).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag == null) return Result.Failure(DomainErrors.Tag.NotFound);
        // Off every record too: the database cascades it, and the in-memory provider the tests
        // use cascades only what it tracks.
        _db.RecordTags.RemoveRange(await _db.RecordTags.Where(t => t.TagId == id).ToListAsync(ct));
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<Guid>>> RecordTagsAsync(Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default)
    {
        var type = Kind(entityType, organizationId);
        if (type == null) return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.InvalidEntityType);
        if (!await _records.ExistsAsync(type, recordId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.RecordNotFound);
        return Result.Success<IReadOnlyList<Guid>>(await TagIdsOf(userId, organizationId, recordId, ct));
    }

    public async Task<Result<IReadOnlyList<Guid>>> SetRecordTagsAsync(Guid userId, Guid? organizationId, SetRecordTagsRequest request, CancellationToken ct = default)
    {
        var type = Kind(request.EntityType, organizationId);
        if (type == null) return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.InvalidEntityType);
        if (!await _records.ExistsAsync(type, request.RecordId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.RecordNotFound);
        var wanted = (request.TagIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (wanted.Count > 0 && await TagsIn(userId, organizationId).CountAsync(t => wanted.Contains(t.Id), ct) != wanted.Count)
            return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.NotFound);

        var current = await RecordTagsIn(userId, organizationId).Where(t => t.RecordId == request.RecordId).ToListAsync(ct);
        _db.RecordTags.RemoveRange(current.Where(t => !wanted.Contains(t.TagId)));
        var now = DateTime.UtcNow;
        foreach (var tagId in wanted.Where(w => current.All(c => c.TagId != w)))
            _db.RecordTags.Add(new RecordTag
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, OwnerUserId = userId, TagId = tagId, EntityType = type,
                RecordId = request.RecordId, CreatedAtUtc = now,
            });
        if (!await SavedAsync(ct)) return Result.Failure<IReadOnlyList<Guid>>(DomainErrors.Tag.RecordsConflict);
        return Result.Success<IReadOnlyList<Guid>>(await TagIdsOf(userId, organizationId, request.RecordId, ct));
    }

    public async Task<Result<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>> RecordTagsBatchAsync(
        Guid userId, Guid? organizationId, RecordTagsBatchRequest request, CancellationToken ct = default)
    {
        var type = Kind(request.EntityType, organizationId);
        if (type == null) return Result.Failure<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(DomainErrors.Tag.InvalidEntityType);
        var ids = (request.RecordIds ?? Array.Empty<Guid>()).Distinct().Take(Application.Services.CustomFieldService.MaxBatch).ToList();
        var visible = (await _records.VisibleAsync(type, ids, userId, organizationId, ct)).ToList();
        var rows = await RecordTagsIn(userId, organizationId).AsNoTracking()
            .Where(t => visible.Contains(t.RecordId))
            .Select(t => new { t.RecordId, t.TagId })
            .ToListAsync(ct);
        return Result.Success<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
            rows.GroupBy(r => r.RecordId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(r => r.TagId).ToList()));
    }

    public async Task<Result<BulkTagResult>> BulkAsync(Guid userId, Guid? organizationId, BulkTagRequest request, CancellationToken ct = default)
    {
        var type = Kind(request.EntityType, organizationId);
        if (type == null) return Result.Failure<BulkTagResult>(DomainErrors.Tag.InvalidEntityType);
        var ids = (request.RecordIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var add = (request.Add ?? Array.Empty<Guid>()).Distinct().ToList();
        var remove = (request.Remove ?? Array.Empty<Guid>()).Distinct().Where(r => !add.Contains(r)).ToList();
        if (ids.Count > MaxBulkRecords || add.Count + remove.Count > MaxBulkTags) return Result.Failure<BulkTagResult>(DomainErrors.Tag.TooMany);
        var named = add.Concat(remove).ToList();
        if (named.Count > 0 && await TagsIn(userId, organizationId).CountAsync(t => named.Contains(t.Id), ct) != named.Count)
            return Result.Failure<BulkTagResult>(DomainErrors.Tag.NotFound);

        // Only the records the caller can see change; the rest are counted, not touched.
        var visible = (await _records.VisibleAsync(type, ids, userId, organizationId, ct)).ToList();
        var current = await RecordTagsIn(userId, organizationId)
            .Where(t => visible.Contains(t.RecordId) && named.Contains(t.TagId))
            .ToListAsync(ct);
        var removing = current.Where(t => remove.Contains(t.TagId)).ToList();
        _db.RecordTags.RemoveRange(removing);
        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var recordId in visible)
            foreach (var tagId in add.Where(a => !current.Any(c => c.RecordId == recordId && c.TagId == a)))
            {
                _db.RecordTags.Add(new RecordTag
                {
                    Id = Guid.NewGuid(), OrganizationId = organizationId, OwnerUserId = userId, TagId = tagId, EntityType = type,
                    RecordId = recordId, CreatedAtUtc = now,
                });
                added++;
            }
        if (!await SavedAsync(ct)) return Result.Failure<BulkTagResult>(DomainErrors.Tag.RecordsConflict);
        return Result.Success(new BulkTagResult(visible.Count, added, removing.Count, ids.Count - visible.Count));
    }

    /// <summary>
    /// Saves; false when another change of the same record's tags got there first (the unique
    /// tag-and-record index), with nothing of this one written.
    /// </summary>
    private async Task<bool> SavedAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _db.ChangeTracker.Entries<RecordTag>().ToList()) entry.State = EntityState.Detached;
            return false;
        }
    }

    private async Task<IReadOnlyList<Guid>> TagIdsOf(Guid userId, Guid? organizationId, Guid recordId, CancellationToken ct) =>
        await RecordTagsIn(userId, organizationId).AsNoTracking().Where(t => t.RecordId == recordId).Select(t => t.TagId).ToListAsync(ct);

    private Task<bool> NameTakenAsync(Guid userId, Guid? organizationId, string name, Guid? except, CancellationToken ct)
    {
        var lower = name.ToLower();
        return TagsIn(userId, organizationId).AnyAsync(t => t.Name.ToLower() == lower && (except == null || t.Id != except), ct);
    }

    private async Task<bool> CanManageAsync(Guid userId, Guid? organizationId, CancellationToken ct) =>
        organizationId is not { } org || await _organizations.HasPermissionAsync(userId, org, MemberPermissions.ManageSettings, ct);

    /// <summary>The kind of record, when it carries tags here (members only in an organization); null otherwise.</summary>
    private static string? Kind(string? entityType, Guid? organizationId)
    {
        var type = entityType?.Trim().ToLowerInvariant();
        return type != null && RecordTypes.WithFields.Contains(type) && (type != RecordTypes.Member || organizationId != null) ? type : null;
    }

    private static string? Clean(string? name)
    {
        var trimmed = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (trimmed.Length == 0) return null;
        return trimmed.Length <= Tag.MaxNameLength ? trimmed : trimmed[..Tag.MaxNameLength];
    }
}
