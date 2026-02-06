using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for sending generated copy to CRM records.
/// </summary>
public class SendToCrmService : ISendToCrmService
{
    private readonly ICopyHistoryService _copyHistoryService;
    private readonly ILogger<SendToCrmService> _logger;

    public SendToCrmService(
        ICopyHistoryService copyHistoryService, 
        ILogger<SendToCrmService> logger)
    {
        _copyHistoryService = copyHistoryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SendToCrmResult> SendAsync(
        Guid userId, 
        Guid? organizationId, 
        SendToCrmRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Sending copy to CRM for user {UserId}, record '{RecordName}' ({ObjectType})", 
            userId, 
            request.RecordName, 
            request.ObjectType);
        
        await _copyHistoryService.AddAsync(
            userId,
            organizationId,
            request.CopyTypeLabel,
            request.Copy,
            request.RecordName,
            request.ObjectType,
            request.RecordId,
            ct);
        
        _logger.LogInformation(
            "Successfully sent copy to CRM for user {UserId}, record '{RecordName}'", 
            userId, 
            request.RecordName);
        
        return new SendToCrmResult(true, $"Copy sent to {request.RecordName} ({request.ObjectType}).");
    }
}
