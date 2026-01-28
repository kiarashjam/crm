using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class ConnectionService : IConnectionService
{
    private readonly IUserRepository _userRepository;

    public ConnectionService(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<ConnectionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var conn = await _userRepository.GetConnectionAsync(userId, ct);
        return conn == null
            ? new ConnectionStatusDto(false, null)
            : new ConnectionStatusDto(conn.Connected, conn.AccountEmail);
    }

    public async Task SetStatusAsync(Guid userId, ConnectionStatusDto dto, CancellationToken ct = default)
    {
        var conn = await _userRepository.GetConnectionAsync(userId, ct) ?? new CrmConnection { UserId = userId };
        conn.Connected = dto.Connected;
        conn.AccountEmail = dto.AccountEmail;
        conn.UpdatedAtUtc = DateTime.UtcNow;
        if (dto.Connected)
            conn.ConnectedAtUtc = DateTime.UtcNow;
        await _userRepository.UpsertConnectionAsync(conn, ct);
    }
}
