namespace ACI.Domain.Entities;

/// <summary>
/// One run of the importer: a file of leads, contacts or companies brought in at once.
/// </summary>
/// <remarks>
/// The leads, contacts and companies a run creates carry its id (ImportBatchId), so the
/// run can be undone: undoing removes the records it created that nobody has changed
/// or worked on since. Changes it made to records that were already there stay.
/// </remarks>
public class ImportBatch : Common.BaseEntity
{
    /// <summary>The workspace it imported into; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Who ran it.</summary>
    public Guid UserId { get; set; }

    /// <summary>What the file held: <see cref="RecordTypes.Lead"/>, <see cref="RecordTypes.Contact"/> or <see cref="RecordTypes.Company"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>What was done with a row matching a record already there: "skip" or "update".</summary>
    public string DuplicateMode { get; set; } = "skip";

    /// <summary>Whether the person importing asked for the workspace's automations to run for these records.</summary>
    public bool RanAutomations { get; set; }

    /// <summary>The file's data rows.</summary>
    public int TotalRows { get; set; }

    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// When it last saved a run of rows, with the counts so far. It moves every few seconds
    /// while the import runs, so a long import is told apart from one the server stopped.
    /// </summary>
    public DateTime? LastSavedAtUtc { get; set; }

    /// <summary>When the last row was done. Null while it runs, or if the server stopped part way.</summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>When it was last undone. An undo that had to keep records can be run again later.</summary>
    public DateTime? UndoneAtUtc { get; set; }

    public Guid? UndoneByUserId { get; set; }

    /// <summary>How many of its records undoing has removed, over every undo.</summary>
    public int RemovedCount { get; set; }
}
