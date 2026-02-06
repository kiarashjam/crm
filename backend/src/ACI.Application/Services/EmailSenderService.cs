using System.Net;
using System.Net.Mail;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

public class EmailSenderService : IEmailSenderService
{
    // In-memory storage for demo purposes
    // In production, this should be stored in the database
    private static readonly Dictionary<string, SmtpSettings> _settings = new();
    
    private static string GetSettingsKey(Guid userId, Guid? orgId) =>
        orgId.HasValue ? $"org_{orgId}" : $"user_{userId}";

    public async Task<SendEmailResult> SendEmailAsync(SendEmailRequest request, Guid userId, Guid? orgId, CancellationToken ct = default)
    {
        var settings = await GetSmtpSettingsAsync(userId, orgId, ct);
        if (settings == null || !settings.IsValid)
        {
            return new SendEmailResult
            {
                Success = false,
                Error = "SMTP settings not configured or invalid. Please configure your email settings first."
            };
        }

        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                Credentials = new NetworkCredential(settings.Username, settings.Password),
                Timeout = 30000, // 30 seconds
            };

            var fromAddress = new MailAddress(
                settings.FromEmail ?? settings.Username,
                settings.FromName ?? "CRM System"
            );

            var toAddress = new MailAddress(request.To, request.ToName);

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = request.IsHtml,
            };

            if (!string.IsNullOrEmpty(request.ReplyTo))
            {
                message.ReplyToList.Add(new MailAddress(request.ReplyTo));
            }

            if (request.Cc?.Count > 0)
            {
                foreach (var cc in request.Cc)
                {
                    message.CC.Add(new MailAddress(cc));
                }
            }

            if (request.Bcc?.Count > 0)
            {
                foreach (var bcc in request.Bcc)
                {
                    message.Bcc.Add(new MailAddress(bcc));
                }
            }

            await client.SendMailAsync(message, ct);

            return new SendEmailResult
            {
                Success = true,
                MessageId = Guid.NewGuid().ToString(), // SMTP doesn't return message ID directly
                SentAt = DateTime.UtcNow,
            };
        }
        catch (SmtpException ex)
        {
            return new SendEmailResult
            {
                Success = false,
                Error = $"SMTP error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new SendEmailResult
            {
                Success = false,
                Error = $"Failed to send email: {ex.Message}"
            };
        }
    }

    public Task<SmtpSettings?> GetSmtpSettingsAsync(Guid userId, Guid? orgId, CancellationToken ct = default)
    {
        var key = GetSettingsKey(userId, orgId);
        _settings.TryGetValue(key, out var settings);
        
        // Return a copy without the password for security
        if (settings != null)
        {
            return Task.FromResult<SmtpSettings?>(new SmtpSettings
            {
                Id = settings.Id,
                Host = settings.Host,
                Port = settings.Port,
                Username = settings.Username,
                Password = "********", // Masked
                UseSsl = settings.UseSsl,
                FromEmail = settings.FromEmail,
                FromName = settings.FromName,
                LastTestedAt = settings.LastTestedAt,
                IsValid = settings.IsValid,
            });
        }
        
        return Task.FromResult<SmtpSettings?>(null);
    }

    public Task SaveSmtpSettingsAsync(SmtpSettings settings, Guid userId, Guid? orgId, CancellationToken ct = default)
    {
        var key = GetSettingsKey(userId, orgId);
        settings.Id = key;
        _settings[key] = settings;
        return Task.CompletedTask;
    }

    public async Task<bool> TestSmtpConnectionAsync(SmtpSettings settings, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                Credentials = new NetworkCredential(settings.Username, settings.Password),
                Timeout = 10000, // 10 seconds for test
            };

            // Create a test message but don't actually send it
            // Just verify we can connect to the SMTP server
            var fromAddress = new MailAddress(
                settings.FromEmail ?? settings.Username,
                settings.FromName ?? "Test"
            );

            // Try to establish connection by sending to self
            using var message = new MailMessage(fromAddress, fromAddress)
            {
                Subject = "SMTP Connection Test",
                Body = "This is a test message to verify SMTP settings.",
            };

            // Some SMTP servers don't allow sending to self, so we catch that
            try
            {
                await client.SendMailAsync(message, ct);
                return true;
            }
            catch (SmtpException ex) when (ex.StatusCode == SmtpStatusCode.MailboxUnavailable ||
                                            ex.StatusCode == SmtpStatusCode.MailboxNameNotAllowed)
            {
                // Connection worked, but mailbox issue - this is OK for testing
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
