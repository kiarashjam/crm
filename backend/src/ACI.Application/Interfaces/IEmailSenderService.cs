using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IEmailSenderService
{
    Task<SendEmailResult> SendEmailAsync(SendEmailRequest request, Guid userId, Guid? orgId, CancellationToken ct = default);
    Task<SmtpSettings?> GetSmtpSettingsAsync(Guid userId, Guid? orgId, CancellationToken ct = default);
    Task SaveSmtpSettingsAsync(SmtpSettings settings, Guid userId, Guid? orgId, CancellationToken ct = default);
    Task<bool> TestSmtpConnectionAsync(SmtpSettings settings, CancellationToken ct = default);
}
