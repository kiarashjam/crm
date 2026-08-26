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
        => await SendAsync(toEmail, recipientName, subject, body, null, ct);

    /// <summary>
    /// Builds and delivers one message, with an optional HTML alternative.
    /// Returns false (never throws) so a delivery problem cannot fail the
    /// caller's operation.
    /// </summary>
    private async Task<bool> SendAsync(string toEmail, string recipientName, string subject, string body, string? htmlBody, CancellationToken ct)
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
            message.Body = new BodyBuilder { TextBody = body, HtmlBody = htmlBody }.ToMessageBody();

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

    public async Task<bool> SendContractForSignatureEmailAsync(
        string toEmail, string recipientName, string organizationName,
        string contractTitle, string signUrl, CancellationToken ct = default)
    {
        if (!SmtpConfigured("contract signature request", toEmail, signUrl)) return false;

        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName;
        var subject = $"{organizationName}: please review and sign “{contractTitle}”";
        var text =
            $"Hi{greeting},\n\n" +
            $"{organizationName} has sent you a contract to review and sign:\n\n" +
            $"    {contractTitle}\n\n" +
            "You can read it in full and sign it here:\n\n" +
            $"    {signUrl}\n\n" +
            "Nothing is agreed until you sign. If anything looks wrong, reply to this " +
            "email instead of signing and we will sort it out.\n\n" +
            $"— {organizationName}\n";

        var html = HtmlDocument(
            heading: $"Please review and sign",
            intro: $"{Escape(organizationName)} has sent you a contract to review and sign.",
            bodyHtml:
                $"<p style=\"{PStyle}\"><strong>{Escape(contractTitle)}</strong></p>" +
                Button(signUrl, "Read and sign the contract") +
                $"<p style=\"{MutedStyle}\">Nothing is agreed until you sign. If anything looks wrong, " +
                "reply to this email instead of signing and we will sort it out.</p>",
            footer: Escape(organizationName));

        return await SendAsync(toEmail, recipientName, subject, text, html, ct);
    }

    public async Task<bool> SendContractSignedNotificationAsync(
        string toEmail, string recipientName, string counterpartyName,
        string contractTitle, string contractUrl, CancellationToken ct = default)
    {
        if (!SmtpConfigured("contract signed notification", toEmail, contractUrl)) return false;

        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName;
        var subject = $"{counterpartyName} signed “{contractTitle}” — your signature is next";
        var text =
            $"Hi{greeting},\n\n" +
            $"{counterpartyName} has signed “{contractTitle}”.\n\n" +
            "Add your countersignature to execute it and send the finished copy to " +
            "everyone:\n\n" +
            $"    {contractUrl}\n";

        var html = HtmlDocument(
            heading: "They have signed",
            intro: $"{Escape(counterpartyName)} has signed <strong>{Escape(contractTitle)}</strong>.",
            bodyHtml:
                $"<p style=\"{PStyle}\">Add your countersignature to execute it and send the " +
                "finished copy to everyone.</p>" +
                Button(contractUrl, "Countersign the contract"),
            footer: null);

        return await SendAsync(toEmail, recipientName, subject, text, html, ct);
    }

    public async Task<bool> SendExecutedContractEmailAsync(
        string toEmail, string recipientName, string organizationName,
        string contractTitle, string contractBody, string signatureBlock,
        CancellationToken ct = default)
    {
        if (!SmtpConfigured("executed contract copy", toEmail, null)) return false;

        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName;
        var subject = $"Signed by both parties: “{contractTitle}”";
        var text =
            $"Hi{greeting},\n\n" +
            $"“{contractTitle}” has now been signed by both parties. The full text " +
            "and the signature record are below — keep this email as your copy.\n\n" +
            new string('-', 60) + "\n\n" +
            contractBody + "\n\n" +
            new string('-', 60) + "\n\n" +
            signatureBlock + "\n\n" +
            $"— {organizationName}\n";

        var html = HtmlDocument(
            heading: "Signed by both parties",
            intro: $"<strong>{Escape(contractTitle)}</strong> has now been signed by both parties. " +
                   "The full text and the signature record are below — keep this email as your copy.",
            bodyHtml:
                // The contract itself, preformatted so its line breaks and headings
                // survive. Any mail client can render this; an attachment could not
                // be produced without a PDF library and somewhere to store it.
                $"<pre style=\"{PreStyle}\">{Escape(contractBody)}</pre>" +
                $"<div style=\"{SignatureBoxStyle}\"><pre style=\"{PreStyle}margin:0;background:none;border:0;padding:0;\">" +
                $"{Escape(signatureBlock)}</pre></div>",
            footer: Escape(organizationName));

        return await SendAsync(toEmail, recipientName, subject, text, html, ct);
    }

    // ── HTML shell ───────────────────────────────────────────────────────────
    //
    // Inline styles and a table-free single column: every mail client strips
    // <style> blocks, and anything cleverer degrades differently in each one.

    private const string PStyle = "margin:0 0 14px;font-size:15px;line-height:1.55;color:#0f172a;";
    private const string MutedStyle = "margin:18px 0 0;font-size:13px;line-height:1.5;color:#64748b;";
    private const string PreStyle =
        "white-space:pre-wrap;word-wrap:break-word;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;" +
        "font-size:13px;line-height:1.6;color:#0f172a;background:#f8fafc;border:1px solid #e2e8f0;" +
        "border-radius:10px;padding:16px;margin:0 0 16px;";
    private const string SignatureBoxStyle =
        "border:1px solid #a7f3d0;background:#ecfdf5;border-radius:10px;padding:14px 16px;margin:0 0 8px;";

    private static string Escape(string? s) => string.IsNullOrEmpty(s)
        ? string.Empty
        : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string Button(string url, string label) =>
        $"<p style=\"margin:22px 0;\"><a href=\"{Escape(url)}\" " +
        "style=\"display:inline-block;background:#ea580c;color:#ffffff;text-decoration:none;" +
        "font-weight:600;font-size:15px;padding:12px 22px;border-radius:10px;\">" +
        $"{Escape(label)}</a></p>" +
        $"<p style=\"{MutedStyle}\">If the button does not work, paste this into your browser:<br>" +
        $"<span style=\"color:#0f172a;word-break:break-all;\">{Escape(url)}</span></p>";

    private string HtmlDocument(string heading, string intro, string bodyHtml, string? footer) =>
        "<!doctype html><html><body style=\"margin:0;padding:24px;background:#f1f5f9;" +
        "font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;\">" +
        "<div style=\"max-width:640px;margin:0 auto;background:#ffffff;border-radius:16px;" +
        "border:1px solid #e2e8f0;padding:28px 30px;\">" +
        $"<h1 style=\"margin:0 0 6px;font-size:20px;line-height:1.3;color:#0f172a;\">{Escape(heading)}</h1>" +
        $"<p style=\"{PStyle}\">{intro}</p>" +
        bodyHtml +
        (string.IsNullOrWhiteSpace(footer)
            ? string.Empty
            : $"<p style=\"{MutedStyle}\">— {footer}</p>") +
        $"<p style=\"{MutedStyle}border-top:1px solid #e2e8f0;padding-top:14px;\">" +
        $"Sent by {Escape(_options.FromName)}.</p>" +
        "</div></body></html>";
}
