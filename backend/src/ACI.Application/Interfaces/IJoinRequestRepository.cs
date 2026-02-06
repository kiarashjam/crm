using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IJoinRequestRepository
{
    Task<JoinRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JoinRequest>> ListPendingByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<JoinRequest> AddAsync(JoinRequest joinRequest, CancellationToken ct = default);
    Task UpdateAsync(JoinRequest joinRequest, CancellationToken ct = default);
}
