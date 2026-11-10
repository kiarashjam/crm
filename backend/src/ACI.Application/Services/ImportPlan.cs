using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>A record already in the workspace, as an import compares a row with it.</summary>
/// <param name="Values">Its fields by import field key; "company" holds the name of its company.</param>
/// <param name="Custom">Its custom field values by field key, without the "custom:" prefix.</param>
/// <param name="Locked">Why an import may not update it, worded to follow its name; null when it may.</param>
internal sealed record ImportExisting(
    Guid Id,
    string Label,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, JsonElement> Custom,
    string? Locked = null);

/// <summary>A company already in the workspace, for linking a lead or a contact to it.</summary>
internal sealed record ImportCompany(Guid Id, string Name, string? Domain);

/// <summary>What one row of a file does: the plan a dry run shows and the import carries out.</summary>
internal sealed class ImportPlannedRow
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Skip = "skip";
    public const string Error = "error";

    public int Row { get; init; }
    public string Label { get; set; } = "";
    public string Action { get; set; } = Create;
    public string? Message { get; set; }
    /// <summary>The record matched, then the one created or updated.</summary>
    public Guid? RecordId { get; set; }

    /// <summary>The fields to write, by key. For an update, only the ones that change.</summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    /// <summary>Custom field values to write, by field key. For an update, only the ones that change.</summary>
    public Dictionary<string, JsonElement> Custom { get; } = new(StringComparer.Ordinal);

    /// <summary>The company to link, by name: one already here, or one this import adds.</summary>
    public string? CompanyName { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? SourceId { get; set; }

    public List<string> Changes { get; } = new();
    public List<string> Notes { get; } = new();

    public void Fail(string message)
    {
        Action = Error;
        Message = message;
    }

    public ImportRowDto ToDto() => new(Row, Action, Label, Message, RecordId, Changes.ToList(), Notes.ToList());
}

/// <summary>
/// Works out what every row of a file does, without writing anything.
/// </summary>
/// <remarks>
/// <para>
/// A row matches a record already here by its email, or by its name and phone number
/// together; a company by its name or its domain. The phone alone is not enough: a
/// couple share a landline, and so do the colleagues at a front desk, and the second
/// of them would have been skipped, or written over the first. Such a row is added,
/// with a note naming whoever has its number, so the dry run shows where a duplicate
/// may be. A row that repeats an earlier row of the same file is skipped and says which.
/// </para>
/// <para>
/// A matching row is skipped, or with "update" it writes its values over the record's.
/// Only cells with something in them count: an empty cell never clears a field, and a
/// "no" never clears a contact's "do not contact".
/// </para>
/// </remarks>
internal sealed class ImportPlanner
{
    private readonly string _kind;
    private readonly IReadOnlyList<(int Column, ImportField Field)> _columns;
    private readonly IReadOnlyDictionary<string, ImportField> _fields;
    private readonly bool _update;
    private readonly IReadOnlyList<LeadStatus>? _statuses;
    private readonly IReadOnlyList<LeadSource> _sources;
    private readonly (string Name, Guid? Id) _defaultStatus;
    private readonly (string Name, Guid? Id)? _defaultSource;

    private readonly Dictionary<string, List<ImportExisting>> _existing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportExisting> _existingByPhone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportCompany> _companyByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportCompany> _companyByDomain = new(StringComparer.Ordinal);

