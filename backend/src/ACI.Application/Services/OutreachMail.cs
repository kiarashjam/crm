using ACI.Application.Common;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

/// <summary>Who a workspace's email goes out as, and who a reply reaches.</summary>
/// <param name="WorkspaceName">Shown beside the configured sender address. Null when there is no workspace.</param>
/// <param name="ReplyTo">The address replies go to, or null when nobody suitable has one.</param>
public sealed record OutreachSender(string? WorkspaceName, string? ReplyTo);

/// <summary>
/// The sending side of email that leaves on a workspace's behalf: the composer,
/// sequences and automations.
/// </summary>
public interface IOutreachMail
{
    /// <summary>
    /// The workspace's name to send under, and the email of the person a reply
    /// should reach: <paramref name="replyToUserId"/> while they are still in the
    /// workspace, otherwise its owner.
    /// </summary>
    Task<OutreachSender> SenderAsync(Guid? organizationId, Guid? replyToUserId, CancellationToken ct = default);
}

/// <remarks>
/// Every workspace sends from the one configured address (Email:FromAddress), the
/// only one the relay has verified. Sequence and automation mail went out as
/// "Cadence", and nothing set Reply-To, so a prospect who answered wrote to that
/// shared inbox — another company's, for any workspace but the one that owns it.
/// </remarks>
public sealed class OutreachMail : IOutreachMail
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;

    public OutreachMail(IOrganizationRepository organizations, IUserRepository users)
    {
        _organizations = organizations;
        _users = users;
    }

    public async Task<OutreachSender> SenderAsync(Guid? organizationId, Guid? replyToUserId, CancellationToken ct = default)
    {
        var workspace = organizationId is Guid id ? await _organizations.GetByIdAsync(id, ct) : null;
        foreach (var candidate in new[] { replyToUserId, workspace?.OwnerUserId }.Distinct())
        {
            if (candidate is not Guid userId) continue;
            // Someone who has left no longer answers for this workspace.
            if (organizationId is Guid org && await _organizations.GetMemberRoleAsync(userId, org, ct) == null) continue;
            var email = (await _users.GetByIdAsync(userId, ct))?.Email?.Trim();
            if (!string.IsNullOrEmpty(email) && ValidationHelper.IsValidEmail(email))
                return new OutreachSender(workspace?.Name, email);
        }
        return new OutreachSender(workspace?.Name, null);
    }
}
