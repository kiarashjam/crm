using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICopyHistoryService
{
    Task<IReadOnlyList<CopyHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<CopyHistoryStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default);
    Task<CopyHistoryItemDto> AddAsync(Guid userId, string type, string copy, string recipientName, string recipientType, string recipientId, CancellationToken ct = default);
}
