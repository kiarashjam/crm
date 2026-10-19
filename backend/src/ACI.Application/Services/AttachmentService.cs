using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Files attached to leads, contacts, companies and deals, scoped to the caller's
/// workspace. Downloads go through signed links rather than the caller's token,
/// because the client opens them as plain links.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    /// <summary>The largest file accepted, in bytes.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    private readonly IAttachmentRepository _repository;
    private readonly IWorkspaceRecords _records;
    private readonly IAttachmentLinks _links;

    public AttachmentService(IAttachmentRepository repository, IWorkspaceRecords records, IAttachmentLinks links)
    {
        _repository = repository;
        _records = records;
        _links = links;
    }

    public async Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default)
    {
        if (entityType == null || !RecordTypes.All.Contains(entityType))
            return Result.Failure<IReadOnlyList<AttachmentDto>>(DomainErrors.Attachment.InvalidEntityType);

        // The record has to be one the caller can see: the files of a private deal, and
        // their download links, are as private as the deal.
        if (!await _records.ExistsAsync(entityType, recordId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyList<AttachmentDto>>(DomainErrors.Attachment.RecordNotFound);

        var rows = await _repository.ListAsync(userId, organizationId, entityType, recordId, ct);
        return Result.Success<IReadOnlyList<AttachmentDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<AttachmentDto>> UploadAsync(
        Guid userId, string? userName, Guid? organizationId, AttachmentUpload upload, CancellationToken ct = default)
    {
        if (!RecordTypes.All.Contains(upload.EntityType))
            return Result.Failure<AttachmentDto>(DomainErrors.Attachment.InvalidEntityType);
        if (upload.Content.Length == 0)
            return Result.Failure<AttachmentDto>(DomainErrors.Attachment.Empty);
        if (upload.Content.Length > MaxBytes)
            return Result.Failure<AttachmentDto>(DomainErrors.Attachment.TooLarge);
        if (!await _records.ExistsAsync(upload.EntityType, upload.RecordId, userId, organizationId, ct))
            return Result.Failure<AttachmentDto>(DomainErrors.Attachment.RecordNotFound);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            EntityType = upload.EntityType,
            RecordId = upload.RecordId,
            FileName = SafeFileName(upload.FileName),
            ContentType = SafeContentType(upload.ContentType),
            Size = upload.Content.Length,
            Content = upload.Content,
            UploadedByName = Clip(string.IsNullOrWhiteSpace(userName) ? "Someone" : userName.Trim(), 200),
            UploadedAtUtc = DateTime.UtcNow,
        };
        await _repository.AddAsync(attachment, ct);
        return Result.Success(ToDto(attachment));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var attachment = await _repository.FindAsync(id, userId, organizationId, ct);
        if (attachment == null) return Result.Failure(DomainErrors.Attachment.NotFound);
        await _repository.DeleteAsync(id, ct);
        return Result.Success();
    }

    /// <summary>The bytes behind a signed link. An invalid or expired token reads as not found.</summary>
    public async Task<Result<AttachmentContent>> OpenAsync(Guid id, string token, CancellationToken ct = default)
    {
        if (!_links.IsValid(id, token)) return Result.Failure<AttachmentContent>(DomainErrors.Attachment.NotFound);
        var attachment = await _repository.GetWithContentAsync(id, ct);
        return attachment == null
            ? Result.Failure<AttachmentContent>(DomainErrors.Attachment.NotFound)
            : Result.Success(new AttachmentContent(attachment.FileName, attachment.ContentType, attachment.Content));
    }

    private AttachmentDto ToDto(Attachment a) => new(
        a.Id, a.EntityType, a.RecordId, a.FileName, a.ContentType, a.Size,
        $"/api/attachments/{a.Id}/content/{_links.CreateToken(a.Id)}",
        a.UploadedAtUtc, a.UploadedByName);

    /// <summary>The name without any path, control characters or quotes, so it is safe in a header.</summary>
    private static string SafeFileName(string? name)
    {
        var bare = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
        var cleaned = new string(bare.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();
        return Clip(cleaned.Length == 0 ? "file" : cleaned, 255);
    }

    private static string SafeContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Trim();
        // A type/subtype with no parameters is all a download needs, and all we pass back.
        return value.Length is > 0 and <= 127 && value.Count(c => c == '/') == 1 && value.All(c => char.IsLetterOrDigit(c) || "/.+-_".Contains(c))
            ? value.ToLowerInvariant()
            : "application/octet-stream";
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
