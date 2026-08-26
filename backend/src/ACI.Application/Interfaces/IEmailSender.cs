namespace ACI.Application.Interfaces;

/// <summary>A file to travel with a message.</summary>
/// <param name="FileName">
/// What the recipient sees and saves. Worth getting right: "contract.pdf" in a
/// folder of thirty contracts is worth nothing.
/// </param>
/// <param name="ContentType">MIME type, e.g. <c>application/pdf</c>.</param>
/// <param name="Content">The bytes.</param>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

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

    /// <summary>
    /// Reminds someone about a task whose reminder time has arrived.
    /// </summary>
    /// <param name="toEmail">Recipient — the task's assignee, or its owner when unassigned.</param>
    /// <param name="recipientName">Recipient's display name (may be empty).</param>
    /// <param name="taskTitle">Task title.</param>
    /// <param name="dueDateUtc">When the task is due, if it has a due date.</param>
    /// <param name="taskUrl">Absolute URL of the task.</param>
    Task<bool> SendTaskReminderEmailAsync(
        string toEmail,
        string recipientName,
        string taskTitle,
        DateTime? dueDateUtc,
        string taskUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Sends the counterparty a link to read and sign a contract.
    /// </summary>
    /// <remarks>
    /// The caller must treat <c>false</c> as a real outcome and surface it. Every
    /// other email here is a nicety whose loss costs a reminder; this one is a step
    /// in a contract, and silently believing it was sent leaves a counterparty who
    /// was never asked to sign and a CRM that says they were.
    /// </remarks>
    /// <param name="toEmail">The counterparty.</param>
    /// <param name="recipientName">Their name (may be empty).</param>
    /// <param name="organizationName">Who is asking them to sign.</param>
    /// <param name="contractTitle">Title of the contract.</param>
    /// <param name="signUrl">Absolute URL of the signing page, carrying the token.</param>
    /// <param name="attachment">
    /// The unsigned copy, watermarked, so the counterparty can read the whole
    /// agreement in their own time before deciding to follow a link and sign it.
    /// </param>
    Task<bool> SendContractForSignatureEmailAsync(
        string toEmail,
        string recipientName,
        string organizationName,
        string contractTitle,
        string signUrl,
        EmailAttachment? attachment = null,
        CancellationToken ct = default);

    /// <summary>
    /// Tells the CRM user the counterparty has signed and it is their turn.
    /// </summary>
    Task<bool> SendContractSignedNotificationAsync(
        string toEmail,
        string recipientName,
        string counterpartyName,
        string contractTitle,
        string contractUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Sends the fully executed contract to one party.
    /// </summary>
    /// <remarks>
    /// The whole contract text goes in the BODY as well as in the attachment, and
    /// that redundancy is deliberate. The inline copy is readable in every mail
    /// client, quotable in a reply, and survives an archive-to-text rule, where a
    /// missing or stripped attachment is a support conversation. The attachment is
    /// the copy somebody files and prints. The signature block carries both names,
    /// both timestamps and the body hash, so the email is evidence on its own even
    /// if the file never arrives.
    /// </remarks>
    /// <param name="signatureBlock">Rendered "signed by / on / hash" summary.</param>
    /// <param name="attachment">The typeset copy, or null if it could not be rendered.</param>
    Task<bool> SendExecutedContractEmailAsync(
        string toEmail,
        string recipientName,
        string organizationName,
        string contractTitle,
        string contractBody,
        string signatureBlock,
        EmailAttachment? attachment = null,
        CancellationToken ct = default);
}
