using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public interface IComposedEmailService
{
    Task<Result<SentEmailDto>> SendAsync(Guid organizationId, SendEmailRequest request, CancellationToken ct = default);
}

/// <summary>
/// Sends what someone wrote in the email composer, from the workspace's sender
/// address (Email:FromAddress — info@pavillon46.ch in production).
/// </summary>
/// <remarks>
/// The composer used to post to an endpoint that did not exist, and on the 404 the
/// app saved the message in the browser and said "Email sent". This is that
/// endpoint. It answers "sent" only when the mail server took the message, and
/// otherwise says which of two things went wrong: no mail server is set up, or the
/// one that is did not accept it.
/// </remarks>
public sealed class ComposedEmailService : IComposedEmailService
{
    /// <summary>A person writing to people, not a mailing: To, Cc and Bcc together.</summary>
    public const int MaxRecipients = 20;
    public const int MaxSubjectLength = 300;
    public const int MaxBodyLength = 50_000;

    private readonly IEmailSender _sender;
    private readonly IOrganizationRepository _organizations;
    private readonly ILogger<ComposedEmailService> _logger;

    public ComposedEmailService(IEmailSender sender, IOrganizationRepository organizations, ILogger<ComposedEmailService> logger)
    {
        _sender = sender;
        _organizations = organizations;
        _logger = logger;
    }

    public async Task<Result<SentEmailDto>> SendAsync(Guid organizationId, SendEmailRequest request, CancellationToken ct = default)
    {
        var to = Addresses(request.To);
        var cc = Addresses(request.Cc);
        var bcc = Addresses(request.Bcc);

        if (to.Count == 0)
            return new Error("Email.NoRecipient", "Add at least one recipient.");
        var wrong = to.Concat(cc).Concat(bcc).FirstOrDefault(address => !ValidationHelper.IsValidEmail(address));
        if (wrong != null)
            return new Error("Email.InvalidAddress", $"\"{wrong}\" is not a valid email address.");
        if (to.Count + cc.Count + bcc.Count > MaxRecipients)
            return new Error("Email.TooManyRecipients", $"An email from the CRM can go to at most {MaxRecipients} people.");

        // A line break in a subject would start a new header line; MimeKit encodes
        // it safely, but a subject is one line, so keep it one.
        var subject = string.Join(' ', (request.Subject ?? "").Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
        if (subject.Length == 0)
            return new Error("Email.NoSubject", "Add a subject.");
        if (subject.Length > MaxSubjectLength)
            return new Error("Email.SubjectTooLong", $"The subject can be at most {MaxSubjectLength} characters.");

        var body = request.Body ?? "";
        if (body.Length > MaxBodyLength)
            return new Error("Email.BodyTooLong", $"The message can be at most {MaxBodyLength:N0} characters.");

        // Sent under the workspace's name: the configured Email:FromName is the
        // product's ("Cadence"), which means nothing to the person reading it.
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        var outcome = await _sender.SendComposedAsync(
            new ComposedEmail(to, cc, bcc, subject, body, PlainTextEmail.ToHtml(body), workspace?.Name), ct);

        switch (outcome)
        {
            case ComposedEmailOutcome.NotConfigured:
                return new Error("Email.NotConfigured", "Sending email is not set up on the server yet.");
            case ComposedEmailOutcome.Failed:
                return new Error("Email.SendFailed", "The email could not be sent. Please try again in a moment.");
        }

        _logger.LogInformation("Sent a composed email to {Count} recipient(s)", to.Count + cc.Count + bcc.Count);
        return new SentEmailDto(
            Guid.NewGuid(),
            string.Join(", ", to),
            cc.Count == 0 ? null : string.Join(", ", cc),
            bcc.Count == 0 ? null : string.Join(", ", bcc),
            subject,
            body,
            "sent",
            "outbound",
            DateTime.UtcNow);
    }

    /// <summary>"a@x.ch, b@y.ch; c@z.ch" → three addresses, each once.</summary>
    internal static List<string> Addresses(string? list) =>
        (list ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
