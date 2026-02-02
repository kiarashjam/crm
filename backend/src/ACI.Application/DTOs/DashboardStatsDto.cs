namespace ACI.Application.DTOs;

public record DashboardStatsDto(
    int ActiveLeadsCount,
    int ActiveDealsCount,
    decimal PipelineValue,
    int DealsWonCount,
    int DealsLostCount
);
