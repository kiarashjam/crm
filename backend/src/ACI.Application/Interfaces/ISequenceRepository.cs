using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ISequenceRepository
{
    Task<IReadOnlyList<OutreachSequence>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<OutreachSequence?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task SaveAsync(OutreachSequence sequence, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OutreachEnrollment>> EnrollmentsAsync(Guid? organizationId, Guid? sequenceId, CancellationToken ct = default);
    Task<OutreachEnrollment?> GetEnrollmentAsync(Guid id, CancellationToken ct = default);
    Task SaveEnrollmentAsync(OutreachEnrollment enrollment, CancellationToken ct = default);
    Task DeleteEnrollmentAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OutreachEnrollment>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default);
}
