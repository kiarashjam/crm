namespace ACI.Application.DTOs;

/// <summary>One search row for a task or an activity. The title is what matched.</summary>
public record SearchHitDto(Guid Id, string Title, string? Detail);

public record GlobalSearchResultDto(
    IReadOnlyList<LeadDto> Leads,
    IReadOnlyList<ContactDto> Contacts,
    IReadOnlyList<CompanyDto> Companies,
    IReadOnlyList<DealDto> Deals,
    IReadOnlyList<SearchHitDto> Activities,
    IReadOnlyList<SearchHitDto> Tasks);
