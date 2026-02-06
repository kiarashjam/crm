using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IInviteRepository
{
    Task<Invite?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Invite>> ListPendingByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Invite>> ListPendingByEmailAsync(string email, CancellationToken ct = default);
    Task<Invite> AddAsync(Invite invite, CancellationToken ct = default);
    Task UpdateAsync(Invite invite, CancellationToken ct = default);
}
