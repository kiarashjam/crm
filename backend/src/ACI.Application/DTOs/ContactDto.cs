namespace ACI.Application.DTOs;

public record ContactDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? JobTitle,
    Guid? CompanyId,
    DateTime? LastActivityAtUtc,
    Guid? ConvertedFromLeadId = null,
    DateTime? ConvertedAtUtc = null,
    bool IsArchived = false,
    bool DoNotContact = false,
    string? PreferredContactMethod = null,
    string? CompanyName = null,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null,
    string? Description = null);
