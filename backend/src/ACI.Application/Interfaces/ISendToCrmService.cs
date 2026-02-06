using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ISendToCrmService
{
    Task<SendToCrmResult> SendAsync(Guid userId, Guid? organizationId, SendToCrmRequest request, CancellationToken ct = default);
}
