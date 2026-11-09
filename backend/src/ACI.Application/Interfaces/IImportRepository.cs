using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>The database side of importing: past runs, what is already here, and undoing a run.</summary>
/// <remarks>Every method reads and writes the caller's workspace only: an organization's records, or with none the user's own.</remarks>
public interface IImportRepository
{
    Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default);

    /// <summary>Saves changes to a batch added or found here.</summary>
    Task SaveBatchAsync(ImportBatch batch, CancellationToken ct = default);

    Task<ImportBatch?> FindBatchAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The latest runs, newest first, with who ran each and how many of its records are still here.</summary>
    Task<IReadOnlyList<(ImportBatch Batch, string ImportedBy, int Remaining)>> ListBatchesAsync(
        Guid userId, Guid? organizationId, string? entityType, int take, CancellationToken ct = default);

    /// <summary>The workspace's leads, with the fields an import reads and writes.</summary>
    Task<IReadOnlyList<Lead>> LeadsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The workspace's active contacts, with the fields an import reads and writes.</summary>
    Task<IReadOnlyList<Contact>> ContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The workspace's companies, with the fields an import reads and writes.</summary>
    Task<IReadOnlyList<Company>> CompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>The custom field values of every record of one kind, as stored JSON by record id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> CustomValuesAsync(string entityType, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Until the returned scope is disposed, every lead, contact and company this request
    /// adds carries <paramref name="batchId"/>, whichever code adds it.
    /// </summary>
    IDisposable StampNewRecords(Guid batchId);

    /// <summary>
    /// Lets go of every record read or written so far in this request. An import does this
    /// between rows, so a row that failed half way cannot fail every row after it, and the
    /// thousandth row is not saved alongside a check of the 999 before it.
    /// </summary>
    void Forget();

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction, so a run of rows commits once instead
    /// of at every save each row makes. Rolled back when it throws, or the commit fails.
    /// </summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>
    /// Which of the records <paramref name="batch"/> created an undo would remove, and which
    /// it keeps and why. A record is kept when anyone has changed it or worked on it since,
    /// or when something that stays still needs it.
    /// </summary>
    Task<ImportUndoPlan> PlanUndoAsync(ImportBatch batch, CancellationToken ct = default);

    /// <summary>Removes the records in <paramref name="plan"/>, in one transaction, and saves the batch with it.</summary>
    Task RemoveAsync(ImportBatch batch, ImportUndoPlan plan, CancellationToken ct = default);
}

/// <summary>What undoing an import would remove, and what it keeps.</summary>
public sealed record ImportUndoPlan(
    IReadOnlyList<Guid> Leads,
    IReadOnlyList<Guid> Contacts,
    IReadOnlyList<Guid> Companies,
    IReadOnlyList<ImportKeptDto> Kept)
{
    public int Count => Leads.Count + Contacts.Count + Companies.Count;
}
