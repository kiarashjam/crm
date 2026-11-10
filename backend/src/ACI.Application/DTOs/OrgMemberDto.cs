using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

public record OrgMemberDto(Guid UserId, string Name, string Email, OrgMemberRole Role);

/// <summary>
/// A member was removed, and their open work handed to <see cref="HandedOverToUserId"/>:
/// how many open leads, deals and tasks moved.
/// </summary>
public record MemberRemovedDto(Guid HandedOverToUserId, string HandedOverToName, int Leads, int Deals, int Tasks);
