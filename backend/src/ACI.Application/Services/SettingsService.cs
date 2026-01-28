using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IUserRepository _userRepository;

    public SettingsService(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _userRepository.GetSettingsAsync(userId, ct);
        if (settings != null)
            return new UserSettingsDto(settings.CompanyName, settings.BrandTone.ToString().ToLowerInvariant());

        return new UserSettingsDto("Acme Corporation", "professional");
    }

    public async Task<UserSettingsDto> SaveAsync(Guid userId, UserSettingsDto dto, CancellationToken ct = default)
    {
        var brandTone = Enum.TryParse<BrandTone>(dto.BrandTone, true, out var bt) ? bt : BrandTone.Professional;
        var settings = new UserSettings
        {
            UserId = userId,
            CompanyName = dto.CompanyName,
            BrandTone = brandTone,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        await _userRepository.UpsertSettingsAsync(settings, ct);
        return new UserSettingsDto(settings.CompanyName, settings.BrandTone.ToString().ToLowerInvariant());
    }
}
