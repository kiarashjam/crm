namespace ACI.Application.Interfaces;

public interface IEmailSender
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetUrl, CancellationToken ct = default);

    /// <summary>
    /// Tells someone they have been invited to an organization and points them at the
    /// app, where the pending invitation is waiting to be accepted.
    /// </summary>
    /// <param name="toEmail">Address the invitation was issued to — it must match the account they sign in with.</param>
    /// <param name="organizationName">Organization they have been invited to.</param>
    /// <param name="invitedByName">Who sent the invitation (may be empty).</param>
    /// <param name="acceptUrl">Absolute URL of the page listing their pending invitations.</param>
    Task<bool> SendOrganizationInviteEmailAsync(
        string toEmail,
        string organizationName,
        string invitedByName,
        string acceptUrl,
        CancellationToken ct = default);
}
