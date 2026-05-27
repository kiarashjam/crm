namespace ACI.Application.DTOs;

public record LeadStatsDto(
    int Total,
    int Converted,
    int Active,
    int NewLeads,
    int Contacted,
    int Qualified,
    int ConversionRate,
    int ThisWeek,
    int HotLeads);
