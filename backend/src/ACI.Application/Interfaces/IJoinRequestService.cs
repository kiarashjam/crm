using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IJoinRequestService
{
    Task<Result<JoinRequestDto>> CreateJoinRequestAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<JoinRequestDto>>> ListPendingJoinRequestsAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<JoinRequestDto>> AcceptJoinRequestAsync(Guid joinRequestId, Guid userId, CancellationToken ct = default);
    Task<Result<JoinRequestDto>> RejectJoinRequestAsync(Guid joinRequestId, Guid userId, CancellationToken ct = default);
}
