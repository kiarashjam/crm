namespace ACI.Application.DTOs;

/// <summary>
/// One line of the audit trail. Property names match the frontend's AuditEvent
/// (src/app/api/audit.ts) exactly so no client-side mapping is needed.
/// </summary>
public record AuditEventDto(
    Guid Id,
    string EntityType,
    Guid? EntityId,
    string? EntityLabel,
    string Action,
    string Summary,
    string ActorName,
    DateTime CreatedAtUtc);

/// <summary>
/// A line the client records itself (the frontend's logAudit). Who and when are
/// always the server's: a client-supplied actor name is ignored.
/// </summary>
public record RecordAuditEventRequest(
    string EntityType,
    Guid? EntityId,
    string? EntityLabel,
    string Action,
    string Summary);
