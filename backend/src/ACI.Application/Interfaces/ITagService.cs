using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>A workspace's tags, and the tags on its records.</summary>
public interface ITagService
{
    Task<IReadOnlyList<TagDto>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<Result<TagDto>> CreateAsync(Guid userId, Guid? organizationId, CreateTagRequest request, CancellationToken ct = default);

    Task<Result<TagDto>> UpdateAsync(Guid userId, Guid? organizationId, Guid id, UpdateTagRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyList<Guid>>> RecordTagsAsync(Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<Guid>>> SetRecordTagsAsync(Guid userId, Guid? organizationId, SetRecordTagsRequest request, CancellationToken ct = default);

    /// <summary>The tags of many records at once, for a list: only records the caller can see answer.</summary>
    Task<Result<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>> RecordTagsBatchAsync(
        Guid userId, Guid? organizationId, RecordTagsBatchRequest request, CancellationToken ct = default);

    Task<Result<BulkTagResult>> BulkAsync(Guid userId, Guid? organizationId, BulkTagRequest request, CancellationToken ct = default);
}
