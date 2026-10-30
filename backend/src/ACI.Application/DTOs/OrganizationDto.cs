namespace ACI.Application.DTOs;

/// <summary>
/// A workspace as one of its members sees it, with the currency, locale and time zone
/// the app formats its amounts and dates in.
/// </summary>
public record OrganizationDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    bool IsOwner,
    int Role,
    string Currency = "CHF",
    string Locale = "de-CH",
    string TimeZone = "Europe/Zurich");

/// <summary>
/// What a workspace works in. Each field left out keeps its current value.
/// </summary>
public record UpdateOrganizationSettingsRequest(string? Currency, string? Locale, string? TimeZone);
