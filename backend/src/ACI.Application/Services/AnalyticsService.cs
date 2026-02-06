using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for copy generation analytics and conversion tracking.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ICopyHistoryRepository _copyHistoryRepository;
    private readonly ILogger<AnalyticsService> _logger;
    
    public AnalyticsService(ICopyHistoryRepository copyHistoryRepository, ILogger<AnalyticsService> logger)
    {
        _copyHistoryRepository = copyHistoryRepository;
        _logger = logger;
    }
    
    public async Task<CopyAnalyticsSummaryDto> GetSummaryAsync(Guid userId, Guid? organizationId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        
        _logger.LogDebug("Getting analytics summary for user {UserId} from {From} to {To}", userId, fromDate, toDate);
        
        // Get all copy history items for the user within date range
        var items = await _copyHistoryRepository.GetByUserIdAsync(userId, ct);
        
        var filtered = items.Where(i => i.CreatedAtUtc >= fromDate && i.CreatedAtUtc <= toDate).ToList();
        
        var totalGenerations = filtered.Count;
        var totalRewrites = filtered.Count(i => i.IsRewritten);
        var totalCopied = filtered.Sum(i => i.CopyCount);
        var totalSent = filtered.Sum(i => i.SendCount);
        var totalResponses = filtered.Sum(i => i.ResponseCount);
        var overallResponseRate = totalSent > 0 ? (double)totalResponses / totalSent * 100 : 0;
        
        var byType = filtered
            .GroupBy(i => i.CopyTypeId.ToString())
            .Select(g => new CopyTypeAnalyticsDto(
                g.Key,
                g.Count(),
                g.Sum(i => i.SendCount),
                g.Sum(i => i.ResponseCount),
                g.Sum(i => i.SendCount) > 0 
                    ? (double)g.Sum(i => i.ResponseCount) / g.Sum(i => i.SendCount) * 100 
                    : 0
            ))
            .OrderByDescending(t => t.GenerationCount)
            .ToList();
        
        var dailyTrend = filtered
            .GroupBy(i => i.CreatedAtUtc.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyAnalyticsDto(
                g.Key,
                g.Count(),
                g.Sum(i => i.SendCount),
                g.Sum(i => i.ResponseCount)
            ))
            .ToList();
        
        _logger.LogInformation(
            "Analytics summary for user {UserId}: {TotalGenerations} generations, {TotalSent} sent, {ResponseRate}% response rate",
            userId, totalGenerations, totalSent, Math.Round(overallResponseRate, 1));
        
        return new CopyAnalyticsSummaryDto(
            totalGenerations,
            totalRewrites,
            totalCopied,
            totalSent,
            totalResponses,
            Math.Round(overallResponseRate, 1),
            byType,
            dailyTrend
        );
    }
    
    public async Task TrackEventAsync(Guid userId, Guid? organizationId, TrackCopyEventRequest request, CancellationToken ct = default)
    {
        if (request.CopyHistoryId == null)
        {
            _logger.LogDebug("TrackEvent called without CopyHistoryId, skipping");
            return;
        }
        
        _logger.LogDebug("Tracking event '{EventType}' for copy history {CopyHistoryId}", request.EventType, request.CopyHistoryId);
        
        var item = await _copyHistoryRepository.GetByIdAsync(request.CopyHistoryId.Value, ct);
        if (item == null || item.UserId != userId)
        {
            _logger.LogWarning("Copy history item {CopyHistoryId} not found or unauthorized for user {UserId}", request.CopyHistoryId, userId);
            return;
        }
        
        switch (request.EventType.ToLowerInvariant())
        {
            case "copy":
                item.CopyCount++;
                _logger.LogDebug("Incremented copy count for {CopyHistoryId}", item.Id);
                break;
            case "send":
                item.SendCount++;
                _logger.LogDebug("Incremented send count for {CopyHistoryId}", item.Id);
                break;
            case "response":
                item.ResponseCount++;
                _logger.LogDebug("Incremented response count for {CopyHistoryId}", item.Id);
                break;
            default:
                _logger.LogWarning("Unknown event type '{EventType}' for {CopyHistoryId}", request.EventType, item.Id);
                return;
        }
        
        await _copyHistoryRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Tracked event '{EventType}' for copy history {CopyHistoryId}", request.EventType, item.Id);
    }
    
    public async Task<IReadOnlyList<CopyConversionDto>> GetConversionsAsync(Guid userId, Guid? organizationId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        
        _logger.LogDebug("Getting conversions for user {UserId} from {From} to {To}", userId, fromDate, toDate);
        
        // For now, we derive conversions from copy history items with responses
        var items = await _copyHistoryRepository.GetByUserIdAsync(userId, ct);
        
        var conversions = items
            .Where(i => i.ResponseCount > 0 && i.CreatedAtUtc >= fromDate && i.CreatedAtUtc <= toDate)
            .Select(i => new CopyConversionDto(
                i.Id,
                i.Id,
                i.CopyTypeId.ToString(),
                null,
                null,
                "Replied",
                null,
                i.CreatedAtUtc
            ))
            .ToList();
        
        _logger.LogInformation("Found {Count} conversions for user {UserId}", conversions.Count, userId);
        return conversions;
    }
    
    public async Task<CopyConversionDto> CreateConversionAsync(Guid userId, Guid? organizationId, CreateConversionRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating conversion for user {UserId}, type: {ConversionType}", userId, request.ConversionType);
        
        // Track response in copy history if linked
        if (request.CopyHistoryId != null)
        {
            var item = await _copyHistoryRepository.GetByIdAsync(request.CopyHistoryId.Value, ct);
            if (item != null && item.UserId == userId)
            {
                item.ResponseCount++;
                await _copyHistoryRepository.UpdateAsync(item, ct);
                _logger.LogDebug("Updated response count for copy history {CopyHistoryId}", request.CopyHistoryId);
            }
        }
        
        var conversionId = Guid.NewGuid();
        _logger.LogInformation("Created conversion {ConversionId} for user {UserId}", conversionId, userId);
        
        return new CopyConversionDto(
            conversionId,
            request.CopyHistoryId,
            request.CopyTypeId,
            request.RecipientEmail,
            request.RecipientName,
            request.ConversionType,
            request.Notes,
            DateTime.UtcNow
        );
    }
}
