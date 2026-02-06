using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IInviteService
{
    Task<Result<InviteDto>> CreateInviteAsync(Guid organizationId, Guid userId, CreateInviteRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InviteDto>>> ListPendingInvitesAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<InviteDto>> AcceptInviteAsync(string token, Guid userId, CancellationToken ct = default);
    Task<Result<InviteDto>> AcceptInviteByIdAsync(Guid inviteId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<InviteDto>>> ListMyPendingInvitesAsync(Guid userId, CancellationToken ct = default);
}
