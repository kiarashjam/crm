using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICopyHistoryService
{
    Task<IReadOnlyList<CopyHistoryItemDto>> GetHistoryAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<CopyHistoryStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<CopyHistoryItemDto> AddAsync(Guid userId, Guid? organizationId, string type, string copy, string recipientName, string recipientType, string recipientId, CancellationToken ct = default);
}
