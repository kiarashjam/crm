using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Saved views of the lists. A view belongs to whoever made it: only they change it, and only
/// they see it until they share it with the workspace. Each person opens each list with one
/// view of their choosing, their own or a shared one.
/// </summary>
/// <remarks>
/// A view keeps the list's query string parameters as they are, and the list reads them as it
/// reads its address bar, so the server checks only their shape and size here.
/// </remarks>
public sealed class SavedViewService : ISavedViewService
{
    private const int MaxQueryKeys = 40;
    private const int MaxQueryLength = 4000;
    private const int MaxColumns = 60;

    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public SavedViewService(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    private IQueryable<SavedView> ViewsIn(Guid? organizationId, Guid userId) =>
        organizationId is { } org
            ? _db.SavedViews.Where(v => v.OrganizationId == org)
            : _db.SavedViews.Where(v => v.OrganizationId == null && v.OwnerUserId == userId);

    /// <summary>The views the person may open: their own, and the shared ones of the workspace.</summary>
    private IQueryable<SavedView> Openable(Guid? organizationId, Guid userId) =>
        ViewsIn(organizationId, userId).Where(v => v.OwnerUserId == userId || v.Shared);

    public async Task<Result<IReadOnlyList<SavedViewDto>>> ListAsync(Guid userId, Guid? organizationId, string? list, CancellationToken ct = default)
    {
        var key = List(list);
        if (key == null) return Result.Failure<IReadOnlyList<SavedViewDto>>(DomainErrors.SavedView.InvalidList);
        var views = await Openable(organizationId, userId).AsNoTracking().Where(v => v.List == key).OrderBy(v => v.Name).ToListAsync(ct);
        var defaultId = await DefaultOf(userId, organizationId, key, ct);
        var owners = await OwnerNamesAsync(views.Where(v => v.OwnerUserId != userId).Select(v => v.OwnerUserId), ct);
        return Result.Success<IReadOnlyList<SavedViewDto>>(views.Select(v => ToDto(v, userId, defaultId, owners)).ToList());
    }

    public async Task<Result<SavedViewDto>> CreateAsync(Guid userId, Guid? organizationId, CreateSavedViewRequest request, CancellationToken ct = default)
    {
        var key = List(request.List);
        if (key == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.InvalidList);
        var name = Name(request.Name);
        if (name == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NameRequired);
        var query = Query(request.Query);
        if (query == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.InvalidQuery);
        var columns = Columns(request.Columns, out var columnsOk);
        if (!columnsOk) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.InvalidColumns);
        var shared = request.Shared == true && organizationId != null;
        if (shared && !await MayShareAsync(userId, organizationId, ct)) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NotOwnerCannotShare);