    // Across the rows of the file: what earlier rows were, the records they matched, the
    // phone numbers of the people they add, and the companies they add.
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, int> _matched = new();
    private readonly Dictionary<string, int> _phonesAdded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _companiesAdded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Name, int Row)> _companiesFromEmail = new(StringComparer.Ordinal);

    /// <param name="columns">Each mapped column and the field it goes into.</param>
    /// <param name="statuses">The workspace's lead statuses; null for a personal workspace, which keeps any status as written.</param>
    public ImportPlanner(
        string kind,
        IReadOnlyList<(int Column, ImportField Field)> columns,
        IReadOnlyList<ImportField> fields,
        bool update,
        IReadOnlyList<ImportExisting> existing,
        IReadOnlyList<ImportCompany> companies,
        IReadOnlyList<LeadStatus>? statuses,
        IReadOnlyList<LeadSource> sources,
        string? defaultStatus,
        string? defaultSource)
    {
        _kind = kind;
        _columns = columns;
        _fields = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        _update = update;
        _statuses = statuses;
        _sources = sources;

        foreach (var record in existing)
            foreach (var (key, _) in MatchKeys(k => record.Values.GetValueOrDefault(k)))
            {
                if (!_existing.TryGetValue(key, out var list)) _existing[key] = list = new List<ImportExisting>();
                list.Add(record);
            }
        // The records come oldest first, so a number several people share names the oldest.
        if (IsPerson)
            foreach (var record in existing)
                if (ImportValues.PhoneKey(record.Values.GetValueOrDefault("phone")) is { Length: > 0 } phone)
                    _existingByPhone.TryAdd(phone, record);
        foreach (var company in companies)
        {
            _companyByName.TryAdd(ImportValues.NameKey(company.Name), company);
            if (ImportValues.DomainKey(company.Domain) is { Length: > 0 } domain) _companyByDomain.TryAdd(domain, company);
        }

        _defaultStatus = DefaultStatus(defaultStatus);
        if (!string.IsNullOrWhiteSpace(defaultSource)) _defaultSource = ResolveSource(defaultSource.Trim());
    }

    private bool IsPerson => _kind is RecordTypes.Lead or RecordTypes.Contact;

    private string Nouns => _kind switch
    {
        RecordTypes.Lead => "leads",
        RecordTypes.Contact => "contacts",
        _ => "companies",
    };

    private string Noun => _kind switch
    {
        RecordTypes.Lead => "lead",
        RecordTypes.Contact => "contact",
        _ => "company",
    };

    private void Remember(List<(string Key, string How)> keys, int row)
    {
        foreach (var (key, _) in keys) _seen[key] = row;
    }

    public IReadOnlyList<ImportPlannedRow> Plan(IReadOnlyList<CsvRow> rows) => rows.Select(PlanRow).ToList();

    private ImportPlannedRow PlanRow(CsvRow row)
    {
        var planned = new ImportPlannedRow { Row = row.Number };
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (column, field) in _columns)
        {
            var cell = row.Cell(column);
            if (cell.Length > 0) cells[field.Key] = cell;
        }

        // A first and a last name column make the name, unless the file has a whole one too.
        var first = cells.GetValueOrDefault("firstName");
        var last = cells.GetValueOrDefault("lastName");
        cells.Remove("firstName");
        cells.Remove("lastName");
        if (!cells.ContainsKey("name") && (first != null || last != null))
            cells["name"] = string.Join(' ', new[] { first, last }.Where(p => p != null));

        planned.Label = cells.GetValueOrDefault("name") ?? cells.GetValueOrDefault("email") ?? "(no name)";

        var errors = new List<string>();
        // A company is matched by its name, so it always needs one. A person needs one only
        // to be added: a file of emails and new phone numbers can update the people it names.
        if (!IsPerson && !cells.ContainsKey("name")) errors.Add("No company name");
        if (IsPerson && !cells.ContainsKey("email")) errors.Add("No email address");
        foreach (var (key, text) in cells)
        {
            var field = _fields[key];
            if (field.Value == ImportValue.Custom)
            {
                var (value, error) = ImportValues.Custom(field.Definition!, text);
                if (error != null) errors.Add(error);
                else if (value is { ValueKind: not JsonValueKind.Null } v) planned.Custom[field.Definition!.Key] = v;
                continue;
            }
            if (Read(field, text, out var read) is { } problem) errors.Add(problem);
            else if (read != null) planned.Values[key] = read;
        }
        if (errors.Count > 0)
        {
            planned.Fail(string.Join("; ", errors));
            return planned;
        }

        var keys = MatchKeys(k => planned.Values.GetValueOrDefault(k));

        // A row the file has already had: the first one stands.
        foreach (var (key, how) in keys)
        {
            if (!_seen.TryGetValue(key, out var earlier)) continue;
            planned.Action = ImportPlannedRow.Skip;
            planned.Message = $"Same {how} as row {earlier}";
            return planned;
        }

        ImportExisting? match = null;
        string? matchedBy = null;
        var sharing = 0;
        foreach (var (key, how) in keys)
        {
            if (!_existing.TryGetValue(key, out var found)) continue;
            match = found[0];
            matchedBy = how;
            sharing = found.Count;
            break;
        }

        if (match == null)
        {
            // Refused without being remembered, so a later row with the name can still add them.
            if (!planned.Values.ContainsKey("name"))
            {
                planned.Fail("No name, and nobody here has this email to update");
                return planned;
            }
            Remember(keys, row.Number);
            PlanCreate(planned);
            NoteSharedPhone(planned);
            return planned;
        }

        planned.RecordId = match.Id;
        // Two rows can reach one record by different keys (one by email, one by name and
        // phone). The second would write over the first, so the first one stands.
        if (_matched.TryGetValue(match.Id, out var sameAs))
        {
            planned.Action = ImportPlannedRow.Skip;
            planned.Message = $"Same {Noun} as row {sameAs}: {match.Label}";
            return planned;
        }
        Remember(keys, row.Number);
        _matched[match.Id] = row.Number;
        if (!_update)
        {
            planned.Action = ImportPlannedRow.Skip;
            planned.Message = $"Already in your {Nouns}: {match.Label} (same {matchedBy})";
            return planned;
        }
        if (match.Locked != null)
        {
            planned.Action = ImportPlannedRow.Skip;
            planned.Message = $"{match.Label} {match.Locked}";
            return planned;
        }

        PlanUpdate(planned, match);
        if (planned.Changes.Count == 0)
        {
            planned.Action = ImportPlannedRow.Skip;
            planned.Message = $"Already up to date: {match.Label}";
            return planned;
        }
        planned.Action = ImportPlannedRow.Update;
        planned.Message = $"Updates {match.Label} (same {matchedBy})";
        if (sharing > 1) planned.Notes.Add($"{sharing} {Nouns} already have this {matchedBy}; this updates the oldest");
        return planned;
    }

    /// <summary>Checks a cell against its field. Null when it is fine; <paramref name="value"/> is what to store, or null for nothing.</summary>
    private static string? Read(ImportField field, string text, out string? value)
    {
        value = text;
        if (text.Length > field.MaxLength)
            return $"{field.Label} is longer than {field.MaxLength} characters";
        switch (field.Value)
        {
            case ImportValue.Email:
                return ValidationHelper.IsValidEmail(text) ? null : $"{ImportValues.Show(text)} is not an email address";
            case ImportValue.Phone:
                return ImportValues.IsPhone(text) ? null : $"{ImportValues.Show(text)} is not a phone number";
            case ImportValue.Domain:
                value = ImportValues.DomainKey(text);
                return value.Length > 0 && ValidationHelper.IsValidDomain(value) ? null : $"{ImportValues.Show(text)} is not a domain";
            case ImportValue.YesNo:
                switch (ImportValues.YesNo(text))
                {
                    case true:
                        value = "yes";
                        return null;
                    case false:
                        // Never clears it: someone who asked not to be contacted stays that way.
                        value = null;
                        return null;
                    default:
                        return $"{field.Label} should be yes or no, not {ImportValues.Show(text)}";
                }
            default:
                return null;
        }
    }

    private void PlanCreate(ImportPlannedRow planned)
    {
        planned.Action = ImportPlannedRow.Create;
        if (_kind == RecordTypes.Lead)
        {
            if (planned.Values.TryGetValue("status", out var status))
            {
                var resolved = ResolveStatus(status);
                if (resolved == null)
                    planned.Notes.Add($"Status {ImportValues.Show(status)} is not one of this workspace's, so it is added as {_defaultStatus.Name}");
                (planned.Values["status"], planned.StatusId) = resolved ?? _defaultStatus;
            }
            else
            {
                (planned.Values["status"], planned.StatusId) = _defaultStatus;
            }

            if (planned.Values.TryGetValue("source", out var source))
                (planned.Values["source"], planned.SourceId) = ResolveSource(source);
            else if (_defaultSource is { } fallback)
                (planned.Values["source"], planned.SourceId) = fallback;
        }

        if (planned.Values.TryGetValue("company", out var company))
        {
            PlanCompany(planned, company);
        }
        else if (_kind == RecordTypes.Lead
                 && LeadDomainEnricher.TryCompanyFromEmail(planned.Values["email"], out var domain, out var named))
        {
            // What saving a lead does anyway: a lead from a business address is linked to
            // the company with that domain, which is added if there is none.
            if (_companyByDomain.TryGetValue(domain, out var known))
                planned.Notes.Add($"Linked to the company {ImportValues.Show(known.Name)}, from the email address");
            else if (_companiesFromEmail.TryGetValue(domain, out var added))
                planned.Notes.Add($"Linked to the company {ImportValues.Show(added.Name)}, which row {added.Row} adds");
            else
            {
                _companiesFromEmail[domain] = (named, planned.Row);
                planned.Notes.Add($"Adds the company {ImportValues.Show(named)}, named after the email address");
            }
        }
    }

    /// <summary>
    /// A person added who shares a phone number with someone here, or with someone an
    /// earlier row adds: not a match (see the class remarks), but worth a look.
    /// </summary>
    private void NoteSharedPhone(ImportPlannedRow planned)
    {
        if (!IsPerson || ImportValues.PhoneKey(planned.Values.GetValueOrDefault("phone")) is not { Length: > 0 } phone) return;
        if (_existingByPhone.TryGetValue(phone, out var known))
            planned.Notes.Add($"Same phone number as {ImportValues.Show(known.Label)}, already in your {Nouns}: check they are not the same person");
        else if (_phonesAdded.TryGetValue(phone, out var row))
            planned.Notes.Add($"Same phone number as row {row}: check they are not the same person");
        else
            _phonesAdded[phone] = planned.Row;
    }

    private void PlanUpdate(ImportPlannedRow planned, ImportExisting match)
    {
        if (_kind == RecordTypes.Lead)
        {
            if (planned.Values.TryGetValue("status", out var status))
            {
                if (ResolveStatus(status) is { } resolved)
                    (planned.Values["status"], planned.StatusId) = resolved;
                else
                {
                    planned.Values.Remove("status");
                    planned.Notes.Add($"Status {ImportValues.Show(status)} is not one of this workspace's, so the status stays as it is");
                }
            }
            if (planned.Values.TryGetValue("source", out var source))
                (planned.Values["source"], planned.SourceId) = ResolveSource(source);
        }

        foreach (var key in planned.Values.Keys.ToList())
        {
            var now = planned.Values[key];
            var was = match.Values.GetValueOrDefault(key);
            if (Same(key, was, now))
            {
                planned.Values.Remove(key);
                continue;
            }
            planned.Changes.Add($"{_fields[key].Label}: {ImportValues.Show(was)} → {ImportValues.Show(now)}");
            if (key == "company") PlanCompany(planned, now);
        }
        if (!planned.Values.ContainsKey("status")) planned.StatusId = null;
        if (!planned.Values.ContainsKey("source")) planned.SourceId = null;

        foreach (var key in planned.Custom.Keys.ToList())
        {
            var now = planned.Custom[key];
            if (match.Custom.TryGetValue(key, out var was) && was.GetRawText() == now.GetRawText())
            {
                planned.Custom.Remove(key);
                continue;
            }
            var label = _fields[ImportFields.CustomPrefix + key].Label;
            planned.Changes.Add($"{label}: {ImportValues.Show(match.Custom.TryGetValue(key, out var old) ? Text(old) : null)} → {ImportValues.Show(Text(now))}");
        }
    }

    private void PlanCompany(ImportPlannedRow planned, string name)
    {
        var key = ImportValues.NameKey(name);
        if (_companyByName.TryGetValue(key, out var known))
        {
            planned.CompanyName = known.Name;
            return;
        }
        planned.CompanyName = name;
        if (_companiesAdded.TryGetValue(key, out var row))
            planned.Notes.Add($"Linked to the company {ImportValues.Show(name)}, which row {row} adds");
        else
        {
            _companiesAdded[key] = planned.Row;
            planned.Notes.Add($"Adds the company {ImportValues.Show(name)}");
        }
    }

    /// <summary>The keys a record is matched by, each with how a person would say it.</summary>
    /// <param name="value">The record's value for a field key, or null.</param>
    private List<(string Key, string How)> MatchKeys(Func<string, string?> value)
    {
        var keys = new List<(string, string)>();
        if (IsPerson)
        {
            if (ImportValues.EmailKey(value("email")) is { Length: > 0 } email)
                keys.Add(("email:" + email, "email"));
            var name = ImportValues.NameKey(value("name"));
            var phone = ImportValues.PhoneKey(value("phone"));
            if (name.Length > 0 && phone.Length > 0) keys.Add(($"name+phone:{name}|{phone}", "name and phone"));
        }
        else
        {
            if (ImportValues.NameKey(value("name")) is { Length: > 0 } name)
                keys.Add(("name:" + name, "name"));
            var domain = ImportValues.DomainKey(value("domain"));
            if (domain.Length == 0) domain = ImportValues.DomainKey(value("website"));
            if (domain.Length > 0) keys.Add(("domain:" + domain, "domain"));
        }
        return keys;
    }

    private static bool Same(string key, string? was, string now) => key switch
    {
        "email" => ImportValues.EmailKey(was) == ImportValues.EmailKey(now),
        // The same number written another way is not a change: "079 123 45 67" stays as typed.
        "phone" => ImportValues.PhoneKey(now) is { Length: > 0 } phone
            ? ImportValues.PhoneKey(was) == phone
            : string.Equals(was?.Trim(), now, StringComparison.Ordinal),
        // Letter case and spacing alone are not a rename: "beta ag" in a file leaves "Beta AG" as it is.
        "name" or "company" => ImportValues.NameKey(was) == ImportValues.NameKey(now),
        "domain" => ImportValues.DomainKey(was) == ImportValues.DomainKey(now),
        "status" or "source" => string.Equals(was?.Trim(), now, StringComparison.OrdinalIgnoreCase),
        _ => string.Equals(was?.Trim(), now, StringComparison.Ordinal),
    };

    private (string Name, Guid? Id)? ResolveStatus(string text)
    {
        // A personal workspace has no status list of its own, and keeps what it is given.
        if (_statuses == null) return (text, null);
        var status = _statuses.FirstOrDefault(s => string.Equals(s.Name.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));
        return status == null ? null : (status.Name, status.Id);
    }

    private (string Name, Guid? Id) DefaultStatus(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && ResolveStatus(requested.Trim()) is { } chosen) return chosen;
        if (_statuses is { Count: > 0 })
        {
            var first = _statuses.FirstOrDefault(s => string.Equals(s.Name.Trim(), "New", StringComparison.OrdinalIgnoreCase))
                ?? _statuses.OrderBy(s => s.DisplayOrder).First();
            return (first.Name, first.Id);
        }
        return ("New", null);
    }

    private (string Name, Guid? Id) ResolveSource(string text)
    {
        var source = _sources.FirstOrDefault(s => string.Equals(s.Name.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));
        return source == null ? (text, null) : (source.Name, source.Id);
    }

    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => "yes",
        JsonValueKind.False => "no",
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => value.GetRawText(),
    };
}
