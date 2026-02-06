namespace ACI.Application.DTOs;

public record InviteDto(Guid Id, Guid OrganizationId, string OrganizationName, string Email, DateTime ExpiresAtUtc, DateTime CreatedAtUtc);
