namespace ACI.Application.DTOs;

/// <summary>
/// An attachment as the client lists it (src/app/api/attachments.ts). <see cref="Url"/>
/// is a short-lived signed link, so it works as a plain link with no Authorization header.
/// </summary>
public record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid RecordId,
    string FileName,
    string ContentType,
    long Size,
    string Url,
    DateTime UploadedAtUtc,
    string UploadedByName);

/// <summary>An attachment's bytes, for streaming a download.</summary>
public record AttachmentContent(string FileName, string ContentType, byte[] Content);

/// <summary>A file arriving from the client, already read off the request.</summary>
public record AttachmentUpload(string EntityType, Guid RecordId, string FileName, string? ContentType, byte[] Content);
