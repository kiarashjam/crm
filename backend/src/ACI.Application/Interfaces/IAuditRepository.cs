using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IAuditRepository
{
    /// <summary>
    /// Newest first. In a workspace, its lines; with no workspace, the lines for
    /// records the user owns in their personal workspace.
    /// </summary>
    Task<IReadOnlyList<AuditEvent>> GetAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid? entityId, int take, CancellationToken ct = default);

    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
}
