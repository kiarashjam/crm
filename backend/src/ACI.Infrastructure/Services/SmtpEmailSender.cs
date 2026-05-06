using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ACI.Infrastructure.Services;

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

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogError("Email:FromAddress is not configured; cannot send password reset email");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(recipientName) ? toEmail : recipientName, toEmail));
            message.Subject = "Reset your Cadence password";

            var body = new BodyBuilder
            {
                TextBody =
                    $"Hi{(string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName)},\n\n" +
                    "We received a request to reset your password. Open the link below to choose a new password. " +
                    "This link expires in one hour.\n\n" +
                    $"{resetUrl}\n\n" +
                    "If you did not request this, you can ignore this email.\n",
            };
            message.Body = body.ToMessageBody();

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
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }
}
