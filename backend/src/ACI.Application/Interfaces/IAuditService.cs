using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IAuditService
{
    Task<Result<IReadOnlyList<AuditEventDto>>> GetAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid? entityId, int? limit, CancellationToken ct = default);

    Task<Result<AuditEventDto>> RecordAsync(
        Guid userId, string? userName, Guid? organizationId, RecordAuditEventRequest request, CancellationToken ct = default);
}
