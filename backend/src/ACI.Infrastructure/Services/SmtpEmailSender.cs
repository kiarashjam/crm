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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send \"{Subject}\" to {Email}", subject, toEmail);
            return false;
        }
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
