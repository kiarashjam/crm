using ACI.Application.Configuration;
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
            message.Body = new BodyBuilder { TextBody = body }.ToMessageBody();

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
}
