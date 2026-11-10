namespace ACI.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    string? Domain,
    string? Industry,
    string? Size,
    string? Description,
    string? Website,
    string? Location,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    // The company as of this read. An edit that sends it back is refused once anyone has
    // saved the company since (see UpdateCompanyRequest.Version).
    Guid? Version = null);

public record MergeCompaniesRequest(Guid OtherCompanyId);
