using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

public record OrgMemberDto(Guid UserId, string Name, string Email, OrgMemberRole Role);
