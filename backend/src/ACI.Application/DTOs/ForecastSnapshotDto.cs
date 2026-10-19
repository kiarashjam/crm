namespace ACI.Application.DTOs;

public record ForecastSnapshotDto(
    Guid Id,
    string Month,
    string Currency,
    string AssigneeName,
    decimal WeightedValue,
    decimal OpenValue,
    int Covered,
    int Missing,
    DateTime SavedAtUtc);
