using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IAttachmentService
{
    Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default);

    Task<Result<AttachmentDto>> UploadAsync(
        Guid userId, string? userName, Guid? organizationId, AttachmentUpload upload, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result<AttachmentContent>> OpenAsync(Guid id, string token, CancellationToken ct = default);
}
