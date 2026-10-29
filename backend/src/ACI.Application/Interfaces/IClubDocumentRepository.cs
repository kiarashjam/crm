using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IClubDocumentRepository
{
    Task<IReadOnlyList<ClubDocument>> ListAsync(Guid? organizationId, string kind, CancellationToken ct = default);
    Task<ClubDocument?> GetAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default);

    /// <summary>
    /// Saves the document. Throws <see cref="ConcurrencyConflictException"/> when the row
    /// changed after this request read it, instead of writing over that change.
    /// </summary>
    Task SaveAsync(ClubDocument document, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, Guid? organizationId, string kind, CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction, committed only when it
    /// succeeds, so a tab close or a redeem cannot stop halfway: the charge posted and the
    /// points missing, or the card spent with no redemption on record.
    /// </summary>
    Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct = default);
}
