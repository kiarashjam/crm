using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserSettingsDto> SaveAsync(Guid userId, UpdateUserSettingsRequest request, CancellationToken ct = default);
    Task<UserSettingsDto> ResetToDefaultsAsync(Guid userId, CancellationToken ct = default);
}
