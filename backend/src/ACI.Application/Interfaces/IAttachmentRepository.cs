using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IAttachmentRepository
{
    /// <summary>A record's attachments in the caller's workspace, newest first, without their bytes.</summary>
    Task<IReadOnlyList<Attachment>> ListAsync(
        Guid userId, Guid? organizationId, string entityType, Guid recordId, CancellationToken ct = default);

    /// <summary>One attachment in the caller's workspace, without its bytes.</summary>
    Task<Attachment?> FindAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>One attachment with its bytes. Callers must already have proven access.</summary>
    Task<Attachment?> GetWithContentAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Attachment attachment, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
