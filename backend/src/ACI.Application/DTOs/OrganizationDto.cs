using ACI.Application.Common;

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
    string TimeZone = "Europe/Zurich",
    int StaleDealDays = StaleDeals.DefaultDays,
    string PermissionPreset = "",
    IReadOnlyList<string>? Permissions = null,
    /// <summary>What the workspace's leads are: "person" (people only), "establishment" or "both".</summary>
    string LeadKinds = "person");

/// <summary>
/// What a workspace works in. Each field left out keeps its current value.
/// </summary>
/// <param name="StaleDealDays">Days without activity after which an open deal is stale, 1–365; 0 never flags one.</param>
public record UpdateOrganizationSettingsRequest(string? Currency, string? Locale, string? TimeZone, int? StaleDealDays = null, string? LeadKinds = null);
