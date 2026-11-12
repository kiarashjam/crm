using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>An image in the workspace's library, with the public address to put in an email.</summary>
public sealed record EmailImageDto(Guid Id, string Url, string FileName, string ContentType, int Size, int? Width, int? Height, DateTime CreatedAtUtc);

/// <summary>What was sent to be kept: the file, and its size in pixels if the browser could tell.</summary>
public sealed record EmailImageUpload(string? FileName, byte[] Content, int? Width, int? Height);

/// <summary>Images for a workspace's emails, kept here and served at public addresses.</summary>
public interface IEmailImageService
{
    Task<IReadOnlyList<EmailImageDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<EmailImageDto>> UploadAsync(Guid organizationId, Guid userId, EmailImageUpload upload, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    /// <summary>The image behind a public address, or null.</summary>
    Task<EmailImage?> GetPublicAsync(Guid id, CancellationToken ct = default);
}

public sealed class EmailImageService : IEmailImageService
{
    /// <summary>Most mail apps start to struggle past a megabyte or two; a photo for an email fits well under this.</summary>
    public const int MaxBytes = 3 * 1024 * 1024;
    /// <summary>Plenty for a club's templates, and a ceiling on what one workspace keeps.</summary>
    public const int MaxImages = 300;

    private readonly IEmailImageRepository _images;
    private readonly EmailSettings _settings;

    public EmailImageService(IEmailImageRepository images, IOptions<EmailSettings> settings)
    {
        _images = images;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<EmailImageDto>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        (await _images.ListAsync(organizationId, MaxImages, ct)).Select(ToDto).ToList();

    public async Task<Result<EmailImageDto>> UploadAsync(Guid organizationId, Guid userId, EmailImageUpload upload, CancellationToken ct = default)
    {
        if (upload.Content.Length == 0) return new Error("EmailImage.Empty", "Choose an image to upload.");
        if (upload.Content.Length > MaxBytes)
            return new Error("EmailImage.TooLarge", $"An image for an email can be at most {MaxBytes / (1024 * 1024)} MB. Make it smaller (1200 px wide is plenty) and try again.");
        // What the file is, read from its first bytes, never from its name or what the
        // browser claimed: an SVG or an HTML page renamed .png is refused here.
        if (Sniff(upload.Content) is not { } type)
            return new Error("EmailImage.Type", "Use a PNG, JPEG, GIF or WebP image. Mail apps do not show other kinds, such as SVG or HEIC.");
        if (await _images.CountAsync(organizationId, ct) >= MaxImages)
            return new Error("EmailImage.TooMany", $"The workspace keeps at most {MaxImages} email images. Remove some you no longer use first.");

        var image = new EmailImage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FileName = FileName(upload.FileName, type.Extension),
            ContentType = type.ContentType,
            Size = upload.Content.Length,
            Width = upload.Width is > 0 and <= 20000 ? upload.Width : null,
            Height = upload.Height is > 0 and <= 20000 ? upload.Height : null,
            Content = upload.Content,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        await _images.AddAsync(image, ct);
        return ToDto(image);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        await _images.DeleteAsync(id, organizationId, ct)
            ? Result.Success()
            : new Error("EmailImage.NotFound", "That image was not found.");

    public Task<EmailImage?> GetPublicAsync(Guid id, CancellationToken ct = default) => _images.GetPublicAsync(id, ct);

    /// <summary>The image's extension for its address: some mail apps look at it.</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/webp" => "webp",
        _ => "bin",
    };

    // The id in its full form, so a copy of the workspace (see OrganizationCloner) finds it
    // in a template's HTML and points the copy's emails at the copy's image.
    private EmailImageDto ToDto(EmailImage i) => new(
        i.Id,
        $"{_settings.ApiBaseUrl.TrimEnd('/')}/api/public/email-images/{i.Id:D}.{Extension(i.ContentType)}",
        i.FileName, i.ContentType, i.Size, i.Width, i.Height, i.CreatedAtUtc);

    private sealed record ImageType(string ContentType, string Extension);

    private static ImageType? Sniff(byte[] b)
    {
        bool Starts(params byte[] magic) => b.Length >= magic.Length && magic.Select((x, i) => b[i] == x).All(x => x);
        if (Starts(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) return new("image/png", "png");
        if (Starts(0xFF, 0xD8, 0xFF)) return new("image/jpeg", "jpg");
        if (Starts(0x47, 0x49, 0x46, 0x38)) return new("image/gif", "gif");
        if (b.Length >= 12 && Starts(0x52, 0x49, 0x46, 0x46) && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            return new("image/webp", "webp");
        return null;
    }

    /// <summary>A name to show in the library: the one given, tidied, with the right extension.</summary>
    private static string FileName(string? given, string extension)
    {
        var name = Path.GetFileNameWithoutExtension((given ?? "").Trim());
        name = new string(name.Where(c => !char.IsControl(c) && c is not ('/' or '\\' or '"' or '<' or '>')).ToArray()).Trim();
        if (name.Length == 0) name = "image";
        if (name.Length > 150) name = name[..150];
        return $"{name}.{extension}";
    }
}
