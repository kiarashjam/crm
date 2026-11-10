using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

/// <summary>
/// A member of an organization: their coarse role, the preset their permissions start
/// from, what they may do, and whether the owner changed that from the preset's.
/// </summary>
public record OrgMemberDto(
    Guid UserId,
    string Name,
    string Email,
    OrgMemberRole Role,
    string Preset = "",
    IReadOnlyList<string>? Permissions = null,
    bool Customised = false);

/// <summary>A permission, as the Team page lists it.</summary>
public record PermissionDto(string Key, string Label, string Description);

/// <summary>A preset, with what it allows. The owner's is shown but given to nobody.</summary>
public record PermissionPresetDto(string Key, string Label, string Description, IReadOnlyList<string> Permissions, bool Assignable);

/// <summary>Every permission and every preset, for the Team page.</summary>
public record PermissionCatalogueDto(IReadOnlyList<PermissionDto> Permissions, IReadOnlyList<PermissionPresetDto> Presets);

/// <summary>
/// What a member may do: a preset, and, from the owner only, their own list of
/// permissions instead of the preset's. Leave <see cref="Permissions"/> out to follow the preset.
/// </summary>
public record SetMemberPermissionsRequest(string Preset, IReadOnlyList<string>? Permissions = null);

/// <summary>
/// A member was removed, and their open work handed to <see cref="HandedOverToUserId"/>:
/// how many open leads, deals and tasks moved.
/// </summary>
public record MemberRemovedDto(Guid HandedOverToUserId, string HandedOverToName, int Leads, int Deals, int Tasks);
