namespace ACI.Application.DTOs;

public record OrganizationDto(Guid Id, string Name, Guid OwnerUserId, bool IsOwner);
