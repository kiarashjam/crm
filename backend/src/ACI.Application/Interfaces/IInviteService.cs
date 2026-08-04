using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IInviteService
{
    Task<Result<InviteDto>> CreateInviteAsync(Guid organizationId, Guid userId, CreateInviteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Re-sends the invitation email for an invite that is still pending, and refreshes its
    /// expiry. Unlike creating an invite — where a mail problem is tolerated because the
    /// invitation itself is what matters — this fails when the email cannot be sent, since
    /// sending is the entire point of the request.
    /// </summary>
    Task<Result<InviteDto>> ResendInviteAsync(Guid inviteId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InviteDto>>> ListPendingInvitesAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<InviteDto>> AcceptInviteAsync(string token, Guid userId, CancellationToken ct = default);
    Task<Result<InviteDto>> AcceptInviteByIdAsync(Guid inviteId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InviteDto>>> ListMyPendingInvitesAsync(Guid userId, CancellationToken ct = default);
}
