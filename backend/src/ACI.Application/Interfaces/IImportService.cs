using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Imports a spreadsheet of leads, contacts or companies into the caller's workspace,
/// shows beforehand what it would do, and undoes it afterwards.
/// </summary>
public interface IImportService
{
    /// <summary>Reads the file: its separator, columns and a few values from each, and the fields they can go into.</summary>
    Task<Result<ImportAnalysisDto>> AnalyzeAsync(Guid userId, Guid? organizationId, ImportFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Works out what each row would do and, unless <paramref name="dryRun"/>, does it. A dry
    /// run writes nothing; the import itself runs the same plan.
    /// </summary>
    Task<Result<ImportResultDto>> RunAsync(Guid userId, Guid? organizationId, ImportRequest request, bool dryRun, CancellationToken ct = default);

    /// <summary>The workspace's latest imports, newest first, optionally of one kind.</summary>
    Task<IReadOnlyList<ImportBatchDto>> ListAsync(Guid userId, Guid? organizationId, string? kind, CancellationToken ct = default);

    /// <summary>
    /// Removes the records the import created that nobody has changed or worked on since,
    /// or for a dry run says which those are. Only whoever ran it, or the workspace's owner
    /// or a manager, may.
    /// </summary>
    Task<Result<ImportUndoDto>> UndoAsync(Guid batchId, Guid userId, Guid? organizationId, bool dryRun, CancellationToken ct = default);
}
