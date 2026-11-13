using System.Globalization;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Custom fields: the definitions a workspace adds to leads, contacts, companies, deals
/// and members, and each record's values for them.
/// </summary>
/// <remarks>
/// Definitions shape every record in the workspace, so only those who manage its settings
/// change them, as with pipelines and lead routing. Anyone who can edit a record can fill
/// in its values. Every value is checked against its field before it is stored, in the
/// typed row its kind is kept in (<see cref="CustomFieldValues"/>).
/// </remarks>
public sealed class CustomFieldService : ICustomFieldService
{
    private const int MaxLabelLength = 120;
    private const int MaxOptions = 50;
    private const int MaxTextLength = 2000;

    /// <summary>Most records one batch read of values answers for: a page of a list, or a club's members a screen at a time.</summary>
    public const int MaxBatch = 500;

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
        if (type != null && !Carries(type, organizationId))
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
        if (entityType == null || !Carries(entityType, organizationId))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.InvalidEntityType);
        var type = Blank(request.Type);
        if (type == null || !CustomFieldTypes.All.Contains(type))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.InvalidType);
        var label = Blank(request.Label);
        if (label == null)
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.LabelRequired);

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
            Required = request.Required ?? false,
            Order = existing.Count == 0 ? 0 : existing.Max(d => d.Order) + 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        var shaped = await ShapeAsync(definition, organizationId, request.Options, request.Currency, request.LinkEntityType, ct);
        if (shaped.IsFailure) return Result.Failure<CustomFieldDefinitionDto>(shaped.Error);
        if (request.DefaultValue is { } raw)
        {
            var defaulted = await DefaultOfAsync(definition, raw, userId, organizationId, ct);
            if (defaulted.IsFailure) return Result.Failure<CustomFieldDefinitionDto>(defaulted.Error);
            definition.DefaultValueJson = defaulted.Value;
        }

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

        // Values belong to records of the field's kind; moving it would leave them on the wrong ones.
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
        var linkType = Blank(request.LinkEntityType) ?? definition.LinkEntityType;
        // A value is kept in the column of its kind, and a link points at one kind of record: with
        // values entered, changing either would leave them unreadable. Deleting the field and adding
        // it again starts afresh, and says so.
        var changesKind = type != definition.Type || (type == CustomFieldTypes.Link && linkType != definition.LinkEntityType);
        if (changesKind && await _repository.HasValuesAsync(definition.Id, ct))
            return Result.Failure<CustomFieldDefinitionDto>(DomainErrors.CustomField.TypeChangeConflict);
        if (changesKind) definition.DefaultValueJson = null;
        definition.Type = type;

        var options = request.Options ?? (CustomFieldTypes.HasOptions(type) ? CustomFieldValues.Options(definition) : null);
        var shaped = await ShapeAsync(definition, organizationId, options, request.Currency ?? definition.Currency, linkType, ct);
        if (shaped.IsFailure) return Result.Failure<CustomFieldDefinitionDto>(shaped.Error);
        if (request.Required is { } required) definition.Required = required;

        if (request.Clears(nameof(UpdateCustomFieldRequest.DefaultValue))) definition.DefaultValueJson = null;
        else if (request.DefaultValue is { } raw)
        {
            var defaulted = await DefaultOfAsync(definition, raw, userId, organizationId, ct);
            if (defaulted.IsFailure) return Result.Failure<CustomFieldDefinitionDto>(defaulted.Error);
            definition.DefaultValueJson = defaulted.Value;
        }
        else if (definition.DefaultValueJson != null && !Normalize(definition, Parse(definition.DefaultValueJson)).Ok)
        {
            // Options changed under the default: a default the field now refuses is dropped rather than kept to fail every new record.
            definition.DefaultValueJson = null;
        }

        await _repository.SaveDefinitionAsync(definition, ct);
        return Result.Success(ToDto(definition));
    }

    public async Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ReorderAsync(
        Guid userId, Guid? organizationId, ReorderCustomFieldsRequest request, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct))
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainErrors.CustomField.NotOwnerOrManager);
        var entityType = Blank(request.EntityType);
        if (entityType == null || !Carries(entityType, organizationId))
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainErrors.CustomField.InvalidEntityType);

        var fields = await _repository.TrackedDefinitionsAsync(userId, organizationId, entityType, ct);
        var ids = request.Ids ?? Array.Empty<Guid>();
        if (ids.Count != fields.Count || ids.Distinct().Count() != ids.Count || ids.Any(i => fields.All(f => f.Id != i)))
            return Result.Failure<IReadOnlyList<CustomFieldDefinitionDto>>(DomainErrors.CustomField.ReorderInvalid);
        for (var i = 0; i < ids.Count; i++) fields.First(f => f.Id == ids[i]).Order = i;
        if (fields.Count > 0) await _repository.SaveDefinitionAsync(fields[0], ct);
        return Result.Success<IReadOnlyList<CustomFieldDefinitionDto>>(fields.OrderBy(f => f.Order).Select(ToDto).ToList());
    }

    public async Task<Result> DeleteDefinitionAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        if (!await CanManageAsync(userId, organizationId, ct))
            return Result.Failure(DomainErrors.CustomField.NotOwnerOrManager);
        var definition = await _repository.FindDefinitionAsync(id, userId, organizationId, ct);
        if (definition == null) return Result.Failure(DomainErrors.CustomField.NotFound);
        // Its values go with it, so a field added later under the same label starts empty: the old
        // values used to come back, typed for the field that was deleted, and block saves.
        await _repository.DeleteDefinitionAsync(definition, ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyDictionary<string, JsonElement>>> GetValuesAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default)
    {
        var type = Blank(entityType);
        if (type == null || !Carries(type, organizationId))
            return Result.Failure<IReadOnlyDictionary<string, JsonElement>>(DomainErrors.CustomField.InvalidEntityType);

        // Only a record the caller can see, so a private deal's values stay with it.
        if (!await _records.ExistsAsync(type, recordId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyDictionary<string, JsonElement>>(DomainErrors.CustomField.RecordNotFound);

        var all = await ReadAsync(userId, organizationId, type, new[] { recordId }, ct);
        return Result.Success(all.TryGetValue(recordId, out var values) ? values : new Dictionary<string, JsonElement>());
    }

    public async Task<Result<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>>> GetValuesBatchAsync(
        Guid userId, Guid? organizationId, CustomFieldValuesBatchRequest request, CancellationToken ct = default)
    {
        var type = Blank(request.EntityType);
        if (type == null || !Carries(type, organizationId))
            return Result.Failure<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>>(DomainErrors.CustomField.InvalidEntityType);
        var ids = (request.RecordIds ?? Array.Empty<Guid>()).Distinct().Take(MaxBatch).ToList();
        // Only the records the caller can see: a private deal on another's board answers nothing.
        var visible = await _records.VisibleAsync(type, ids, userId, organizationId, ct);
        return Result.Success(await ReadAsync(userId, organizationId, type, visible.ToList(), ct));
    }

    public async Task<Result> SaveValuesAsync(
        Guid userId, Guid? organizationId, SaveCustomFieldValuesRequest request, CancellationToken ct = default)
    {
        var type = Blank(request.EntityType);
        if (type == null || !Carries(type, organizationId))
            return Result.Failure(DomainErrors.CustomField.InvalidEntityType);
        if (!await _records.ExistsAsync(type, request.RecordId, userId, organizationId, ct))
            return Result.Failure(DomainErrors.CustomField.RecordNotFound);

        var definitions = (await _repository.ListDefinitionsAsync(userId, organizationId, type, ct))
            .ToDictionary(d => d.Key, StringComparer.Ordinal);
        var filled = (await _repository.EntriesAsync(type, new[] { request.RecordId }, ct)).Select(e => e.FieldId).ToHashSet();

        // Only the fields sent change, so a client sending what it edited leaves the rest, and a
        // field whose options changed since cannot block saving another.
        var wanted = new Dictionary<CustomFieldDefinition, List<CustomFieldEntry>>();
        foreach (var (key, raw) in request.Values ?? new Dictionary<string, JsonElement>())
        {
            // A key with no definition is a field deleted since the form loaded; nothing to store.
            if (!definitions.TryGetValue(key, out var definition)) continue;
            var (ok, value, expected) = Normalize(definition, raw);
            if (!ok) return Result.Failure(DomainErrors.CustomField.InvalidValue(definition.Label, expected));
            // A required field that has a value cannot be emptied. One still empty (a record from
            // before the field was required, an import's empty cell) stays empty without blocking the rest.
            if (definition.Required && CustomFieldValues.IsEmpty(value))
            {
                if (filled.Contains(definition.Id)) return Result.Failure(DomainErrors.CustomField.IsRequired(definition.Label));
                continue;
            }
            if (!await LinkExistsAsync(definition, value, userId, organizationId, ct))
                return Result.Failure(DomainErrors.CustomField.LinkNotFound(definition.Label));
            wanted[definition] = CustomFieldValues.Rows(definition, value);
        }
        if (wanted.Count == 0) return Result.Success();

        return await _repository.ApplyEntriesAsync(type, request.RecordId, organizationId, userId, wanted, ct)
            ? Result.Success()
            : Result.Failure(DomainErrors.CustomField.ValuesConflict);
    }

    /// <summary>The values of these records, keyed by record then field key; links carry the name of the record they point at.</summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>> ReadAsync(
        Guid userId, Guid? organizationId, string entityType, IReadOnlyCollection<Guid> recordIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>();
        if (recordIds.Count == 0) return result;
        var definitions = await _repository.ListDefinitionsAsync(userId, organizationId, entityType, ct);
        var byId = definitions.ToDictionary(d => d.Id);
        var entries = (await _repository.EntriesAsync(entityType, recordIds, ct)).Where(e => byId.ContainsKey(e.FieldId)).ToList();

        var names = new Dictionary<Guid, string>();
        foreach (var group in entries.Where(e => e.LinkId != null).GroupBy(e => byId[e.FieldId].LinkEntityType ?? ""))
            foreach (var (id, name) in await _records.NamesAsync(group.Key, group.Select(e => e.LinkId!.Value).ToList(), userId, organizationId, ct))
                names[id] = name;

        foreach (var record in entries.GroupBy(e => e.RecordId))
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in record.GroupBy(e => e.FieldId))
            {
                var definition = byId[field.Key];
                var value = CustomFieldValues.Read(definition, field.ToList());
                if (definition.Type == CustomFieldTypes.Link && field.First().LinkId is Guid linked)
                    // A link to a record the caller cannot see keeps its id but no name.
                    value = JsonSerializer.SerializeToElement(new { id = linked, name = names.TryGetValue(linked, out var n) ? n : null });
                if (!CustomFieldValues.IsEmpty(value)) values[definition.Key] = value;
            }
            result[record.Key] = values;
        }
        return result;
    }

    private async Task<bool> LinkExistsAsync(CustomFieldDefinition definition, JsonElement value, Guid userId, Guid? organizationId, CancellationToken ct) =>
        definition.Type != CustomFieldTypes.Link || CustomFieldValues.IsEmpty(value)
        || await _records.ExistsAsync(definition.LinkEntityType ?? "", value.GetProperty("id").GetGuid(), userId, organizationId, ct);

    /// <summary>Sets the parts of a field that depend on its kind: options, currency, the kind of record it links to.</summary>
    private async Task<Result> ShapeAsync(
        CustomFieldDefinition definition, Guid? organizationId, IEnumerable<string>? options, string? currency, string? linkEntityType, CancellationToken ct)
    {
        definition.OptionsJson = null;
        definition.Currency = null;
        definition.LinkEntityType = null;
        switch (definition.Type)
        {
            case CustomFieldTypes.Select or CustomFieldTypes.MultiSelect:
                var cleaned = CleanOptions(options);
                if (cleaned.Count == 0) return Result.Failure(DomainErrors.CustomField.OptionsRequired);
                definition.OptionsJson = JsonSerializer.Serialize(cleaned);
                break;
            case CustomFieldTypes.Money:
                if (Blank(currency) is { } given)
                {
                    if (!RegionalSettings.TryCurrency(given, out var code)) return Result.Failure(DomainErrors.CustomField.InvalidCurrency);
                    definition.Currency = code;
                }
                else
                {
                    // The workspace's own currency, as for its deals: CHF for a Swiss club.
                    var org = organizationId is { } orgId ? await _organizations.GetByIdAsync(orgId, ct) : null;
                    definition.Currency = org?.Currency ?? RegionalSettings.DefaultCurrency;
                }
                break;
            case CustomFieldTypes.Link:
                var linkType = Blank(linkEntityType);
                if (linkType == null || !Carries(linkType, organizationId)) return Result.Failure(DomainErrors.CustomField.LinkTypeRequired);
                definition.LinkEntityType = linkType;
                break;
        }
        return Result.Success();
    }

    private async Task<Result<string?>> DefaultOfAsync(CustomFieldDefinition definition, JsonElement raw, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var (ok, value, expected) = Normalize(definition, raw);
        if (!ok) return Result.Failure<string?>(DomainErrors.CustomField.InvalidDefault(definition.Label, expected));
        if (!await LinkExistsAsync(definition, value, userId, organizationId, ct))
            return Result.Failure<string?>(DomainErrors.CustomField.LinkNotFound(definition.Label));
        return Result.Success<string?>(CustomFieldValues.IsEmpty(value) ? null : value.GetRawText());
    }

    private async Task<bool> CanManageAsync(Guid userId, Guid? organizationId, CancellationToken ct)
    {
        // A personal workspace is its owner's alone.
        if (organizationId is not { } orgId) return true;
        return await _organizations.HasPermissionAsync(userId, orgId, Domain.Common.MemberPermissions.ManageSettings, ct);
    }

    /// <summary>Whether records of this kind carry fields here: members only in an organization.</summary>
    private static bool Carries(string entityType, Guid? organizationId) =>
        RecordTypes.WithFields.Contains(entityType) && (entityType != RecordTypes.Member || organizationId != null);

    /// <summary>A value checked against its field and written as that kind's JSON (see <see cref="CustomFieldValues"/>); strings that are empty become null.</summary>
    /// <remarks>Internal so an import checks each cell by this same rule before it writes anything.</remarks>
    internal static (bool Ok, JsonElement Value, string Expected) Normalize(CustomFieldDefinition definition, JsonElement raw)
    {
        var none = JsonSerializer.SerializeToElement<object?>(null);
        if (raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return (true, none, "");

        switch (definition.Type)
        {
            case CustomFieldTypes.Number:
                return Number(raw) switch
                {
                    (true, null) => (true, none, ""),
                    (true, decimal n) when Math.Abs(n) < 1e20m => (true, JsonSerializer.SerializeToElement(Math.Round(n, 8)), ""),
                    _ => (false, none, "must be a number"),
                };

            case CustomFieldTypes.Money:
            {
                // An amount alone is in the field's currency; {"amount", "currency"} names its own.
                var amountOf = raw;
                var currency = definition.Currency ?? RegionalSettings.DefaultCurrency;
                if (raw.ValueKind == JsonValueKind.Object)
                {
                    if (!raw.TryGetProperty("amount", out amountOf)) return (true, none, "");
                    if (raw.TryGetProperty("currency", out var given) && given.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(given.GetString()))
                    {
                        if (!RegionalSettings.TryCurrency(given.GetString(), out var code)) return (false, none, "must name a currency such as CHF");
                        currency = code;
                    }
                }
                return Number(amountOf) switch
                {
                    (true, null) => (true, none, ""),
                    (true, decimal n) when Math.Abs(n) < 1e16m =>
                        (true, JsonSerializer.SerializeToElement(new { amount = Math.Round(n, 2, MidpointRounding.AwayFromZero), currency }), ""),
                    _ => (false, none, "must be an amount"),
                };
            }

            case CustomFieldTypes.Checkbox:
                return raw.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? (true, raw.Clone(), "")
                    : (false, none, "must be ticked or not");

            case CustomFieldTypes.MultiSelect:
            {
                var options = CustomFieldValues.Options(definition);
                var chosen = new List<string>();
                var items = raw.ValueKind switch
                {
                    JsonValueKind.Array => raw.EnumerateArray().ToList(),
                    JsonValueKind.String => new List<JsonElement> { raw },
                    _ => null,
                };
                if (items == null) return (false, none, "must be a list of its options");
                foreach (var item in items)
                {
                    if (item.ValueKind != JsonValueKind.String) return (false, none, "must be a list of its options");
                    var text = item.GetString()!.Trim();
                    if (text.Length == 0) continue;
                    var match = options.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
                    if (match == null) return (false, none, "must be chosen from its options");
                    if (!chosen.Contains(match, StringComparer.OrdinalIgnoreCase)) chosen.Add(match);
                }
                return chosen.Count == 0 ? (true, none, "") : (true, JsonSerializer.SerializeToElement(chosen), "");
            }

            case CustomFieldTypes.Link:
            {
                var idText = raw.ValueKind switch
                {
                    JsonValueKind.String => raw.GetString(),
                    JsonValueKind.Object when raw.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String => id.GetString(),
                    JsonValueKind.Object when raw.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Null => "",
                    _ => null,
                };
                if (idText == null) return (false, none, "must point at a record");
                if (idText.Trim().Length == 0) return (true, none, "");
                return Guid.TryParse(idText, out var guid) && guid != Guid.Empty
                    ? (true, JsonSerializer.SerializeToElement(new { id = guid }), "")
                    : (false, none, "must point at a record");
            }
        }

        if (raw.ValueKind != JsonValueKind.String) return (false, none, "must be text");
        var text2 = raw.GetString()!.Trim();
        if (text2.Length == 0) return (true, none, "");

        switch (definition.Type)
        {
            case CustomFieldTypes.Date:
                return DateOnly.TryParseExact(text2, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? (true, JsonSerializer.SerializeToElement(text2), "")
                    : (false, none, "must be a date");
            case CustomFieldTypes.Select:
                var option = CustomFieldValues.Options(definition).FirstOrDefault(o => string.Equals(o, text2, StringComparison.Ordinal));
                return option != null
                    ? (true, JsonSerializer.SerializeToElement(option), "")
                    : (false, none, "must be one of its options");
            case CustomFieldTypes.Url:
                // People type "www.example.com"; read it the way a browser would.
                var candidate = text2.Contains("://", StringComparison.Ordinal) ? text2 : $"https://{text2}";
                return candidate.Length <= MaxTextLength
                       && Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                       && uri.Host.Contains('.', StringComparison.Ordinal)
                    ? (true, JsonSerializer.SerializeToElement(candidate), "")
                    : (false, none, "must be a web address");
            default:
                return text2.Length <= MaxTextLength
                    ? (true, JsonSerializer.SerializeToElement(text2), "")
                    : (false, none, $"must be {MaxTextLength:N0} characters or fewer");
        }
    }

    /// <summary>A number sent as a number or as text with a point for decimals: (true, null) for an empty text, (false, _) for anything else.</summary>
    private static (bool Ok, decimal? Value) Number(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.Number) return raw.TryGetDecimal(out var n) ? (true, n) : (false, null);
        if (raw.ValueKind != JsonValueKind.String) return (false, null);
        var s = raw.GetString()!.Trim();
        if (s.Length == 0) return (true, null);
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? (true, parsed) : (false, null);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static CustomFieldDefinitionDto ToDto(CustomFieldDefinition d) =>
        new(d.Id, d.EntityType, d.Key, d.Label, d.Type, CustomFieldTypes.HasOptions(d.Type) ? CustomFieldValues.Options(d) : null,
            d.Required, d.Order, d.DefaultValueJson == null ? null : Parse(d.DefaultValueJson), d.Currency, d.LinkEntityType);

    private static List<string> CleanOptions(IEnumerable<string>? options) =>
        (options ?? Array.Empty<string>())
            .Select(o => Clip((o ?? string.Empty).Trim(), MaxLabelLength))
            .Where(o => o.Length > 0)
            // Ignoring case: options are matched that way, and kept once each.
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
