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
    /// <summary>
    /// Atomically claims a pending invite for the given user. Returns true if the row was claimed,
    /// false if the invite was already accepted (loser of the race) or no longer exists.
    /// </summary>
    Task<bool> TryClaimAsync(Guid inviteId, Guid acceptingUserId, CancellationToken ct = default);
}
