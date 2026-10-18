using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Reads the audit trail and records the lines a client sends. Most lines never
/// pass through here: the server writes them as part of the save that changed
/// the record.
/// </summary>
public sealed class AuditService : IAuditService
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    private readonly IAuditRepository _repository;

    public AuditService(IAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<AuditEventDto>>> GetAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid? entityId, int? limit, CancellationToken ct = default)
    {
        var type = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim();
        if (type != null && !AuditVocabulary.EntityTypes.Contains(type))
            return Result.Failure<IReadOnlyList<AuditEventDto>>(DomainErrors.Audit.InvalidEntityType);

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var rows = await _repository.GetAsync(userId, organizationId, type, entityId, take, ct);
        return Result.Success<IReadOnlyList<AuditEventDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<AuditEventDto>> RecordAsync(
        Guid userId, string? userName, Guid? organizationId, RecordAuditEventRequest request, CancellationToken ct = default)
    {
        var type = request.EntityType?.Trim() ?? string.Empty;
        if (!AuditVocabulary.EntityTypes.Contains(type))
            return Result.Failure<AuditEventDto>(DomainErrors.Audit.InvalidEntityType);
        var action = request.Action?.Trim() ?? string.Empty;
        if (!AuditVocabulary.Actions.Contains(action))
            return Result.Failure<AuditEventDto>(DomainErrors.Audit.InvalidAction);
        var summary = request.Summary?.Trim() ?? string.Empty;
        if (summary.Length == 0)
            return Result.Failure<AuditEventDto>(DomainErrors.Audit.SummaryRequired);

        var line = new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            EntityType = type,
            EntityId = request.EntityId,
            EntityLabel = Truncate(request.EntityLabel?.Trim(), 200),
            Action = action,
            Summary = Truncate(summary, 500)!,
            ActorUserId = userId,
            ActorName = Truncate(string.IsNullOrWhiteSpace(userName) ? "Someone" : userName.Trim(), 200)!,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _repository.AddAsync(line, ct);
        return Result.Success(ToDto(line));
    }

    private static AuditEventDto ToDto(AuditEvent e) =>
        new(e.Id, e.EntityType, e.EntityId, e.EntityLabel, e.Action, e.Summary, e.ActorName, e.CreatedAtUtc);

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..(max - 1)] + "…";
}
