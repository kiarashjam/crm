namespace ACI.Application.Interfaces;

/// <summary>
/// Signed, time-limited download tokens. A download is opened with a plain link,
/// which carries no Authorization header, so the token is the whole permission.
/// </summary>
public interface IAttachmentLinks
{
    string CreateToken(Guid attachmentId);

    bool IsValid(Guid attachmentId, string token);
}
