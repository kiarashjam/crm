using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>A workspace's images for its emails.</summary>
public interface IEmailImageRepository
{
    /// <summary>The workspace's images, newest first, without their bytes.</summary>
    Task<IReadOnlyList<EmailImage>> ListAsync(Guid organizationId, int take, CancellationToken ct = default);

    Task<int> CountAsync(Guid organizationId, CancellationToken ct = default);

    Task AddAsync(EmailImage image, CancellationToken ct = default);

    /// <summary>Deletes one of the workspace's images; false when it has none by that id.</summary>
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// An image by its id alone, whichever workspace it belongs to: for the public address
    /// a mail app fetches, where there is no workspace, only the id in the email.
    /// </summary>
    Task<EmailImage?> GetPublicAsync(Guid id, CancellationToken ct = default);
}
