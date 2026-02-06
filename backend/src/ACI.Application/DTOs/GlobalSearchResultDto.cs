namespace ACI.Application.DTOs;

public record GlobalSearchResultDto(
    IReadOnlyList<LeadDto> Leads,
    IReadOnlyList<ContactDto> Contacts,
    IReadOnlyList<CompanyDto> Companies,
    IReadOnlyList<DealDto> Deals);
