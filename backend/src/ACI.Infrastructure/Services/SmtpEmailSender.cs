using ACI.Application.Configuration;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Sends transactional email over SMTP (MailKit).
///
/// Works with any SMTP relay. For SendGrid, set Email:SmtpHost to
/// smtp.sendgrid.net, Email:SmtpUser to the literal "apikey" and
/// Email:SmtpPassword to the API key — supplied by the environment, never
/// committed. Email:FromAddress must be a sender SendGrid has verified,
/// otherwise the relay rejects the message.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            if (_options.LogResetLinksWhenSmtpNotConfigured)
            {
                _logger.LogWarning(
                    "Email:SmtpHost is not set; logging password reset link for {Email}: {ResetUrl}",
                    toEmail,
                    resetUrl);
                return true;
            }

            _logger.LogError("Email:SmtpHost is not configured; cannot send password reset email to {Email}", toEmail);
            return false;
        }

        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName;
        var body =
            $"Hi{greeting},\n\n" +
            "We received a request to reset your password. Open the link below to choose a new password. " +
            "This link expires in one hour.\n\n" +
            $"{resetUrl}\n\n" +
            "If you did not request this, you can ignore this email.\n";

        return await SendAsync(toEmail, recipientName, "Reset your Cadence password", body, ct);
    }

    public async Task<bool> SendOrganizationInviteEmailAsync(
        string toEmail,
        string organizationName,
        string invitedByName,
        string acceptUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            if (_options.LogResetLinksWhenSmtpNotConfigured)
            {
                _logger.LogWarning(
                    "Email:SmtpHost is not set; logging invitation for {Email} to {Organization}: {AcceptUrl}",
                    toEmail, organizationName, acceptUrl);
                return true;
            }

            _logger.LogError("Email:SmtpHost is not configured; cannot send invitation email to {Email}", toEmail);
            return false;
        }

        var from = string.IsNullOrWhiteSpace(invitedByName) ? "Someone" : invitedByName;
        var body =
            $"Hi,\n\n" +
            $"{from} invited you to join \"{organizationName}\" on Cadence.\n\n" +
            $"Open the link below and sign in to accept — the invitation is waiting there:\n\n" +
            $"{acceptUrl}\n\n" +
            $"Important: sign in (or create your account) with this email address — {toEmail} — " +
            "otherwise the invitation will not appear.\n\n" +
            "This invitation expires in 7 days. If you were not expecting it, you can ignore this email.\n";

        return await SendAsync(toEmail, recipientName: "", subject: $"You have been invited to {organizationName} on Cadence", body: body, ct: ct);
    }

    public async Task<bool> SendTaskReminderEmailAsync(
        string toEmail,
        string recipientName,
        string taskTitle,
        DateTime? dueDateUtc,
        string taskUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            if (_options.LogResetLinksWhenSmtpNotConfigured)
            {
                _logger.LogWarning(
                    "Email:SmtpHost is not set; logging task reminder for {Email}: {TaskTitle} ({TaskUrl})",
                    toEmail, taskTitle, taskUrl);
                return true;
            }

            _logger.LogError("Email:SmtpHost is not configured; cannot send task reminder to {Email}", toEmail);
            return false;
        }

        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName;
        var due = dueDateUtc.HasValue
            ? $"Due: {dueDateUtc.Value:dddd d MMMM yyyy 'at' HH:mm} UTC\n"
            : string.Empty;
        var body =
            $"Hi{greeting},\n\n" +
            $"Reminder about your task:\n\n" +
            $"  {taskTitle}\n\n" +
            due +
            $"\nOpen it here:\n{taskUrl}\n\n" +
            "You are receiving this because task reminders are on in your notification settings.\n";

        return await SendAsync(toEmail, recipientName, $"Reminder: {taskTitle}", body, ct);
    }

    public Task<bool> SendMessageAsync(string toEmail, string recipientName, string subject, string body, CancellationToken ct = default) =>
        SendAsync(toEmail, recipientName, subject, body, ct);

    /// <summary>
    /// Outreach for a workspace. Sent from the configured address — the one the relay
    /// has verified — but under the workspace's name, with Reply-To set to its
    /// person. It used to go out as "Cadence", and every reply went to the shared
    /// sender inbox.
    /// </summary>
    public async Task<bool> SendOutreachAsync(OutreachEmail email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning("Email:SmtpHost or Email:FromAddress is not set, so outreach \"{Subject}\" was NOT sent", email.Subject);
            return false;
        }

        try
        {
            await TransmitAsync(BuildOutreachMessage(email, _options.FromName, _options.FromAddress), ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outreach \"{Subject}\" to {Email}", email.Subject, email.To);
            return false;
        }
    }

    /// <summary>The MIME message for one piece of outreach.</summary>
    public static MimeMessage BuildOutreachMessage(OutreachEmail email, string defaultFromName, string fromAddress)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(email.FromName) ? defaultFromName : email.FromName.Trim(), fromAddress));
        AddReplyTo(message, email.ReplyTo);
        message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(email.RecipientName) ? email.To : email.RecipientName, email.To));
        message.Subject = email.Subject;
        // The page, not a one-click POST endpoint (RFC 8058): the link opens the
        // confirmation page, which is what a mail app's "Unsubscribe" does with it.
        if (!string.IsNullOrWhiteSpace(email.UnsubscribeUrl))
            message.Headers.Add("List-Unsubscribe", $"<{email.UnsubscribeUrl.Trim()}>");
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();
        return message;
    }

    /// <summary>Replies go to this address, when it is one.</summary>
    private static void AddReplyTo(MimeMessage message, string? replyTo)
    {
        if (!string.IsNullOrWhiteSpace(replyTo) && MailboxAddress.TryParse(replyTo.Trim(), out var address))
            message.ReplyTo.Add(address);
    }

    /// <summary>
    /// Builds and delivers one plain-text message. Returns false (never throws) so a
    /// delivery problem cannot fail the caller's operation.
    /// </summary>
    private async Task<bool> SendAsync(string toEmail, string recipientName, string subject, string body, CancellationToken ct)
        => await SendAsync(toEmail, recipientName, subject, body, null, null, ct);

    /// <summary>
    /// Builds and delivers one message, with an optional HTML alternative.
    /// Returns false (never throws) so a delivery problem cannot fail the
    /// caller's operation.
    /// </summary>
    private async Task<bool> SendAsync(
        string toEmail, string recipientName, string subject, string body, string? htmlBody,
        EmailAttachment? attachment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogError("Email:FromAddress is not configured; cannot send \"{Subject}\" to {Email}", subject, toEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(recipientName) ? toEmail : recipientName, toEmail));
            message.Subject = subject;
            // Both parts when HTML is supplied: the text alternative is what a
            // plain-text client, a screen reader, or an archive-to-text rule sees,
            // and for a contract that copy has to be complete on its own.
            var builder = new BodyBuilder { TextBody = body, HtmlBody = htmlBody };
            if (attachment is not null && attachment.Content.Length > 0)
            {
                // MimeKit picks base64 for a binary part on its own; what it must be
                // told is the type, or a PDF arrives as application/octet-stream and
                // some clients refuse to preview it.
                builder.Attachments.Add(
                    attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
            message.Body = builder.ToMessageBody();

            await TransmitAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send \"{Subject}\" to {Email}", subject, toEmail);
            return false;
        }
    }

    /// <summary>Hands one message to the configured SMTP relay. Throws on failure.</summary>
    private async Task TransmitAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        var secure = !_options.UseSsl
            ? SecureSocketOptions.Auto
            : _options.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secure, ct);

        if (!string.IsNullOrEmpty(_options.SmtpUser))
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    // ── Composed in the CRM ──────────────────────────────────────────────────

    /// <summary>
    /// Sends a message someone wrote in the composer. Unlike the reminders above,
    /// an unset SmtpHost is NOT treated as success — the composer would tell the
    /// person their email went when it did not — and the answer says which way it
    /// failed, so the composer can say whether trying again could help.
    /// </summary>
    public async Task<ComposedEmailOutcome> SendComposedAsync(ComposedEmail email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning("Email:SmtpHost or Email:FromAddress is not set, so a composed email was NOT sent");
            return ComposedEmailOutcome.NotConfigured;
        }

        try
        {
            var fromName = string.IsNullOrWhiteSpace(email.FromName) ? _options.FromName : email.FromName.Trim();
            await TransmitAsync(BuildComposedMessage(email, fromName, _options.FromAddress), ct);
            return ComposedEmailOutcome.Sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send composed email \"{Subject}\"", email.Subject);
            return ComposedEmailOutcome.Failed;
        }
    }

    /// <summary>The MIME message for a composed email: every recipient, both parts.</summary>
    public static MimeMessage BuildComposedMessage(ComposedEmail email, string fromName, string fromAddress)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        AddReplyTo(message, email.ReplyTo);
        message.To.AddRange(email.To.Select(MailboxAddress.Parse));
        message.Cc.AddRange(email.Cc.Select(MailboxAddress.Parse));
        message.Bcc.AddRange(email.Bcc.Select(MailboxAddress.Parse));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();
        return message;
    }

    // ── Contracts ────────────────────────────────────────────────────────────
    //
    // These three deliberately BREAK the convention used above. The other methods
    // return `true` when SmtpHost is unset and LogResetLinksWhenSmtpNotConfigured
    // is on, so a developer environment can pretend a reminder went out. For a
    // contract that is exactly the wrong answer: the caller would record a
    // counterparty as asked-to-sign when nothing left the building. So they log
    // the link and return FALSE, and the caller surfaces that.

    /// <summary>True when SMTP is configured well enough to attempt a send.</summary>
    private bool SmtpConfigured(string what, string toEmail, string? linkForTheLog)
    {
        if (!string.IsNullOrWhiteSpace(_options.SmtpHost)) return true;

        _logger.LogWarning(
            "Email:SmtpHost is not set, so the {What} for {Email} was NOT sent. " +
            "Pass the link on by hand if needed: {Link}",
            what, toEmail, linkForTheLog ?? "(no link)");
        return false;
    }

    // The wording and markup live in ContractEmailContent, so they can be tested
    // and rendered without opening an SMTP connection. This class keeps only the
    // decision of whether to send and the reporting of whether it went.

    public async Task<bool> SendContractForSignatureEmailAsync(
        string toEmail, string recipientName, string organizationName,
        string contractTitle, string signUrl,
        EmailAttachment? attachment = null, CancellationToken ct = default)
    {
        if (!SmtpConfigured("contract signature request", toEmail, signUrl)) return false;
        var email = ContractEmailContent.ForSignature(
            _options.FromName, recipientName, organizationName, contractTitle, signUrl,
            hasAttachment: attachment is not null);
        return await SendAsync(toEmail, recipientName, email.Subject, email.Text, email.Html, attachment, ct);
    }

    public async Task<bool> SendContractSignedNotificationAsync(
        string toEmail, string recipientName, string counterpartyName,
        string contractTitle, string contractUrl, CancellationToken ct = default)
    {
        if (!SmtpConfigured("contract signed notification", toEmail, contractUrl)) return false;
        var email = ContractEmailContent.SignedNotification(
            _options.FromName, recipientName, counterpartyName, contractTitle, contractUrl);
        return await SendAsync(toEmail, recipientName, email.Subject, email.Text, email.Html, null, ct);
    }

    public async Task<bool> SendExecutedContractEmailAsync(
        string toEmail, string recipientName, string organizationName,
        string contractTitle, string contractBody, string signatureBlock,
        EmailAttachment? attachment = null, CancellationToken ct = default)
    {
        if (!SmtpConfigured("executed contract copy", toEmail, null)) return false;
        var email = ContractEmailContent.Executed(
            _options.FromName, recipientName, organizationName, contractTitle,
            contractBody, signatureBlock, hasAttachment: attachment is not null);
        return await SendAsync(toEmail, recipientName, email.Subject, email.Text, email.Html, attachment, ct);
    }
}
