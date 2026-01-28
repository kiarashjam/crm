using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Services;

public class CopyHistoryService : ICopyHistoryService
{
    private readonly ICopyHistoryRepository _repository;

    public CopyHistoryService(ICopyHistoryRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CopyHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<CopyHistoryStatsDto> GetStatsAsync(Guid userId, CancellationToken ct = default)
    {
        var total = await _repository.CountByUserIdAsync(userId, ct);
        var sentThisWeek = await _repository.CountSentThisWeekAsync(userId, ct);
        return new CopyHistoryStatsDto(sentThisWeek, total);
    }

    public async Task<CopyHistoryItemDto> AddAsync(
        Guid userId,
        string type,
        string copy,
        string recipientName,
        string recipientType,
        string recipientId,
        CancellationToken ct = default)
    {
        var recipientTypeEnum = ParseRecipientType(recipientType);
        var item = new CopyHistoryItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Copy = copy,
            RecipientName = recipientName,
            RecipientType = recipientTypeEnum,
            RecipientId = recipientId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        var saved = await _repository.AddAsync(item, ct);
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
        new CopyHistoryItemDto(e.Id, e.Type, e.Copy, e.RecipientName, RecipientTypeToString(e.RecipientType), e.RecipientId, e.CreatedAtUtc);
}
