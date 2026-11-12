using ACI.Application.Common;

namespace ACI.Application.DTOs;

/// <summary>
/// A workspace as one of its members sees it, with the currency new deals are priced in,
/// the locale its amounts are written in, the time zone its days and months begin in, and
/// which of the Sales and Club modules it has on.
/// </summary>
public record OrganizationDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    bool IsOwner,
    int Role,
    string Currency = "CHF",
    string Locale = "de-CH",
    string TimeZone = "Europe/Zurich",
    int StaleDealDays = StaleDeals.DefaultDays,
    string PermissionPreset = "",
    IReadOnlyList<string>? Permissions = null,
    bool SalesEnabled = true,
    bool ClubEnabled = true);

/// <summary>
/// What a workspace works in. Each field left out keeps its current value.
/// </summary>
/// <param name="StaleDealDays">Days without activity after which an open deal is stale, 1–365; 0 never flags one.</param>
public record UpdateOrganizationSettingsRequest(string? Currency, string? Locale, string? TimeZone, int? StaleDealDays = null);

/// <summary>Which of the Sales and Club modules a workspace has on.</summary>
public record WorkspaceModulesDto(bool Sales, bool Club);

/// <summary>Turns the Sales or Club module on (true) or off (false); a field left out keeps its value.</summary>
public record SetWorkspaceModulesRequest(bool? Sales, bool? Club);
