using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IConnectionService
{
    Task<ConnectionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task SetStatusAsync(Guid userId, ConnectionStatusDto dto, CancellationToken ct = default);
}
