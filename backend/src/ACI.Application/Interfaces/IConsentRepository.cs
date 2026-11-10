using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>Consent answers, as recorded (see <see cref="ConsentRecord"/>). Rows are only ever added.</summary>
public interface IConsentRepository
{
    /// <summary>
    /// The answers that stand for these subjects, optionally for one channel and purpose.
    /// </summary>
    Task<IReadOnlyList<ConsentRecord>> CurrentAsync(
        Guid organizationId, string subjectType, IReadOnlyCollection<Guid> subjectIds,
        string? channel = null, string? purpose = null, CancellationToken ct = default);

    /// <summary>Everything recorded for one subject, newest first.</summary>
    Task<IReadOnlyList<ConsentRecord>> HistoryAsync(Guid organizationId, string subjectType, Guid subjectId, int take, CancellationToken ct = default);

    /// <summary>
    /// Adds <paramref name="record"/> as the answer that stands for its subject, channel and
    /// purpose, and marks the one before it superseded, in one save. False when another
    /// answer for the same was recorded at the same moment and this one was not saved.
    /// </summary>
    Task<bool> AddCurrentAsync(ConsentRecord record, CancellationToken ct = default);
}
