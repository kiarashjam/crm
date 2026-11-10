using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Imports a spreadsheet of leads, contacts or companies on the server, in one batch that
/// can be undone.
/// </summary>
/// <remarks>
/// <para>
/// The lead import used to run in the browser: it split the file itself (and broke on
/// the ";" Swiss Excel writes), dropped the company column it had mapped, and posted
/// one lead at a time, so a dry run was impossible and a wrong file could only be
/// cleaned up by hand. Contacts and companies had no import at all.
/// </para>
/// <para>
/// Every record goes through the same service the app's own forms use, so it is checked
/// and stored exactly as one typed in would be: a lead is routed, scored and given its
/// contact. A lead from an import sets off no automations, webhook or first-response
/// clock unless the person importing asks for the automations (see
/// <see cref="CreateLeadRequest.Origin"/>).
/// </para>
/// </remarks>
public sealed class ImportService : IImportService
{
    /// <summary>The largest file accepted.</summary>
    public const int MaxFileMegabytes = 5;

    /// <summary>
    /// The most data rows one file may have. The import runs while its request waits, and
    /// a lead takes some 15 ms to save on SQL Server with everything a lead's save does, so
    /// this keeps a file well inside the two to four minutes a hosting front end allows.
    /// </summary>
    public const int MaxRows = 2000;

    private const int SampleValues = 3;
    private const int ListedBatches = 20;
    private const int RowsPerTransaction = 50;

    /// <summary>
    /// An unfinished import that has saved nothing for this long has stopped (the server
    /// restarted part way), rather than still running. It saves after every run of rows,
    /// which takes seconds, or a minute or two when each lead's automations send an email.
    /// </summary>
    /// <remarks>
    /// Counted from its last save, not from its start: such an import can take far longer
    /// than this in all, and counted from the start it was called stopped, and could be
    /// undone, while it was still adding rows.
    /// </remarks>
    private static readonly TimeSpan StillRunningFor = TimeSpan.FromMinutes(10);

    private static bool Running(ImportBatch batch) =>
        batch.FinishedAtUtc == null && (batch.LastSavedAtUtc ?? batch.StartedAtUtc) > DateTime.UtcNow - StillRunningFor;