        var now = DateTime.UtcNow;
        var view = new SavedView
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, OwnerUserId = userId, List = key, Name = name, Shared = shared,
            QueryJson = query, ColumnsJson = columns, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        _db.SavedViews.Add(view);
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToDto(view, userId, await DefaultOf(userId, organizationId, key, ct), new Dictionary<Guid, string>()));
    }

    public async Task<Result<SavedViewDto>> UpdateAsync(
        Guid userId, Guid? organizationId, Guid id, UpdateSavedViewRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var view = await Openable(organizationId, userId).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (view == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NotFound);
        if (view.OwnerUserId != userId) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NotOwner);
        if (expectedVersion is Guid expected && expected != view.Version) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.Changed);

        if (request.Name != null)
        {
            var name = Name(request.Name);
            if (name == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NameRequired);
            view.Name = name;
        }
        if (request.Query is { } raw)
        {
            var query = Query(raw);
            if (query == null) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.InvalidQuery);
            view.QueryJson = query;
        }
        if (request.Clears(nameof(UpdateSavedViewRequest.Columns))) view.ColumnsJson = null;
        else if (request.Columns is { } rawColumns)
        {
            var columns = Columns(rawColumns, out var ok);
            if (!ok) return Result.Failure<SavedViewDto>(DomainErrors.SavedView.InvalidColumns);
            view.ColumnsJson = columns;
        }
        if (request.Shared is bool shared)
        {
            shared = shared && organizationId != null;
            if (shared && !view.Shared && !await MayShareAsync(userId, organizationId, ct))
                return Result.Failure<SavedViewDto>(DomainErrors.SavedView.NotOwnerCannotShare);
            if (!shared && view.Shared)
            {
                // No longer theirs to open, so no longer anyone else's default either.
                _db.SavedViewDefaults.RemoveRange(await _db.SavedViewDefaults.Where(d => d.SavedViewId == view.Id && d.UserId != userId).ToListAsync(ct));
            }
            view.Shared = shared;
        }
        view.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SavedViewDto>(DomainErrors.SavedView.Changed);
        }
        return Result.Success(ToDto(view, userId, await DefaultOf(userId, organizationId, view.List, ct), new Dictionary<Guid, string>()));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var view = await Openable(organizationId, userId).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (view == null) return Result.Failure(DomainErrors.SavedView.NotFound);
        // A shared view that has outlived its use can also be cleared away by whoever manages the workspace.
        if (view.OwnerUserId != userId
            && !(await _permissions.PermissionsAsync(userId, organizationId, ct)).Contains(MemberPermissions.ManageSettings))
            return Result.Failure(DomainErrors.SavedView.NotOwner);
        _db.SavedViewDefaults.RemoveRange(await _db.SavedViewDefaults.Where(d => d.SavedViewId == id).ToListAsync(ct));
        _db.SavedViews.Remove(view);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetDefaultAsync(Guid userId, Guid? organizationId, SetDefaultViewRequest request, CancellationToken ct = default)
    {
        var key = List(request.List);
        if (key == null) return Result.Failure(DomainErrors.SavedView.InvalidList);
        if (request.ViewId is Guid viewId && !await Openable(organizationId, userId).AnyAsync(v => v.Id == viewId && v.List == key, ct))
            return Result.Failure(DomainErrors.SavedView.NotFound);

        var current = await _db.SavedViewDefaults
            .FirstOrDefaultAsync(d => d.UserId == userId && d.OrganizationId == organizationId && d.List == key, ct);
        if (request.ViewId is not Guid chosen)
        {
            if (current != null) _db.SavedViewDefaults.Remove(current);
        }
        else if (current == null)
        {
            _db.SavedViewDefaults.Add(new SavedViewDefault
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = userId, List = key, SavedViewId = chosen, UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            current.SavedViewId = chosen;
            current.UpdatedAtUtc = DateTime.UtcNow;
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same person set a default in two tabs at once: one of them stands.
            return Result.Failure(DomainErrors.SavedView.Changed);
        }
        return Result.Success();
    }

    private async Task<Guid?> DefaultOf(Guid userId, Guid? organizationId, string list, CancellationToken ct)
    {
        var chosen = await _db.SavedViewDefaults.AsNoTracking()
            .Where(d => d.UserId == userId && d.OrganizationId == organizationId && d.List == list)
            .Select(d => (Guid?)d.SavedViewId)
            .FirstOrDefaultAsync(ct);
        // A default the person can no longer open (unshared since, or gone) opens nothing.
        return chosen is Guid id && await Openable(organizationId, userId).AnyAsync(v => v.Id == id, ct) ? id : null;
    }

    private async Task<bool> MayShareAsync(Guid userId, Guid? organizationId, CancellationToken ct) =>
        (await _permissions.PermissionsAsync(userId, organizationId, ct)).Any(MemberPermissions.Writes.Contains);

    private async Task<IReadOnlyDictionary<Guid, string>> OwnerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private static SavedViewDto ToDto(SavedView v, Guid userId, Guid? defaultId, IReadOnlyDictionary<Guid, string> owners) =>
        new(v.Id, v.List, v.Name, v.Shared, v.OwnerUserId == userId, v.Id == defaultId, Parse(v.QueryJson) ?? Parse("{}")!.Value,
            v.ColumnsJson == null ? null : Parse(v.ColumnsJson), v.OwnerUserId == userId ? null : owners.GetValueOrDefault(v.OwnerUserId),
            v.Version, v.UpdatedAtUtc);

    private static JsonElement? Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? List(string? list)
    {
        var key = list?.Trim().ToLowerInvariant();
        return key != null && SavedViewLists.All.Contains(key) ? key : null;
    }

    private static string? Name(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= SavedView.MaxNameLength ? trimmed : trimmed[..SavedView.MaxNameLength];
    }

    /// <summary>The query as stored: an object of names to text or lists of text; null when it is not that shape.</summary>
    private static string? Query(JsonElement? raw)
    {
        if (raw is not { } query || query.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "{}";
        if (query.ValueKind != JsonValueKind.Object) return null;
        var clean = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in query.EnumerateObject())
        {
            if (property.Name.Length is 0 or > 40) return null;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    clean[property.Name] = property.Value.GetString()!;
                    break;
                case JsonValueKind.Array when property.Value.EnumerateArray().All(v => v.ValueKind == JsonValueKind.String):
                    clean[property.Name] = property.Value.EnumerateArray().Select(v => v.GetString()!).ToList();
                    break;
                default:
                    return null;
            }
        }
        var json = JsonSerializer.Serialize(clean);
        return clean.Count <= MaxQueryKeys && json.Length <= MaxQueryLength ? json : null;
    }

    private static string? Columns(JsonElement? raw, out bool ok)
    {
        ok = true;
        if (raw is not { } columns || columns.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() > MaxColumns
            || columns.EnumerateArray().Any(c => c.ValueKind != JsonValueKind.String || c.GetString()!.Length is 0 or > 80))
        {
            ok = false;
            return null;
        }
        return JsonSerializer.Serialize(columns.EnumerateArray().Select(c => c.GetString()!).Distinct().ToList());
    }
}
