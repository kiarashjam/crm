using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserSettingsDto> SaveAsync(Guid userId, UserSettingsDto dto, CancellationToken ct = default);
}
