namespace ACI.Application.DTOs;

/// <summary>
/// A workspace as one of its members sees it, with the currency new deals are priced in,
/// the locale its amounts are written in, and the time zone its days and months begin in.
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
