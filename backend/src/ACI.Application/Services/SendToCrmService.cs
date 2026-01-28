using ACI.Application.DTOs;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

public class SendToCrmService : ISendToCrmService
{
    private readonly ICopyHistoryService _copyHistoryService;

    public SendToCrmService(ICopyHistoryService copyHistoryService) => _copyHistoryService = copyHistoryService;

    public async Task<SendToCrmResult> SendAsync(Guid userId, SendToCrmRequest request, CancellationToken ct = default)
    {
        await _copyHistoryService.AddAsync(
            userId,
            request.CopyTypeLabel,
            request.Copy,
            request.RecordName,
            request.ObjectType,
            request.RecordId,
            ct);
        return new SendToCrmResult(true, $"Copy sent to {request.RecordName} ({request.ObjectType}).");
    }
}
