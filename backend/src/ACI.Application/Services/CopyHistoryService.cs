using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing copy generation history in the CRM system.
/// </summary>
public class CopyHistoryService : ICopyHistoryService
{
    private readonly ICopyHistoryRepository _repository;
    private readonly ILogger<CopyHistoryService> _logger;

    public CopyHistoryService(ICopyHistoryRepository repository, ILogger<CopyHistoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CopyHistoryItemDto>> GetHistoryAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting copy history for user {UserId}", userId);
        
        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        
        _logger.LogDebug("Retrieved {Count} copy history items for user {UserId}", list.Count, userId);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<CopyHistoryStatsDto> GetStatsAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting copy history stats for user {UserId}", userId);
        
        var total = await _repository.CountByUserIdAsync(userId, organizationId, ct);
        var sentThisWeek = await _repository.CountSentThisWeekAsync(userId, organizationId, ct);
        
        return new CopyHistoryStatsDto(sentThisWeek, total);
    }

    /// <inheritdoc />
    public async Task<CopyHistoryItemDto> AddAsync(
        Guid userId,
        Guid? organizationId,
        string type,
        string copy,
        string recipientName,
        string recipientType,
        string recipientId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Adding copy history item for user {UserId}, type '{Type}', recipient '{RecipientName}'", 
            userId, 
            type, 
            recipientName);
        
        var recipientTypeEnum = ParseRecipientType(recipientType);
        var item = new CopyHistoryItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = type,
            Copy = copy,
            RecipientName = recipientName,
            RecipientType = recipientTypeEnum,
            RecipientId = recipientId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        
        var saved = await _repository.AddAsync(item, ct);
        
        _logger.LogInformation("Successfully added copy history item {ItemId}", saved.Id);
        
        return Map(saved);
    }

    private static RecipientType ParseRecipientType(string s) => s?.ToLowerInvariant() switch
    {
        "contact" => RecipientType.Contact,
        "deal" => RecipientType.Deal,
        "workflow" => RecipientType.Workflow,
        "email" => RecipientType.Email,
        _ => RecipientType.Contact,
    };

    private static string RecipientTypeToString(RecipientType r) => r switch
    {
        RecipientType.Contact => "contact",
        RecipientType.Deal => "deal",
        RecipientType.Workflow => "workflow",
        RecipientType.Email => "email",
        _ => "contact",
    };

    private static CopyHistoryItemDto Map(CopyHistoryItem e) =>
        new CopyHistoryItemDto(
            e.Id, 
            e.Type, 
            e.Copy, 
            e.RecipientName, 
            RecipientTypeToString(e.RecipientType), 
            e.RecipientId, 
            e.CreatedAtUtc
        );
}
