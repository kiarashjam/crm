using System.Globalization;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Services;

/// <summary>
/// Custom fields: the definitions a workspace adds to leads, contacts, companies
/// and deals, and each record's values for them.
/// </summary>
/// <remarks>
/// Definitions shape every record in the workspace, so only its owner and managers
/// change them, as with pipelines and lead routing. Anyone who can edit a record can
/// fill in its values. Every value is checked against its field before it is stored.
/// </remarks>
public sealed class CustomFieldService : ICustomFieldService
{
    private const int MaxLabelLength = 120;
    private const int MaxOptions = 50;
    private const int MaxTextLength = 2000;

    private readonly ICustomFieldRepository _repository;
    private readonly IWorkspaceRecords _records;
    private readonly IOrganizationRepository _organizations;

    public CustomFieldService(ICustomFieldRepository repository, IWorkspaceRecords records, IOrganizationRepository organizations)
    {
        _repository = repository;
        _records = records;
        _organizations = organizations;
    }

    public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ListDefinitionsAsync(
        Guid userId, Guid? organizationId, string? entityType, CancellationToken ct = default)
    {
        var type = Blank(entityType);
        if (type != null && !RecordTypes.All.Contains(type))
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainErrors.CustomField.InvalidEntityType);

        var definitions = await _repository.ListDefinitionsAsync(userId, organizationId, type, ct);
        return Result.Success<IReadOnlyList<CustomFieldDefinitionDto>>(definitions.Select(ToDto).ToList());
    }

    public async Task<Result<CustomFieldDefinitionDto>> CreateDefinitionAsync(
        Guid userId, Guid? organizationId, CreateCustomFieldRequest request, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.NotOwnerOrManager);

        var entityType = Blank(request.EntityType);
        if (entityType == null || !RecordTypes.All.Contains(entityType))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.InvalidEntityType);
        var type = Blank(request.Type);
        if (type == null || !CustomFieldTypes.All.Contains(type))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.InvalidType);
        var label = Blank(request.Label);
        if (label == null)
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.LabelRequired);
        var options = type == CustomFieldTypes.Select ? CleanOptions(request.Options) : null;
        if (options is { Count: 0 })
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.OptionsRequired);

        var existing = await _repository.ListDefinitionsAsync(userId, organizationId, entityType, ct);
        var definition = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            EntityType = entityType,
            Key = UniqueKey(label, existing.Select(d => d.Key).ToHashSet(StringComparer.Ordinal)),
            Label = Clip(label, MaxLabelLength),
            Type = type,
            OptionsJson = options == null ? null : JsonSerializer.Serialize(options),
            Required = request.Required ?? false,
            Order = existing.Count == 0 ? 0 : existing.Max(d => d.Order) + 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _repository.AddDefinitionAsync(definition, ct);
        return Result.Success(ToDto(definition));
    }

    public async Task<Result<CustomFieldDefinitionDto>> UpdateDefinitionAsync(
        Guid userId, Guid? organizationId, Guid id, UpdateCustomFieldRequest request, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.NotOwnerOrManager);
        var definition = await _repository.FindDefinitionAsync(id, userId, organizationId, ct);
        if (definition == null)
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.NotFound);

        // Values are stored under the field's key for its kind of record; moving it would orphan them.
        if (Blank(request.EntityType) is { } entityType && entityType != definition.EntityType)
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.CannotMove);
        if (request.Label != null)
        {
            var label = Blank(request.Label);
            if (label == null) return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.LabelRequired);
            definition.Label = Clip(label, MaxLabelLength);
        }
        var type = Blank(request.Type) ?? definition.Type;
        if (!CustomFieldTypes.All.Contains(type))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.InvalidType);
        if (type == CustomFieldTypes.Select)
        {
            var options = request.Options != null ? CleanOptions(request.Options) : ReadOptions(definition) ?? new List<string>();
            if (options.Count == 0) return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.OptionsRequired);
            definition.OptionsJson = JsonSerializer.Serialize(options);
        }
        else
        {
            definition.OptionsJson = null;
        }
        definition.Type = type;
        if (request.Required is { } required) definition.Required = required;

        await _repository.SaveDefinitionAsync(definition, ct);
        return Result.Success(ToDto(definition));
    }

    public async Task<Result> DeleteDefinitionAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct))
            return Result.Failure(DomainErrors.CustomField.NotOwnerOrManager);
        var definition = await _repository.FindDefinitionAsync(id, userId, organizationId, ct);
        if (definition == null) return Result.Failure(DomainErrors.CustomField.NotFound);
        // Its values stay in their records' JSON but are no longer read, since reads keep defined keys only.
        await _repository.DeleteDefinitionAsync(definition, ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyDictionary<string, JsonElement>>> GetValuesAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default)
    {
        var type = Blank(entityType);
        if (type == null || !RecordTypes.All.Contains(type))
            return Result.Failure<IReadOnlyDictionary<string, JsonElement>>(DomainErrors.CustomField.InvalidEntityType);

        // Only a record the caller can see, so a private deal's values stay with it.
        if (!await _records.ExistsAsync(type, recordId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyDictionary<string, JsonElement>>(DomainErrors.CustomField.RecordNotFound);

        var definitions = await _repository.ListDefinitionsAsync(userId, organizationId, type, ct);
        var set = await _repository.FindValuesAsync(userId, organizationId, type, recordId, ct);
        var values = Defined(set, definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal));
        return Result.Success<IReadOnlyDictionary<string, JsonElement>>(values);
    }

    public async Task<Result> SaveValuesAsync(
        Guid userId, Guid? organizationId, SaveCustomFieldValuesRequest request, CancellationToken ct = default)
    {
        var type = Blank(request.EntityType);
        if (type == null || !RecordTypes.All.Contains(type))
            return Result.Failure(DomainErrors.CustomField.InvalidEntityType);
        if (!await _records.ExistsAsync(type, request.RecordId, userId, organizationId, ct))
            return Result.Failure(DomainErrors.CustomField.RecordNotFound);

        var definitions = (await _repository.ListDefinitionsAsync(userId, organizationId, type, ct))
            .ToDictionary(d => d.Key, StringComparer.Ordinal);
        var set = await _repository.FindValuesAsync(userId, organizationId, type, request.RecordId, ct);
        var isNew = set == null;
        set ??= new CustomFieldValueSet
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            EntityType = type,
            RecordId = request.RecordId,
        };

        // Merge, so a client that sends only the fields it changed doesn't clear the rest.
        var values = Defined(set, definitions.Keys.ToHashSet(StringComparer.Ordinal));
        foreach (var (key, raw) in request.Values ?? new Dictionary<string, JsonElement>())
        {
            // A key with no definition is a field deleted since the form loaded; nothing to store.
            if (!definitions.TryGetValue(key, out var definition)) continue;
            var (ok, value, expected) = Normalize(definition, raw);
            if (!ok) return Result.Failure(DomainErrors.CustomField.InvalidValue(definition.Label, expected));
            values[key] = value;
        }

        set.ValuesJson = JsonSerializer.Serialize(values);
        set.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveValuesAsync(set, isNew, ct);
        return Result.Success();
    }

    private async Task<bool> CanManageAsync(Guid userId, Guid? organizationId, CancellationToken ct)
    {
        // A personal workspace is its owner's alone.
        if (organizationId is not { } orgId) return true;
        var role = await _organizations.GetMemberRoleAsync(userId, orgId, ct);
        return role is OrgMemberRole.Owner or OrgMemberRole.Manager;
    }

    /// <summary>A value checked against its field; strings that are empty become null.</summary>
    private static (bool Ok, JsonElement Value, string Expected) Normalize(CustomFieldDefinition definition, JsonElement raw)
    {
        var none = JsonSerializer.SerializeToElement<object?>(null);
        if (raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return (true, none, "");

        switch (definition.Type)
        {
            case CustomFieldTypes.Number:
                if (raw.ValueKind == JsonValueKind.Number) return (true, raw.Clone(), "");
                if (raw.ValueKind == JsonValueKind.String)
                {
                    var s = raw.GetString()!.Trim();
                    if (s.Length == 0) return (true, none, "");
                    if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                        return (true, JsonSerializer.SerializeToElement(n), "");
                }
                return (false, none, "must be a number");

            case CustomFieldTypes.Checkbox:
                return raw.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? (true, raw.Clone(), "")
                    : (false, none, "must be ticked or not");
        }

        if (raw.ValueKind != JsonValueKind.String) return (false, none, "must be text");
        var text = raw.GetString()!.Trim();
        if (text.Length == 0) return (true, none, "");

        switch (definition.Type)
        {
            case CustomFieldTypes.Date:
                return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? (true, JsonSerializer.SerializeToElement(text), "")
                    : (false, none, "must be a date");
            case CustomFieldTypes.Select:
                return (ReadOptions(definition) ?? new List<string>()).Contains(text, StringComparer.Ordinal)
                    ? (true, JsonSerializer.SerializeToElement(text), "")
                    : (false, none, "must be one of its options");
            case CustomFieldTypes.Url:
                // People type "www.example.com"; read it the way a browser would.
                var candidate = text.Contains("://", StringComparison.Ordinal) ? text : $"https://{text}";
                return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                       && uri.Host.Contains('.', StringComparison.Ordinal)
                    ? (true, JsonSerializer.SerializeToElement(candidate), "")
                    : (false, none, "must be a web address");
            default:
                return text.Length <= MaxTextLength
                    ? (true, JsonSerializer.SerializeToElement(text), "")
                    : (false, none, $"must be {MaxTextLength:N0} characters or fewer");
        }
    }

    /// <summary>The stored values whose fields still exist.</summary>
    private static Dictionary<string, JsonElement> Defined(CustomFieldValueSet? set, IReadOnlySet<string> keys)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (set == null) return values;
        using var doc = JsonDocument.Parse(set.ValuesJson);
        foreach (var property in doc.RootElement.EnumerateObject())
            if (keys.Contains(property.Name)) values[property.Name] = property.Value.Clone();
        return values;
    }

    private static CustomFieldDefinitionDto ToDto(CustomFieldDefinition d) =>
        new(d.Id, d.EntityType, d.Key, d.Label, d.Type, ReadOptions(d), d.Required, d.Order);

    private static List<string>? ReadOptions(CustomFieldDefinition d) =>
        d.OptionsJson == null ? null : JsonSerializer.Deserialize<List<string>>(d.OptionsJson);

    private static List<string> CleanOptions(IEnumerable<string>? options) =>
        (options ?? Array.Empty<string>())
            .Select(o => Clip((o ?? string.Empty).Trim(), MaxLabelLength))
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxOptions)
            .ToList();

    /// <summary>"Source channel" becomes "source_channel", then "source_channel_2" if taken.</summary>
    private static string UniqueKey(string label, IReadOnlySet<string> taken)
    {
        var slug = new StringBuilder();
        foreach (var c in label.ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '_') slug.Append('_');
        }
        var key = Clip(slug.ToString().Trim('_'), 48);
        if (key.Length == 0) key = "field";
        if (!taken.Contains(key)) return key;
        for (var n = 2; ; n++)
            if (!taken.Contains($"{key}_{n}")) return $"{key}_{n}";
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