    private readonly IImportRepository _imports;
    private readonly ILeadService _leads;
    private readonly IContactService _contacts;
    private readonly ICompanyService _companies;
    private readonly ICustomFieldRepository _customFields;
    private readonly ICustomFieldService _customFieldValues;
    private readonly ILeadStatusRepository _statuses;
    private readonly ILeadSourceRepository _sources;
    private readonly IOrganizationRepository _organizations;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IImportRepository imports,
        ILeadService leads,
        IContactService contacts,
        ICompanyService companies,
        ICustomFieldRepository customFields,
        ICustomFieldService customFieldValues,
        ILeadStatusRepository statuses,
        ILeadSourceRepository sources,
        IOrganizationRepository organizations,
        ILogger<ImportService> logger)
    {
        _imports = imports;
        _leads = leads;
        _contacts = contacts;
        _companies = companies;
        _customFields = customFields;
        _customFieldValues = customFieldValues;
        _statuses = statuses;
        _sources = sources;
        _organizations = organizations;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ImportAnalysisDto>> AnalyzeAsync(Guid userId, Guid? organizationId, ImportFileRequest request, CancellationToken ct = default)
    {
        var kind = Kind(request.Kind);
        if (kind == null) return DomainErrors.Import.InvalidKind;
        var read = ReadFile(request.Content);
        if (read.IsFailure) return read.Error;
        var file = read.Value;

        var fields = ImportFields.For(kind, await _customFields.ListDefinitionsAsync(userId, organizationId, kind, ct));
        var suggested = ImportFields.Suggest(file.Headers, fields);
        var columns = file.Headers.Select((header, i) => new ImportColumnDto(
            i,
            header,
            file.Rows.Select(r => r.Cell(i)).Where(c => c.Length > 0).Take(SampleValues).ToList(),
            suggested[i])).ToList();

        return new ImportAnalysisDto(kind, file.SeparatorName, file.Encoding, file.Rows.Count, columns, fields.Select(f => f.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<ImportResultDto>> RunAsync(Guid userId, Guid? organizationId, ImportRequest request, bool dryRun, CancellationToken ct = default)
    {
        var kind = Kind(request.Kind);
        if (kind == null) return DomainErrors.Import.InvalidKind;
        var read = ReadFile(request.Content);
        if (read.IsFailure) return read.Error;
        var file = read.Value;

        var fields = ImportFields.For(kind, await _customFields.ListDefinitionsAsync(userId, organizationId, kind, ct));
        bool update;
        switch (request.Duplicates?.Trim().ToLowerInvariant())
        {
            case null or "" or "skip": update = false; break;
            case "update": update = true; break;
            default: return DomainErrors.Import.InvalidDuplicates;
        }
        var columns = Columns(kind, request.Columns, file, fields, update);
        if (columns.IsFailure) return columns.Error;

        // What is here already, read once for the whole file.
        var companies = await _imports.CompaniesAsync(userId, organizationId, ct);
        var withCustom = update && columns.Value.Any(c => c.Field.Value == ImportValue.Custom);
        var existing = await ExistingAsync(kind, userId, organizationId, companies, withCustom, ct);
        IReadOnlyList<LeadStatus>? statuses = null;
        IReadOnlyList<LeadSource> sources = Array.Empty<LeadSource>();
        if (kind == RecordTypes.Lead && organizationId is Guid orgId)
        {
            // With no list of its own there is nothing to check a status against, and it is kept as written.
            var own = (await _statuses.GetByOrganizationIdAsync(orgId, ct)).OrderBy(s => s.DisplayOrder).ToList();
            statuses = own.Count > 0 ? own : null;
            sources = await _sources.GetByOrganizationIdAsync(orgId, ct);
        }

        var planner = new ImportPlanner(
            kind, columns.Value, fields, update, existing,
            companies.Select(c => new ImportCompany(c.Id, c.Name, c.Domain)).ToList(),
            statuses, sources, request.DefaultStatus, request.DefaultSource);
        var plan = planner.Plan(file.Rows);
        // Checked as the API checks a form's request, in the dry run as in the import, so
        // the preview never promises a row the import would then refuse.
        foreach (var row in plan.Where(r => r.Action is ImportPlannedRow.Create or ImportPlannedRow.Update))
            if (Invalid(Request(kind, row, companyId: null, runAutomations: false)) is { } invalid) row.Fail(invalid.Description);
        if (dryRun) return Outcome(null, true, kind, file, plan);

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            EntityType = kind,
            FileName = FileName(request.FileName),
            DuplicateMode = update ? "update" : "skip",
            RanAutomations = kind == RecordTypes.Lead && request.RunAutomations,
            TotalRows = file.Rows.Count,
            StartedAtUtc = DateTime.UtcNow,
        };
        // Not the request's token from here on: someone closing the tab half way through
        // would otherwise leave half a file imported. Finishing it leaves one batch to
        // keep or to undo.
        await _imports.AddBatchAsync(batch, CancellationToken.None);
        _logger.LogInformation("Import {BatchId}: {Rows} {Kind} rows from {FileName} for user {UserId}",
            batch.Id, file.Rows.Count, kind, batch.FileName, userId);

        // The rows done so far, from the top of the plan; the ones after it are untouched.
        var done = 0;
        using (_imports.StampNewRecords(batch.Id))
        {
            try
            {
                var companyIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
                foreach (var company in companies)
                    companyIds.TryAdd(ImportValues.NameKey(company.Name), company.Id);

                // Each run of rows commits once. Every save committing on its own made a
                // thousand leads take twenty seconds on SQL Server, most of it waiting on
                // the log; a run is still short enough not to hold anyone else up.
                foreach (var run in plan.Chunk(RowsPerTransaction))
                {
                    var companiesBefore = new Dictionary<string, Guid>(companyIds, StringComparer.Ordinal);
                    try
                    {
                        await _imports.InTransactionAsync(async () =>
                        {
                            foreach (var row in run)
                            {
                                if (row.Action is ImportPlannedRow.Create or ImportPlannedRow.Update)
                                    await ApplyAsync(kind, row, batch, companyIds, userId, organizationId);
                                _imports.Forget();
                            }
                        }, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // The database refused the run as a whole (a deadlock, a lost
                        // connection), so none of it was saved. Say so for each row it held.
                        _logger.LogWarning(ex, "Import {BatchId}: rows {First} to {Last} were rolled back", batch.Id, run[0].Row, run[^1].Row);
                        _imports.Forget();
                        companyIds.Clear();
                        foreach (var (key, id) in companiesBefore) companyIds[key] = id;
                        foreach (var row in run.Where(r => r.Action is ImportPlannedRow.Create or ImportPlannedRow.Update))
                        {
                            if (row.Action == ImportPlannedRow.Create) row.RecordId = null;
                            row.Fail("Not saved: the database turned this part of the file away. Import these rows again");
                        }
                    }

                    // How far it has got, saved with the time: the list of imports shows the
                    // rows so far, and tells this import, still saving, from one the server
                    // stopped (see Running).
                    done += run.Length;
                    Count(batch, plan.Take(done));
                    batch.LastSavedAtUtc = DateTime.UtcNow;
                    try
                    {
                        await _imports.SaveBatchAsync(batch, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // Only the progress shown is lost, not the rows; the next run saves it again.
                        _logger.LogWarning(ex, "Import {BatchId}: progress after row {Row} could not be saved", batch.Id, run[^1].Row);
                        _imports.Forget();
                    }
                }
            }
            finally
            {
                Count(batch, plan.Take(done));
                batch.FinishedAtUtc = DateTime.UtcNow;
                await _imports.SaveBatchAsync(batch, CancellationToken.None);
            }
        }

        _logger.LogInformation("Import {BatchId}: {Created} created, {Updated} updated, {Skipped} skipped, {Failed} failed",
            batch.Id, batch.CreatedCount, batch.UpdatedCount, batch.SkippedCount, batch.FailedCount);
        return Outcome(batch.Id, false, kind, file, plan);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportBatchDto>> ListAsync(Guid userId, Guid? organizationId, string? kind, CancellationToken ct = default)
    {
        var filter = Kind(kind);
        var batches = await _imports.ListBatchesAsync(userId, organizationId, filter, ListedBatches, ct);
        var manager = await CanUndoAnyAsync(userId, organizationId, ct);
        return batches.Select(b => new ImportBatchDto(
            b.Batch.Id,
            b.Batch.EntityType,
            b.Batch.FileName,
            b.ImportedBy,
            b.Batch.StartedAtUtc,
            b.Batch.FinishedAtUtc,
            b.Batch.DuplicateMode,
            b.Batch.RanAutomations,
            b.Batch.TotalRows,
            b.Batch.CreatedCount,
            b.Batch.UpdatedCount,
            b.Batch.SkippedCount,
            b.Batch.FailedCount,
            b.Batch.UndoneAtUtc,
            b.Batch.RemovedCount,
            b.Remaining,
            Running(b.Batch),
            b.Remaining > 0 && !Running(b.Batch) && (manager || b.Batch.UserId == userId))).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<ImportUndoDto>> UndoAsync(Guid batchId, Guid userId, Guid? organizationId, bool dryRun, CancellationToken ct = default)
    {
        var batch = await _imports.FindBatchAsync(batchId, userId, organizationId, ct);
        if (batch == null) return DomainErrors.Import.NotFound;
        // Undoing removes records in bulk, so it is for whoever ran the import, or for the
        // people who could remove those records anyway.
        if (batch.UserId != userId && !await CanUndoAnyAsync(userId, organizationId, ct))
            return DomainErrors.Import.NotOwnerOrManager;
        // Undoing what is still arriving would remove some rows while later ones link to them.
        if (Running(batch)) return DomainErrors.Import.StillRunning;

        var plan = await _imports.PlanUndoAsync(batch, ct);
        if (!dryRun)
        {
            batch.UndoneAtUtc = DateTime.UtcNow;
            batch.UndoneByUserId = userId;
            batch.RemovedCount += plan.Count;
            // As with the import: once started it finishes, whoever closes the page.
            await _imports.RemoveAsync(batch, plan, CancellationToken.None);
            _logger.LogInformation("Import {BatchId} undone by {UserId}: {Leads} leads, {Contacts} contacts, {Companies} companies removed, {Kept} kept",
                batch.Id, userId, plan.Leads.Count, plan.Contacts.Count, plan.Companies.Count, plan.Kept.Count);
        }
        return new ImportUndoDto(batch.Id, dryRun, plan.Leads.Count, plan.Contacts.Count, plan.Companies.Count, plan.Kept);
    }

    private async Task ApplyAsync(
        string kind, ImportPlannedRow row, ImportBatch batch, Dictionary<string, Guid> companyIds, Guid userId, Guid? organizationId)
    {
        var ct = CancellationToken.None;
        try
        {
            Guid? companyId = null;
            if (row.CompanyName != null)
            {
                var company = await CompanyIdAsync(row.CompanyName, companyIds, userId, organizationId, ct);
                if (company.IsFailure)
                {
                    row.Fail(company.Error.Description);
                    return;
                }
                companyId = company.Value;
            }

            var request = Request(kind, row, companyId, batch.RanAutomations);
            var saved = Invalid(request) is { } invalid ? invalid : await SaveAsync(request, row.RecordId, userId, organizationId, ct);
            if (saved.IsFailure)
            {
                row.Fail(saved.Error.Description);
                return;
            }
            row.RecordId = saved.Value;

            if (row.Custom.Count > 0)
            {
                var values = await _customFieldValues.SaveValuesAsync(
                    userId, organizationId, new SaveCustomFieldValuesRequest(kind, saved.Value, row.Custom), ct);
                if (values.IsFailure) row.Notes.Add($"Its custom fields were not saved: {values.Error.Description}");
            }
        }
        catch (Exception ex)
        {
            // One row's failure is that row's. The next row starts clean (see Forget).
            _logger.LogWarning(ex, "Import {BatchId}: row {Row} could not be saved", batch.Id, row.Row);
            row.Fail("Something went wrong saving this row. Check whether it was added before importing it again");
        }
    }

    /// <summary>
    /// What a row sends to the service a form would use. Built the same way for the dry run,
    /// which checks it, and for the import, which sends it, so the two cannot disagree.
    /// </summary>
    /// <param name="companyId">The company to link; unknown, and so null, in a dry run.</param>
    private static object Request(string kind, ImportPlannedRow row, Guid? companyId, bool runAutomations)
    {
        var v = row.Values;
        var create = row.Action == ImportPlannedRow.Create;
        return kind switch
        {
            RecordTypes.Lead when create => new CreateLeadRequest
            {
                Name = v["name"], Email = v["email"], Phone = v.GetValueOrDefault("phone"), CompanyId = companyId,
                Source = v.GetValueOrDefault("source"), LeadSourceId = row.SourceId, Status = v["status"], LeadStatusId = row.StatusId,
                JobTitle = v.GetValueOrDefault("jobTitle"), Country = v.GetValueOrDefault("country"), Industry = v.GetValueOrDefault("industry"),
                CompanySize = v.GetValueOrDefault("companySize"), Description = v.GetValueOrDefault("description"),
                Origin = "import", RunAutomations = runAutomations,
            },
            RecordTypes.Lead => new UpdateLeadRequest
            {
                Name = v.GetValueOrDefault("name"), Email = v.GetValueOrDefault("email"), Phone = v.GetValueOrDefault("phone"), CompanyId = companyId,
                Source = v.GetValueOrDefault("source"), LeadSourceId = row.SourceId, Status = v.GetValueOrDefault("status"), LeadStatusId = row.StatusId,
                JobTitle = v.GetValueOrDefault("jobTitle"), Country = v.GetValueOrDefault("country"), Industry = v.GetValueOrDefault("industry"),
                CompanySize = v.GetValueOrDefault("companySize"), Description = v.GetValueOrDefault("description"),
                Origin = "import", RunAutomations = runAutomations,
            },
            RecordTypes.Contact when create => new CreateContactRequest
            {
                Name = v["name"], Email = v["email"], Phone = v.GetValueOrDefault("phone"), JobTitle = v.GetValueOrDefault("jobTitle"),
                CompanyId = companyId, Description = v.GetValueOrDefault("description"), DoNotContact = v.ContainsKey("doNotContact"),
            },
            RecordTypes.Contact => new UpdateContactRequest
            {
                Name = v.GetValueOrDefault("name"), Email = v.GetValueOrDefault("email"), Phone = v.GetValueOrDefault("phone"),
                JobTitle = v.GetValueOrDefault("jobTitle"), CompanyId = companyId, Description = v.GetValueOrDefault("description"),
                DoNotContact = v.ContainsKey("doNotContact") ? true : null,
            },
            _ when create => new CreateCompanyRequest
            {
                Name = v["name"], Domain = v.GetValueOrDefault("domain"), Website = v.GetValueOrDefault("website"), Industry = v.GetValueOrDefault("industry"),
                Size = v.GetValueOrDefault("size"), Location = v.GetValueOrDefault("location"), Description = v.GetValueOrDefault("description"),
            },
            _ => new UpdateCompanyRequest
            {
                Name = v.GetValueOrDefault("name"), Domain = v.GetValueOrDefault("domain"), Website = v.GetValueOrDefault("website"),
                Industry = v.GetValueOrDefault("industry"), Size = v.GetValueOrDefault("size"), Location = v.GetValueOrDefault("location"),
                Description = v.GetValueOrDefault("description"),
            },
        };
    }

    /// <summary>Sends a row's request to its service. The id is of the record created or updated.</summary>
    private async Task<Result<Guid>> SaveAsync(object request, Guid? recordId, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        static Result<Guid> Id<T>(Result<T> result, Func<T, Guid> id) => result.IsSuccess ? id(result.Value) : result.Error;
        return request switch
        {
            CreateLeadRequest r => Id(await _leads.CreateAsync(userId, organizationId, r, ct), d => d.Id),
            UpdateLeadRequest r => Id(await _leads.UpdateAsync(recordId!.Value, userId, organizationId, r, ct), d => d.Id),
            CreateContactRequest r => Id(await _contacts.CreateAsync(userId, organizationId, r, ct), d => d.Id),
            UpdateContactRequest r => Id(await _contacts.UpdateAsync(recordId!.Value, userId, organizationId, r, ct), d => d.Id),
            CreateCompanyRequest r => Id(await _companies.CreateAsync(userId, organizationId, r, ct), d => d.Id),
            UpdateCompanyRequest r => Id(await _companies.UpdateAsync(recordId!.Value, userId, organizationId, r, ct), d => d.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
    }

    /// <summary>The company of this name: one already here, one an earlier row added, or a new one.</summary>
    private async Task<Result<Guid>> CompanyIdAsync(string name, Dictionary<string, Guid> known, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var key = ImportValues.NameKey(name);
        if (known.TryGetValue(key, out var id)) return id;

        var created = await _companies.CreateAsync(userId, organizationId, new CreateCompanyRequest { Name = name }, ct);
        if (created.IsSuccess)
        {
            known[key] = created.Value.Id;
            return created.Value.Id;
        }
        // Someone added it while the file was being read.
        var added = (await _imports.CompaniesAsync(userId, organizationId, ct)).FirstOrDefault(c => ImportValues.NameKey(c.Name) == key);
        if (added == null) return created.Error;
        known[key] = added.Id;
        return added.Id;
    }

    /// <summary>
    /// The request checked as the API checks it on the way in. A form's request is checked
    /// before it reaches the service; an import's is made here, so it is checked here.
    /// </summary>
    private static Error? Invalid(object request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)) return null;
        return new Error("Import.InvalidRow", string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    private async Task<IReadOnlyList<ImportExisting>> ExistingAsync(
        string kind, Guid userId, Guid? organizationId, IReadOnlyList<Company> companies, bool withCustom, CancellationToken ct)
    {
        var custom = withCustom
            ? (await _imports.CustomValuesAsync(kind, userId, organizationId, ct)).ToDictionary(p => p.Key, p => ParseValues(p.Value))
            : new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>();
        IReadOnlyDictionary<string, JsonElement> CustomOf(Guid id) =>
            custom.TryGetValue(id, out var values) ? values : new Dictionary<string, JsonElement>();
        var companyNames = companies.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);
        string? CompanyOf(Guid? id) => id is Guid c && companyNames.TryGetValue(c, out var n) ? n : null;

        switch (kind)
        {
            case RecordTypes.Lead:
                return (await _imports.LeadsAsync(userId, organizationId, ct)).Select(l => new ImportExisting(l.Id, l.Name, new Dictionary<string, string?>
                {
                    ["name"] = l.Name, ["email"] = l.Email, ["phone"] = l.Phone, ["company"] = CompanyOf(l.CompanyId),
                    ["jobTitle"] = l.JobTitle, ["source"] = l.Source, ["status"] = l.Status, ["country"] = l.Country,
                    ["industry"] = l.Industry, ["companySize"] = l.CompanySize, ["description"] = l.Description,
                }, CustomOf(l.Id), l.IsConverted ? "was converted into a contact, so there is no lead left to update" : null)).ToList();
            case RecordTypes.Contact:
                return (await _imports.ContactsAsync(userId, organizationId, ct)).Select(c => new ImportExisting(c.Id, c.Name, new Dictionary<string, string?>
                {
                    ["name"] = c.Name, ["email"] = c.Email, ["phone"] = c.Phone, ["company"] = CompanyOf(c.CompanyId),
                    ["jobTitle"] = c.JobTitle, ["description"] = c.Description, ["doNotContact"] = c.DoNotContact ? "yes" : null,
                }, CustomOf(c.Id))).ToList();
            default:
                return companies.Select(c => new ImportExisting(c.Id, c.Name, new Dictionary<string, string?>
                {
                    ["name"] = c.Name, ["domain"] = c.Domain, ["website"] = c.Website, ["industry"] = c.Industry,
                    ["size"] = c.Size, ["location"] = c.Location, ["description"] = c.Description,
                }, CustomOf(c.Id))).ToList();
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseValues(string json)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in doc.RootElement.EnumerateObject())
                    values[property.Name] = property.Value.Clone();
        }
        catch (JsonException)
        {
            // Values that cannot be read compare as none, so the file's values are written.
        }
        return values;
    }

    /// <summary>Each mapped column with its field, after checking the choices fit the file and the kind.</summary>
    /// <param name="update">Rows update the records they match. A file of people can then leave the name out.</param>
    private static Result<IReadOnlyList<(int Column, ImportField Field)>> Columns(
        string kind, IReadOnlyList<string?>? choices, CsvFile file, IReadOnlyList<ImportField> fields, bool update)
    {
        if (choices == null || choices.Count != file.Headers.Count) return DomainErrors.Import.ColumnsMismatch;
        var byKey = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var mapped = new List<(int, ImportField)>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < choices.Count; i++)
        {
            var key = choices[i]?.Trim();
            if (string.IsNullOrEmpty(key)) continue;
            if (!byKey.TryGetValue(key, out var field)) return DomainErrors.Import.UnknownField(key);
            if (!used.Add(key)) return DomainErrors.Import.FieldMappedTwice(field.Label);
            mapped.Add((i, field));
        }

        if (kind == RecordTypes.Company)
        {
            if (!used.Contains("name")) return DomainErrors.Import.FieldRequired("the company's name");
        }
        else
        {
            if (!update && !used.Contains("name") && !used.Contains("firstName") && !used.Contains("lastName"))
                return DomainErrors.Import.FieldRequired("the name");
            if (!used.Contains("email")) return DomainErrors.Import.FieldRequired("the email address");
        }
        return mapped;
    }

    /// <summary>The batch's counts for the rows done so far.</summary>
    private static void Count(ImportBatch batch, IEnumerable<ImportPlannedRow> done)
    {
        var rows = done.ToList();
        batch.CreatedCount = rows.Count(r => r.Action == ImportPlannedRow.Create);
        batch.UpdatedCount = rows.Count(r => r.Action == ImportPlannedRow.Update);
        batch.SkippedCount = rows.Count(r => r.Action == ImportPlannedRow.Skip);
        batch.FailedCount = rows.Count(r => r.Action == ImportPlannedRow.Error);
    }

    private static ImportResultDto Outcome(Guid? batchId, bool dryRun, string kind, CsvFile file, IReadOnlyList<ImportPlannedRow> plan) => new(
        batchId,
        dryRun,
        kind,
        file.Rows.Count,
        plan.Count(r => r.Action == ImportPlannedRow.Create),
        plan.Count(r => r.Action == ImportPlannedRow.Update),
        plan.Count(r => r.Action == ImportPlannedRow.Skip),
        plan.Count(r => r.Action == ImportPlannedRow.Error),
        plan.Select(r => r.ToDto()).ToList());

    private static Result<CsvFile> ReadFile(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return DomainErrors.Import.Empty;
        // Base64 is four characters for every three bytes; refuse a big one before decoding it.
        if (content.Length > (MaxFileMegabytes * 1024 * 1024 + 2) / 3 * 4 + 8) return DomainErrors.Import.TooLarge(MaxFileMegabytes);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(content.Trim());
        }
        catch (FormatException)
        {
            return DomainErrors.Import.Unreadable;
        }
        if (bytes.Length > MaxFileMegabytes * 1024 * 1024) return DomainErrors.Import.TooLarge(MaxFileMegabytes);
        return CsvFile.Read(bytes, MaxRows);
    }

    private static string? Kind(string? kind)
    {
        var k = kind?.Trim().ToLowerInvariant();
        return ImportFields.IsKind(k) ? k : null;
    }

    private static string FileName(string? name)
    {
        var clean = Path.GetFileName((name ?? "").Trim());
        if (clean.Length == 0) return "import.csv";
        return clean.Length <= 260 ? clean : clean[..260];
    }

    /// <summary>
    /// Whether this person may undo anyone's import here: whoever manages the workspace's
    /// settings (the owner and managers by default), or anyone in their own personal workspace.
    /// </summary>
    private async Task<bool> CanUndoAnyAsync(Guid userId, Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is not Guid orgId) return true;
        return await _organizations.HasPermissionAsync(userId, orgId, Domain.Common.MemberPermissions.ManageSettings, ct);
    }
}
