namespace ACI.Application.Interfaces;

/// <summary>A file to travel with a message.</summary>
/// <param name="FileName">
/// What the recipient sees and saves. Worth getting right: "contract.pdf" in a
/// folder of thirty contracts is worth nothing.
/// </param>
/// <param name="ContentType">MIME type, e.g. <c>application/pdf</c>.</param>
/// <param name="Content">The bytes.</param>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// One email from the outbox, exactly as it is to go out: who it is from, where a
/// reply goes, both parts, and the headers that let a reply or a delivery report be
/// matched to it later.
/// </summary>
/// <param name="FromName">
/// The name shown beside the sender address — the workspace's, so a lead sees
/// "Pavillon 46", not the product's name. Null falls back to Email:FromName.
/// </param>
/// <param name="FromAddress">
/// The workspace's own address on a domain the mail service has verified. Null: the
/// configured Email:FromAddress, the only one the relay accepts otherwise.
/// </param>
/// <param name="ReplyTo">
/// Where a reply goes: the person behind the message, and the reply-capture address
/// when replies come back into the CRM. Every workspace sends from one configured
/// address, so without it a prospect's reply landed in that shared inbox.
/// </param>
/// <param name="RecipientName">The name to show beside a single To address.</param>
/// <param name="MessageId">The Message-ID header, angle brackets included.</param>
/// <param name="UnsubscribeUrl">
/// The page that stops this workspace emailing the recipient, sent as the
/// List-Unsubscribe header so mail apps can offer it next to the sender. The same link
/// is in the footer of both parts.
/// </param>
/// <param name="OutboundMessageId">The outbox's id for it, which the mail service's reports carry back.</param>
/// <param name="OneClickUnsubscribe">
/// Whether <paramref name="UnsubscribeUrl"/> takes the one-click POST of RFC 8058, so the
/// message also says List-Unsubscribe-Post and a mail app's own Unsubscribe button works
/// without opening a page.
/// </param>
public sealed record OutboxEmail(
    string? FromName,
    string? FromAddress,
    IReadOnlyList<string> ReplyTo,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string? RecipientName,
    string Subject,
    string TextBody,
    string HtmlBody,
    string MessageId,
    string? UnsubscribeUrl,
    Guid OutboundMessageId,
    bool OneClickUnsubscribe = false);

/// <summary>What the outbox puts on a message for the mail service to report back.</summary>
public static class OutboxEmailTags
{
    /// <summary>
    /// The custom argument holding the outbox's id for a message. SendGrid copies it from
    /// the X-SMTPAPI header into every event it reports about that message.
    /// </summary>
    public const string MessageIdArg = "cadence_message_id";
}

/// <summary>What happened to one attempt at sending an outbox email.</summary>
public enum OutboxSendOutcome
{
    /// <summary>The mail server took it.</summary>
    Sent,
    /// <summary>No mail server is set up, so nothing was attempted.</summary>
    NotConfigured,
    /// <summary>It did not go, for a reason that may pass: the server was unreachable, busy or refused us for now.</summary>
    Retry,
    /// <summary>The mail server refused this message for good: a recipient it will not take, say. Trying again cannot help.</summary>
    Rejected,
}

/// <param name="Error">Why it did not go, in a form fit to show the person who sent it.</param>
/// <param name="Code">The mail server's reply code, when it gave one.</param>
public sealed record OutboxSendResult(OutboxSendOutcome Outcome, string? Error = null, int? Code = null)
{
    public static readonly OutboxSendResult Sent = new(OutboxSendOutcome.Sent);
    public static readonly OutboxSendResult NotConfigured = new(OutboxSendOutcome.NotConfigured, "No mail server is set up.");
}

public interface IEmailSender
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetUrl, CancellationToken ct = default);

    /// <summary>
    /// Asks a new account to confirm its address by opening <paramref name="verifyUrl"/>.
    /// </summary>
    Task<bool> SendEmailVerificationEmailAsync(string toEmail, string recipientName, string verifyUrl, CancellationToken ct = default);

    /// <summary>
    /// Tells someone they have been invited to an organization, with the link that accepts it.
    /// </summary>
    /// <param name="toEmail">Address the invitation was issued to — it must match the account they sign in with.</param>
    /// <param name="organizationName">Organization they have been invited to.</param>
    /// <param name="invitedByName">Who sent the invitation (may be empty).</param>
    /// <param name="acceptUrl">Absolute URL of the accept page, carrying the invitation's one-time token.</param>
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

    /// <summary>A plain message under the product's name, e.g. a contract reminder.</summary>
    Task<bool> SendMessageAsync(string toEmail, string recipientName, string subject, string body, CancellationToken ct = default);

    /// <summary>
    /// Sends one email from the outbox — composer, assistant, sequence and automation
    /// mail all come this way. Says whether it went and, when it did not, whether
    /// trying again could help. Never throws for a delivery problem: the outbox decides
    /// what happens next.
    /// </summary>
    Task<OutboxSendResult> SendOutboxAsync(OutboxEmail email, CancellationToken ct = default);
}
