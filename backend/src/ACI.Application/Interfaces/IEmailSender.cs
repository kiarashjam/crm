namespace ACI.Application.Interfaces;

public interface IEmailSender
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string recipientName, string resetUrl, CancellationToken ct = default);
}
