using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserSettingsDto> SaveAsync(Guid userId, UpdateUserSettingsRequest request, CancellationToken ct = default);
    Task<UserSettingsDto> ResetToDefaultsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The language the person chose for the app, or null when they have not chosen.</summary>
    Task<Result<LanguageChoiceDto>> LanguageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Keeps the person's language on their account: "en", "de" or "fr", or null (or empty)
    /// to follow their workspace and browser again. Anything else is refused.
    /// </summary>
    Task<Result<LanguageChoiceDto>> SetLanguageAsync(Guid userId, string? language, CancellationToken ct = default);
}
