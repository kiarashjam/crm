using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>How a reply was taken in.</summary>
public enum InboundReplyOutcome
{
    /// <summary>Matched to a message, written on the record's timeline, and the person's sequence stopped.</summary>
    Recorded,
    /// <summary>The same reply had been taken in already.</summary>
    AlreadyRecorded,
    /// <summary>It answers nothing the workspaces sent, so it is not kept.</summary>
    NotMatched,
}

public interface IInboundEmailService
{
    /// <summary>A reply the mail service received: matched, recorded, and its sequence stopped.</summary>
    Task<InboundReplyOutcome> ReceiveReplyAsync(InboundReply reply, CancellationToken ct = default);

    /// <summary>The mail service's delivery reports. Returns how many matched a message.</summary>
    Task<int> ReceiveEventsAsync(IReadOnlyList<MailEvent> events, CancellationToken ct = default);
}

/// <summary>
/// What comes back after the outbox sends: replies, bounces and spam complaints.
/// </summary>
/// <remarks>
/// <para>
/// Nothing came back before. A prospect's reply reached the rep's own inbox, if the
/// Reply-To was right, and the CRM never knew: the sequence carried on until someone
/// logged an activity by hand. A bounced address went on bouncing, and a spam
/// complaint went unheard while the workspace kept writing.
/// </para>
/// <para>
/// A reply is matched to the message it answers by the reply address of that message
/// (reply+token@…), or by the Message-ID it names in In-Reply-To or References; both
/// are random enough that nobody can attach mail to someone else's record. Mail that
/// matches nothing is dropped, not kept: it belongs to no workspace.
/// </para>
/// </remarks>
public sealed class InboundEmailService : IInboundEmailService
{
    /// <summary>InboundEmails.MessageIdHeader is nvarchar(255).</summary>
    private const int MaxMessageIdLength = 255;

    private readonly IOutboxRepository _messages;
    private readonly ISequenceRepository _sequences;
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityService _activities;
    private readonly INotificationService _notifications;
    private readonly EmailSettings _settings;
    private readonly ILogger<InboundEmailService> _logger;

    public InboundEmailService(
        IOutboxRepository messages,
        ISequenceRepository sequences,
        IOrganizationRepository organizations,
        IActivityService activities,
        INotificationService notifications,
        IOptions<EmailSettings> settings,
        ILogger<InboundEmailService> logger)
    {
        _messages = messages;
        _sequences = sequences;
        _organizations = organizations;
        _activities = activities;
        _notifications = notifications;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Replies ───────────────────────────────────────────────────────────────

    public async Task<InboundReplyOutcome> ReceiveReplyAsync(InboundReply reply, CancellationToken ct = default)
    {
        var (message, matchedBy) = await MatchAsync(reply, ct);
        if (message?.OrganizationId is not Guid organizationId || message.Channel != OutboundChannels.Email)
        {
            _logger.LogInformation("An inbound email matched nothing that was sent, so it was dropped");
            return InboundReplyOutcome.NotMatched;
        }
        // A Message-ID longer than the column holds is not kept: saving it would fail, and
        // the failure would read as "taken in already", dropping the reply.
        var messageId = NormalizeMessageId(reply.MessageId) is { Length: <= MaxMessageIdLength } id ? id : null;
        if (messageId != null && await _messages.InboundSeenAsync(organizationId, messageId, ct))
            return InboundReplyOutcome.AlreadyRecorded;

        var now = DateTime.UtcNow;
        var inbound = new InboundEmail
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OutboundMessageId = message.Id,
            MessageIdHeader = messageId,
            FromAddress = Clip(reply.FromAddress.Trim(), 256)!,
            Subject = Clip(OutboxService.OneLine(reply.Subject), 300),
            MatchedBy = matchedBy!,
            ReceivedAtUtc = now,
        };
        message.RepliedAtUtc ??= now;

        // Recorded first, so the same reply posted twice at once is written on the timeline once.
        if (!await _messages.TryAddInboundAsync(inbound, ct)) return InboundReplyOutcome.AlreadyRecorded;
        inbound.ActivityId = await WriteTimelineAsync(message, reply, ct);
        await _messages.SaveAsync(ct);

        // They answered: the scheduled emails stop, as when someone logs their reply by hand.
        await StopOutreachAsync(message, organizationId, ct);
        await TellOwnerAsync(message, organizationId, reply, inbound.Id, ct);
        _logger.LogInformation("A reply to outbox email {MessageId} was recorded (matched by {MatchedBy})", message.Id, matchedBy);
        return InboundReplyOutcome.Recorded;
    }

