namespace ACI.Application.DTOs;

/// <param name="Preset">The preset the person joins with; null when they join as a Member.</param>
public record InviteDto(Guid Id, Guid OrganizationId, string OrganizationName, string Email, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, string? Preset = null);
