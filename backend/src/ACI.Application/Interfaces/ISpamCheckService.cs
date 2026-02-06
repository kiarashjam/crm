using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ISpamCheckService
{
    Task<SpamCheckResponse> CheckAsync(SpamCheckRequest request, CancellationToken ct = default);
}
