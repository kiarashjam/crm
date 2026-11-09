using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public interface IComposedEmailService
{
    Task<Result<SentEmailDto>> SendAsync(Guid organizationId, SendEmailRequest request, Guid sentByUserId = default, CancellationToken ct = default);
}

/// <summary>
/// Sends what someone wrote in the email composer, or what the sales assistant drafted
/// and they confirmed, through the outbox.
/// </summary>
/// <remarks>
/// <para>
/// The composer used to post to an endpoint that did not exist, and on the 404 the
/// app saved the message in the browser and said "Email sent". This answers "sent"
/// only when the mail server took the message. When it did not but may yet, the
/// message is queued and retried, and the answer says so ("queued"), so nobody sends
/// it a second way. When no mail server is set up, or the server refused it for good,
/// it says which, and the message will never go.
/// </para>
/// <para>
/// The outbox writes the email on the record's timeline once it has gone. The browser
/// used to write that entry itself, and the assistant wrote none at all.
/// </para>
/// </remarks>
public sealed class ComposedEmailService : IComposedEmailService
{
    /// <summary>A person writing to people, not a mailing: To, Cc and Bcc together.</summary>
    public const int MaxRecipients = 20;
    public const int MaxSubjectLength = 300;
    public const int MaxBodyLength = 50_000;

    private readonly IOutbox _outbox;
    private readonly IWorkspaceRecords _records;
    private readonly ILogger<ComposedEmailService> _logger;

    public ComposedEmailService(IOutbox outbox, IWorkspaceRecords records, ILogger<ComposedEmailService> logger)
    {
        _outbox = outbox;
        _records = records;
        _logger = logger;
    }

    public async Task<Result<SentEmailDto>> SendAsync(Guid organizationId, SendEmailRequest request, Guid sentByUserId = default, CancellationToken ct = default)
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
        var subject = OutboxService.OneLine(request.Subject);
        if (subject.Length == 0)
            return new Error("Email.NoSubject", "Add a subject.");
        if (subject.Length > MaxSubjectLength)
            return new Error("Email.SubjectTooLong", $"The subject can be at most {MaxSubjectLength} characters.");

        var body = request.Body ?? "";
        if (body.Length > MaxBodyLength)
            return new Error("Email.BodyTooLong", $"The message can be at most {MaxBodyLength:N0} characters.");

        // The record it is about fills the placeholders and gets the timeline entry, so it
        // has to be one of this workspace's that the sender can open (a private deal too).
        var links = await _records.CheckLinksAsync(sentByUserId, organizationId,
            new RecordLinks { LeadId = request.LeadId, ContactId = request.ContactId, DealId = request.DealId }, ct);
        if (links.IsFailure) return links.Error;

        // Do not contact, unsubscribes and bounces are checked by the outbox, on the
        // server. The composer only hid its button on a contact's page, so the lead page,
        // a typed address or the sales assistant could still email someone who had
        // asked to be left alone.
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = string.Equals(request.Source?.Trim(), OutboundSources.Assistant, StringComparison.OrdinalIgnoreCase)
                ? OutboundSources.Assistant
                : OutboundSources.Composer,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Subject = subject,
            Body = body,
            CreatedByUserId = sentByUserId == default ? null : sentByUserId,
            // A reply goes to whoever wrote it, not to the shared sender address.
            ReplyToUserId = sentByUserId == default ? null : sentByUserId,
            LeadId = request.LeadId,
            ContactId = request.ContactId,
            DealId = request.DealId,
            CountsAsResponse = true,
            PersonWaiting = true,
        }, ct);
        if (queued.IsFailure) return queued.Error;
        var message = queued.Value;

        // Not the request's token: once the mail server has the message, a browser that
        // hangs up must not cut the attempt off halfway and leave it unknown whether it went.
        var attempt = await _outbox.SendNowAsync(message, CancellationToken.None);
        switch (attempt.Result)
        {
            case OutboxAttemptResult.NotConfigured:
                return new Error("Email.NotConfigured", "Sending email is not set up on the server yet.");
            case OutboxAttemptResult.Suppressed:
                return new Error(attempt.Code ?? "Email.Suppressed", attempt.Error ?? "Someone it was for asked not to be emailed, so nothing was sent.");
            case OutboxAttemptResult.Failed:
                return new Error("Email.SendFailed", $"The email was not sent. {attempt.Error ?? "The mail server refused it."}");
        }

        var status = attempt.Result == OutboxAttemptResult.Sent ? "sent" : "queued";
        _logger.LogInformation("A composed email to {Count} recipient(s) is {Status}", to.Count + cc.Count + bcc.Count, status);
        return new SentEmailDto(
            message.Id,
            message.To ?? "",
            message.Cc,
            message.Bcc,
            message.Subject ?? subject,
            message.Body ?? body,
            status,
            "outbound",
            message.SentAtUtc ?? message.CreatedAtUtc,
            request.LeadId,
            request.ContactId,
            request.DealId);
    }

    /// <summary>"a@x.ch, b@y.ch; c@z.ch" → three addresses, each once.</summary>
    internal static List<string> Addresses(string? list) => OutboxService.Addresses(list == null ? null : [list]);
}
