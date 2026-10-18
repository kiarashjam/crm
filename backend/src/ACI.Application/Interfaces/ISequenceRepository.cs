using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// The facts the sequence runner needs about whoever it is about to contact, read
/// fresh at each step. Raw facts only: whether they mean "stop" is the service's
/// decision, where it can be tested.
/// </summary>
/// <param name="Exists">False when the lead or contact is gone (deleted, or not in this workspace).</param>
/// <param name="Email">Their address as it is NOW, not as it was when they were enrolled.</param>
public sealed record SequenceTargetState(
    bool Exists,
    string? Name,
    string? Email,
    string? CompanyName,
    bool IsConverted = false,
    string? Status = null,
    bool DoNotContact = false,
    bool IsArchived = false)
{
    public static readonly SequenceTargetState Missing = new(false, null, null, null);
}

public interface ISequenceRepository
{
    Task<IReadOnlyList<OutreachSequence>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<OutreachSequence?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task SaveAsync(OutreachSequence sequence, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OutreachEnrollment>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default);

    /// <summary>One enrollment, only if it belongs to this organisation.</summary>
    Task<OutreachEnrollment?> GetEnrollmentAsync(Guid id, Guid? organizationId, CancellationToken ct = default);

    /// <summary>An active or paused enrollment of this target in this sequence, if there is one.</summary>
    Task<OutreachEnrollment?> FindOpenEnrollmentAsync(Guid sequenceId, Guid targetId, CancellationToken ct = default);

    Task SaveEnrollmentAsync(OutreachEnrollment enrollment, CancellationToken ct = default);

    /// <summary>Delete an enrollment, only if it belongs to this organisation.</summary>
    Task<bool> DeleteEnrollmentAsync(Guid id, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Stop every open enrollment in a sequence that is being deleted.</summary>
    Task StopEnrollmentsAsync(Guid sequenceId, CancellationToken ct = default);

    Task<IReadOnlyList<OutreachEnrollment>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default);

    /// <summary>
    /// Atomically take the right to run this enrollment's current step. True for
    /// exactly one caller: the row moves only if it is still active, still on
    /// <paramref name="step"/> and still due at <paramref name="dueAtUtc"/>.
    /// </summary>
    Task<bool> TryClaimAsync(Guid enrollmentId, int step, DateTime dueAtUtc, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>The target's current state, read within the enrollment's organisation.</summary>
    Task<SequenceTargetState> TargetStateAsync(string targetType, Guid targetId, Guid? organizationId, CancellationToken ct = default);
}