    /// <summary>The message a reply answers, and how it was found.</summary>
    private async Task<(OutboundMessage? Message, string? MatchedBy)> MatchAsync(InboundReply reply, CancellationToken ct)
    {
        // The reply address first: it survives a mail app that drops In-Reply-To.
        var domain = _settings.ReplyDomain();
        if (domain != null)
        {
            foreach (var recipient in reply.Recipients)
            {
                var address = recipient.Trim().ToLowerInvariant();
                var at = address.LastIndexOf('@');
                if (at <= 0 || address[(at + 1)..] != domain) continue;
                var local = address[..at];
                if (!local.StartsWith("reply+", StringComparison.Ordinal)) continue;
                if (await _messages.FindByReplyTokenAsync(local["reply+".Length..], ct) is { } byToken) return (byToken, "token");
            }
        }
        if (NormalizeMessageId(reply.InReplyTo) is { } inReplyTo && await _messages.FindByMessageIdAsync(inReplyTo, ct) is { } answered)
            return (answered, "in-reply-to");
        // The thread's newest message first.
        foreach (var reference in reply.References.Reverse())
        {
            if (NormalizeMessageId(reference) is { } id && await _messages.FindByMessageIdAsync(id, ct) is { } referenced)
                return (referenced, "references");
        }
        return (null, null);
    }

    private async Task<Guid?> WriteTimelineAsync(OutboundMessage message, InboundReply reply, CancellationToken ct)
    {
        if (message.LeadId == null && message.ContactId == null && message.DealId == null) return null;
        try
        {
            var author = message.CreatedByUserId ?? (await _organizations.GetByIdAsync(message.OrganizationId!.Value, ct))?.OwnerUserId;
            if (author is not Guid userId) return null;
            var who = string.IsNullOrWhiteSpace(reply.FromName) ? reply.FromAddress.Trim() : reply.FromName.Trim();
            var subject = OutboxService.OneLine(reply.Subject);
            // Automated: it is the prospect writing, not somebody here answering them, so it
            // is no first response. The sequence is stopped below instead.
            var created = await _activities.CreateAsync(userId, message.OrganizationId, new CreateActivityRequest
            {
                Type = "email",
                Subject = Clip($"Reply from {who}: {(subject.Length > 0 ? subject : message.Subject)}", 200),
                Body = InboundMail.WithoutQuotedHistory(reply.Text),
                LeadId = message.LeadId,
                ContactId = message.ContactId,
                DealId = message.DealId,
                Participants = Clip(reply.FromAddress.Trim(), 500),
                IsAutomated = true,
            }, ct);
            if (created.IsSuccess) return created.Value.Id;
            _logger.LogWarning("A reply to outbox email {MessageId} could not go on the timeline: {Error}", message.Id, created.Error.Description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "A reply to outbox email {MessageId} could not go on the timeline", message.Id);
        }
        return null;
    }

