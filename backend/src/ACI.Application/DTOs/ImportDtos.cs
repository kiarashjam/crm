using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>A spreadsheet to import, sent as its bytes so the server can tell how they are encoded.</summary>
public record ImportFileRequest
{
    /// <summary>What the file holds: "lead", "contact" or "company".</summary>
    [Required]
    [StringLength(16)]
    public string Kind { get; init; } = string.Empty;

    /// <summary>The file's name, shown in the list of past imports.</summary>
    [StringLength(260)]
    public string? FileName { get; init; }

    /// <summary>The file's bytes, base64-encoded.</summary>
    [Required]
    public string Content { get; init; } = string.Empty;
}

/// <summary>A file to import, with what to do with each of its columns.</summary>
public record ImportRequest : ImportFileRequest
{
    /// <summary>
    /// One entry per column of the file, in the file's order: the key of the field the
    /// column goes into (see <see cref="ImportFieldDto.Key"/>), or null to leave it out.
    /// </summary>
    public IReadOnlyList<string?>? Columns { get; init; }

    /// <summary>
    /// What to do with a row that matches a record already here: "skip" it (the default),
    /// or "update" that record with the row's values. An empty cell never clears anything.
    /// </summary>
    [StringLength(16)]
    public string? Duplicates { get; init; }

    /// <summary>
    /// Leads only: run the workspace's automations for them as if each were a new lead.
    /// Off unless asked: an imported list is people the workspace already knows.
    /// </summary>
    public bool RunAutomations { get; init; }

    /// <summary>Leads only: the status of a row whose status cell is empty, or holds one the workspace does not have.</summary>
    [StringLength(50)]
    public string? DefaultStatus { get; init; }

    /// <summary>Leads only: the source of a row whose source cell is empty.</summary>
    [StringLength(64)]
    public string? DefaultSource { get; init; }
}

/// <summary>A field a column can go into.</summary>
/// <param name="Key">What the request names it by: "email", or "custom:tier" for a custom field.</param>
/// <param name="Required">The import needs a column for it. For a person's name, "name" or its parts.</param>
public sealed record ImportFieldDto(string Key, string Label, bool Required, bool Custom, string? Hint);

/// <summary>One column of the file.</summary>
/// <param name="Field">The field its header suggests, or null.</param>
public sealed record ImportColumnDto(int Index, string Header, IReadOnlyList<string> Samples, string? Field);

/// <summary>What the server read from a file, before anything is imported.</summary>
public sealed record ImportAnalysisDto(
    string Kind,
    string Separator,
    string Encoding,
    int RowCount,
    IReadOnlyList<ImportColumnDto> Columns,
    IReadOnlyList<ImportFieldDto> Fields);

/// <summary>What happened, or would happen, to one row.</summary>
/// <param name="Row">The row's number in the spreadsheet, counting the header as row 1.</param>
/// <param name="Action">"create", "update", "skip" or "error".</param>
/// <param name="Label">Who or what the row is, as a person would name it.</param>
/// <param name="Message">Why it is skipped or refused, or which record it updates.</param>
/// <param name="RecordId">The record it created or updated, or the one it matched.</param>
/// <param name="Changes">For an update: each field that changes, from what to what.</param>
/// <param name="Notes">Things worth knowing that do not stop the row: a company it adds, a status it could not use.</param>
public sealed record ImportRowDto(
    int Row,
    string Action,
    string Label,
    string? Message,
    Guid? RecordId,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Notes);

/// <summary>An import's outcome, or for a dry run what it would be.</summary>
/// <param name="BatchId">The import, for undoing it. Null for a dry run.</param>
public sealed record ImportResultDto(
    Guid? BatchId,
    bool DryRun,
    string Kind,
    int TotalRows,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportRowDto> Rows);

/// <summary>A past import.</summary>
/// <param name="Remaining">How many records it created are still here.</param>
/// <param name="CanUndo">Whether the caller may undo it, and it has records left to undo.</param>
public sealed record ImportBatchDto(
    Guid Id,
    string Kind,
    string FileName,
    string ImportedBy,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string Duplicates,
    bool RanAutomations,
    int TotalRows,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    DateTime? UndoneAtUtc,
    int Removed,
    int Remaining,
    bool CanUndo);

/// <summary>What undoing an import removed, or for a dry run would remove, and what it kept.</summary>
/// <param name="Leads">Leads removed (or to remove).</param>
/// <param name="Contacts">Contacts removed, including the ones made for removed leads.</param>
/// <param name="Companies">Companies removed.</param>
/// <param name="Kept">Records the import created that stay, each with why.</param>
public sealed record ImportUndoDto(
    Guid BatchId,
    bool DryRun,
    int Leads,
    int Contacts,
    int Companies,
    IReadOnlyList<ImportKeptDto> Kept);

/// <summary>A record an undo leaves where it is.</summary>
public sealed record ImportKeptDto(string Kind, Guid Id, string Label, string Reason);
