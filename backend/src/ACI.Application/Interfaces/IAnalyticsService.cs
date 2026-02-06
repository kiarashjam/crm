using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IAnalyticsService
{
    Task<CopyAnalyticsSummaryDto> GetSummaryAsync(Guid userId, Guid? organizationId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task TrackEventAsync(Guid userId, Guid? organizationId, TrackCopyEventRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CopyConversionDto>> GetConversionsAsync(Guid userId, Guid? organizationId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<CopyConversionDto> CreateConversionAsync(Guid userId, Guid? organizationId, CreateConversionRequest request, CancellationToken ct = default);
}
