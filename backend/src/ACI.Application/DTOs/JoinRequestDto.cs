namespace ACI.Application.DTOs;

public record JoinRequestDto(Guid Id, Guid OrganizationId, string OrganizationName, Guid UserId, string UserName, string UserEmail, string Status, DateTime CreatedAtUtc);