    private async Task StopOutreachAsync(OutboundMessage message, Guid organizationId, CancellationToken ct)
    {
        try
        {
            if (message.LeadId is Guid leadId) await _sequences.StopOpenForTargetAsync(organizationId, "lead", leadId, ct);
            if (message.ContactId is Guid contactId) await _sequences.StopOpenForTargetAsync(organizationId, "contact", contactId, ct);
            if (message.Source == OutboundSources.Sequence && message.SourceId is Guid enrollmentId
                && await _sequences.GetEnrollmentAsync(enrollmentId, organizationId, ct) is { Status: "active" or "paused" } enrollment)
            {
                enrollment.Status = "stopped";
                enrollment.NextActionAtUtc = null;
                await _sequences.SaveEnrollmentAsync(enrollment, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "A reply to outbox email {MessageId} was recorded, but its sequence could not be stopped", message.Id);
        }
    }

    /// <summary>
    /// The person behind the record hears that a reply came: whoever works the lead, else
    /// whoever sent the email, else the workspace's owner. The reply itself also reached
    /// the person in Reply-To.
    /// </summary>
    private async Task TellOwnerAsync(OutboundMessage message, Guid organizationId, InboundReply reply, Guid inboundId, CancellationToken ct)
    {
        try
        {
            Guid? recipient = null;
            if (message.LeadId is Guid leadId)
                recipient = (await _sequences.TargetStateAsync("lead", leadId, organizationId, ct)).OwnerUserId;
            if (recipient == null && message.CreatedByUserId is Guid creator && await _organizations.GetMemberRoleAsync(creator, organizationId, ct) != null)
                recipient = creator;
            recipient ??= (await _organizations.GetByIdAsync(organizationId, ct))?.OwnerUserId;
            if (recipient is not Guid userId) return;
            var who = string.IsNullOrWhiteSpace(reply.FromName) ? reply.FromAddress.Trim() : reply.FromName.Trim();
            var link = message.LeadId is Guid lead ? $"/leads/{lead}"
                : message.ContactId is Guid contact ? $"/contacts/{contact}"
                : message.DealId is Guid deal ? $"/deals/{deal}" : null;
            await _notifications.CreateAsync(userId, organizationId, new CreateNotificationRequest(
                "system", Clip($"{who} replied", 200)!, Clip(InboundMail.WithoutQuotedHistory(reply.Text, 300), 300), link,
                SourceKey: $"reply:{inboundId}"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nobody could be told about a reply to outbox email {MessageId}", message.Id);
        }
    }

    // ── Delivery reports ──────────────────────────────────────────────────────

    public async Task<int> ReceiveEventsAsync(IReadOnlyList<MailEvent> events, CancellationToken ct = default)
    {
        var matched = 0;
        foreach (var report in events)
        {
            try
            {
                if (await ApplyAsync(report, ct)) matched++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One report that fails must not lose the others in the same post.
                _logger.LogWarning(ex, "A delivery report ({Event}) could not be applied", report.Event);
            }
        }
        return matched;
    }

    private async Task<bool> ApplyAsync(MailEvent report, CancellationToken ct)
    {
        var message = report.OutboundMessageId is Guid id ? await _messages.GetAsync(id, ct) : null;
        if (message == null && !string.IsNullOrWhiteSpace(report.SmtpId))
            message = await _messages.FindByMessageIdAsync(report.SmtpId, ct);
        // A report about mail we cannot place belongs to no workspace: the relay is shared,
        // and suppressing the address for everyone would act on another workspace's mail.
        if (message?.OrganizationId is not Guid organizationId || message.Channel != OutboundChannels.Email) return false;

        var at = report.OccurredAtUtc ?? DateTime.UtcNow;
        var detail = Clip(report.Reason, 500);
        string? suppressAs = null;
        switch (report.Event)
        {
            case "delivered":
                if (message.DeliveryStatus is null or "deferred") Deliver(message, "delivered", null, at);
                break;
            case "deferred":
                if (message.DeliveryStatus is null) Deliver(message, "deferred", detail, at);
                break;
            case "open":
                message.OpenedAtUtc ??= at;
                break;
            case "bounce" when string.Equals(report.Type, "blocked", StringComparison.OrdinalIgnoreCase):
                // Refused for now (the receiving server's policy, a reputation block): the
                // address itself may be fine, so it is not suppressed.
                Deliver(message, "blocked", detail, at);
                break;
            case "bounce":
                Deliver(message, "bounced", detail, at);
                suppressAs = EmailSuppressionReasons.Bounced;
                break;
            case "dropped":
                Deliver(message, "dropped", detail, at);
                suppressAs = DroppedAs(report.Reason);
                break;
            case "spamreport":
                Deliver(message, "spam_report", null, at);
                suppressAs = EmailSuppressionReasons.SpamReport;
                break;
            case "unsubscribe":
            case "group_unsubscribe":
                suppressAs = EmailSuppressionReasons.Unsubscribed;
                break;
            default:
                return true;
        }
        await _messages.SaveAsync(ct);

        if (suppressAs != null && !string.IsNullOrWhiteSpace(report.Email))
        {
            await _messages.SuppressAsync(new EmailSuppression
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Email = report.Email,
                Reason = suppressAs,
                Detail = detail,
                OutboundMessageId = message.Id,
                CreatedAtUtc = at,
            }, ct);
            // The sequences writing to that address stop now, not at their next step.
            var stopped = await _sequences.StopOpenForAddressAsync(organizationId, report.Email, ct);
            _logger.LogInformation("The mail service reported {Event} for an address of organization {OrganizationId}; {Stopped} enrollment(s) stopped",
                report.Event, organizationId, stopped);
        }
        return true;
    }

    /// <summary>What a drop means for the address, from SendGrid's reason.</summary>
    private static string? DroppedAs(string? reason)
    {
        var why = (reason ?? "").Trim();
        if (why.Contains("Bounced Address", StringComparison.OrdinalIgnoreCase)) return EmailSuppressionReasons.Bounced;
        if (why.Contains("Spam Reporting Address", StringComparison.OrdinalIgnoreCase)) return EmailSuppressionReasons.SpamReport;
        if (why.Contains("Unsubscribed Address", StringComparison.OrdinalIgnoreCase)) return EmailSuppressionReasons.Unsubscribed;
        if (why.Equals("Invalid", StringComparison.OrdinalIgnoreCase)) return EmailSuppressionReasons.Invalid;
        // Spam content, a bad header, a quota: about this message, not the address.
        return null;
    }

    private static void Deliver(OutboundMessage message, string status, string? detail, DateTime at)
    {
        message.DeliveryStatus = status;
        message.DeliveryDetail = detail;
        message.DeliveryUpdatedAtUtc = at;
    }

    /// <summary>"&lt;id@host&gt;" from any of "id@host", "&lt;id@host&gt;" or " &lt;id@host&gt; ", or null.</summary>
    internal static string? NormalizeMessageId(string? value)
    {
        var bare = (value ?? "").Trim().TrimStart('<').TrimEnd('>').Trim();
        return bare.Length == 0 ? null : $"<{bare}>";
    }

    private static string? Clip(string? text, int max) =>
        text == null ? null : text.Length <= max ? text : text[..max];
}
