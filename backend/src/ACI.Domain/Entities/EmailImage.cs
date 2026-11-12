namespace ACI.Domain.Entities;

/// <summary>
/// An image for a workspace's emails (the welcome email first), served at a public
/// address so any mail app can show it.
/// </summary>
/// <remarks>
/// Mail apps show only images they can fetch from the web: one embedded in the HTML is
/// blocked by most, and one on someone's computer reaches nobody. So an image is kept
/// here and its address, which no one can guess, goes into the email.
/// </remarks>
public class EmailImage : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>image/png, image/jpeg, image/gif or image/webp, as read from the file itself.</summary>
    public string ContentType { get; set; } = string.Empty;
    public int Size { get; set; }
    /// <summary>The image's own size in pixels, when the browser that sent it could tell.</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
}
