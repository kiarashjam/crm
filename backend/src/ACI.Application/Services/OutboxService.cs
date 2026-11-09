using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>
/// An email for the outbox: who it is for, the words with their placeholders, and who
/// is behind it. The outbox fills the placeholders, checks the suppression list and
/// decides who it is from and where replies go.
/// </summary>
public sealed record OutboundEmailRequest
{
    public required Guid OrganizationId { get; init; }

    /// <summary><see cref="OutboundSources"/>.</summary>
    public required string Source { get; init; }

    /// <summary>The enrollment, rule or campaign behind it.</summary>
    public Guid? SourceId { get; init; }

    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
    public IReadOnlyList<string> Bcc { get; init; } = [];

    /// <summary>The recipient's name, when the message has no lead or contact to take it from.</summary>
    public string? RecipientName { get; init; }

    /// <summary>The subject as written; placeholders are filled.</summary>
    public required string Subject { get; init; }

    /// <summary>The words as written, plain text; placeholders are filled and the footer added.</summary>
    public required string Body { get; init; }

    /// <summary>Who queued it: the timeline entry is theirs, and they hear about a message that fails.</summary>
    public Guid? CreatedByUserId { get; init; }

    /// <summary>Whose address replies go to, while they are in the workspace; else its owner's.</summary>
    public Guid? ReplyToUserId { get; init; }

    public Guid? LeadId { get; init; }
    public Guid? ContactId { get; init; }
    public Guid? DealId { get; init; }
    public Guid? MemberId { get; init; }

    /// <summary>A person wrote it, so its timeline entry is their response (see <see cref="OutboundMessage.CountsAsResponse"/>).</summary>
    public bool CountsAsResponse { get; init; }

    /// <summary>Put the lead's open-tracking image in the HTML part, where it can work.</summary>
    public bool TrackOpens { get; init; }

    /// <summary>
    /// A person is waiting for the answer: the first attempt is made at once with
    /// <see cref="IOutbox.SendNowAsync"/>, which holds the message meanwhile, and a
    /// placeholder that cannot be filled is refused rather than left out.
    /// </summary>
    public bool PersonWaiting { get; init; }
}

/// <summary>How an attempt at a message ended.</summary>
public enum OutboxAttemptResult
{
    /// <summary>It went.</summary>
    Sent,
    /// <summary>It did not go this time and is queued for another attempt.</summary>
    Queued,
    /// <summary>No mail server is set up. Nothing was attempted, and it will not be sent later.</summary>
    NotConfigured,
    /// <summary>It was refused for good, or still failing after the last attempt. It will not be sent.</summary>
    Failed,
    /// <summary>Someone it was for had asked not to be emailed, or their address had bounced. It was not sent.</summary>
    Suppressed,
}

/// <param name="Error">Why it did not go, fit to show the person who sent it.</param>
/// <param name="Code">For a suppressed message, the error code of the refusal (Contact.DoNotContact, Email.Suppressed).</param>
public sealed record OutboxAttempt(OutboxAttemptResult Result, string? Error = null, string? Code = null);

public interface IOutbox
{
    /// <summary>
    /// Queues an email: fills its placeholders, refuses anyone on the suppression list
    /// (do not contact, unsubscribed, bounced, complained), and settles who it is from,
    /// where replies go and its Message-ID. Nothing is sent here.
    /// </summary>
    Task<Result<OutboundMessage>> QueueEmailAsync(OutboundEmailRequest request, CancellationToken ct = default);

    /// <summary>
    /// The first attempt at a message queued with <see cref="OutboundEmailRequest.PersonWaiting"/>,
    /// made while the person waits. When it does not go but may later, it stays queued.
    /// </summary>
    Task<OutboxAttempt> SendNowAsync(OutboundMessage message, CancellationToken ct = default);

    /// <summary>One due message, claimed and attempted: what the dispatcher runs for each.</summary>
    Task ProcessAsync(DueOutboundMessage due, CancellationToken ct = default);

    /// <summary>Takes back an email still waiting to be sent: by whoever queued it, or a manager.</summary>
    Task<Result> CancelAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>The workspace's latest webhook deliveries, for its owner and managers.</summary>
    Task<Result<IReadOnlyList<WebhookDeliveryDto>>> WebhookDeliveriesAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>Whether something queued for this sender record (a sequence enrollment) has still not gone.</summary>
    Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default);
}

/// <summary>
/// One outbox for everything a workspace sends: composer and assistant email, sequence
/// steps, automation email and the outbound webhook.
/// </summary>
/// <remarks>
/// <para>
/// Each of those used to send in its own way. Automation email and webhooks went out
/// inside the request that saved a lead, so a slow mail server or receiver held that
/// save up, and a failure was logged and lost. Sequences retried, the others did not.
/// Do not contact was honoured in some places, merge fields filled in others, and
/// only some sends reached the record's timeline.
/// </para>
/// <para>
/// Now every message is a row in OutboundMessages. It is checked against the
/// suppression list when it is queued and again when it goes, sent by a background
/// dispatcher that retries with growing gaps (a minute, five, fifteen, then hours, for
/// about two days), and written once on the record's timeline when it has gone. It
/// carries its own Message-ID and, when replies are captured, a reply address of its
/// own, so a reply or a bounce can be traced back to it.
/// </para>
/// <para>
/// Delivery is at least once: an instance that dies between handing a message to the
/// mail server and recording it leaves it to be sent again when its claim runs out.
/// </para>
/// </remarks>
public sealed class OutboxService : IOutbox, IOutboundWebhookPublisher
{
    /// <summary>How long one attempt holds a message before another instance may take it.</summary>
    internal static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(5);

    /// <summary>The wait after each failed attempt: about two days in all.</summary>
    internal static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1),
        TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12), TimeSpan.FromHours(24),
    };

    /// <summary>The first attempt and one after each retry delay.</summary>
    public static readonly int MaxAttempts = RetryDelays.Length + 1;

    private readonly IOutboxRepository _messages;
    private readonly IEmailSender _email;
    private readonly IWebhookTransport _webhooks;
    private readonly IOutreachMail _outreach;
    private readonly IContactRepository _contacts;
    private readonly ISequenceRepository _targets;
    private readonly IOrganizationRepository _organizations;
    private readonly IActivityService _activities;
    private readonly INotificationService _notifications;
    private readonly IOutboxSignal _signal;
    private readonly EmailSettings _settings;
    private readonly ILogger<OutboxService> _logger;

    public OutboxService(
        IOutboxRepository messages,
        IEmailSender email,
        IWebhookTransport webhooks,
        IOutreachMail outreach,
        IContactRepository contacts,
        ISequenceRepository targets,
        IOrganizationRepository organizations,
        IActivityService activities,
        INotificationService notifications,
        IOutboxSignal signal,
        IOptions<EmailSettings> settings,
        ILogger<OutboxService> logger)
    {
        _messages = messages;
        _email = email;
        _webhooks = webhooks;
        _outreach = outreach;
        _contacts = contacts;
        _targets = targets;
        _organizations = organizations;
        _activities = activities;
        _notifications = notifications;
        _signal = signal;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Queueing ──────────────────────────────────────────────────────────────

    public async Task<Result<OutboundMessage>> QueueEmailAsync(OutboundEmailRequest request, CancellationToken ct = default)
    {
        var to = Addresses(request.To);
        var cc = Addresses(request.Cc);
        var bcc = Addresses(request.Bcc);
        if (to.Count == 0) return new Error("Email.NoRecipient", "Add at least one recipient.");
        var wrong = to.Concat(cc).Concat(bcc).FirstOrDefault(a => !ValidationHelper.IsValidEmail(a));
        if (wrong != null) return new Error("Email.InvalidAddress", $"\"{wrong}\" is not a valid email address.");

        var organizationId = request.OrganizationId;
        if (await RefusalAsync(organizationId, to.Concat(cc).Concat(bcc).ToList(), request.LeadId, request.ContactId, ct) is { } refusal)
            return refusal;

        // Under the workspace's name, from its own address once that is verified, and a
        // reply reaches the person behind it rather than the shared sender inbox.
        var sender = await _outreach.SenderAsync(organizationId, request.ReplyToUserId, ct);
        var values = await MergeValuesAsync(request, to, sender, ct);
        var subject = OneLine(MergeFields.Fill(request.Subject, values, out var subjectUnfilled));
        var body = MergeFields.Fill(request.Body, values, out var bodyUnfilled);
        if (request.PersonWaiting)
        {
            var unfilled = subjectUnfilled.Concat(bodyUnfilled).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (unfilled.Count > 0) return UnfilledError(unfilled);
        }
        if (subject.Length == 0) return new Error("Email.NoSubject", "Add a subject.");
        if (subject.Length > 300) subject = subject[..300];

        // A link at the foot for each person it is visibly addressed to, to stop this
        // workspace's email. Not for Bcc: naming them would show everyone who else got it.
        var tracking = request.TrackOpens && request.LeadId is Guid leadId ? _outreach.TrackingUrl(leadId) : null;
        var mail = _outreach.Body(organizationId, sender.WorkspaceName, body, to.Concat(cc).ToList(), tracking);

        var id = Guid.NewGuid();
        var replyDomain = _settings.ReplyDomain();
        var token = replyDomain == null ? null : NewReplyToken();
        var replyTo = new List<string>();
        if (sender.ReplyTo != null) replyTo.Add(sender.ReplyTo);
        // With reply capture on, a second Reply-To brings the answer into the CRM as well,
        // while the first still puts it in the person's own inbox.
        if (token != null) replyTo.Add($"reply+{token}@{replyDomain}");

        var now = DateTime.UtcNow;
        var message = new OutboundMessage
        {
            Id = id,
            OrganizationId = organizationId,
            Channel = OutboundChannels.Email,
            Source = request.Source,
            SourceId = request.SourceId,
            Status = OutboundStatuses.Queued,
            // A person waiting gets the first attempt straight away, held so no
            // dispatcher takes it meanwhile; if this request dies, the hold runs out and
            // the dispatcher sends it.
            Attempts = request.PersonWaiting ? 1 : 0,
            ClaimedUntilUtc = request.PersonWaiting ? now.Add(ClaimFor) : null,
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = request.CreatedByUserId,
            FromName = sender.WorkspaceName,
            FromAddress = sender.FromAddress,
            ReplyTo = replyTo.Count == 0 ? null : string.Join(", ", replyTo),
            To = string.Join(", ", to),
            Cc = cc.Count == 0 ? null : string.Join(", ", cc),
            Bcc = bcc.Count == 0 ? null : string.Join(", ", bcc),
            RecipientName = to.Count == 1 ? Clip(values[MergeFields.Name], 200) : null,
            Subject = subject,
            Body = body,
            TextBody = mail.Text,
            HtmlBody = mail.Html,
            UnsubscribeUrl = mail.UnsubscribeUrl,
            MessageIdHeader = $"<{id:N}@{MessageIdDomain(sender.FromAddress)}>",
            ReplyToken = token,
            CountsAsResponse = request.CountsAsResponse,
            LeadId = request.LeadId,
            ContactId = request.ContactId,
            DealId = request.DealId,
            MemberId = request.MemberId,
        };
        await _messages.AddAsync(message, ct);
        if (!request.PersonWaiting) _signal.Notify();
        return message;
    }

    public async Task QueueAsync(Guid? organizationId, string eventName, object payload, CancellationToken ct = default)
    {
        if (organizationId is not Guid org) return;
        try
        {
            // Nothing to queue for a workspace that posts nowhere; one that sets an
            // address later starts with the events after that.
            var workspace = await _organizations.GetByIdAsync(org, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(workspace?.OutboundWebhookUrl)) return;
            var now = DateTime.UtcNow;
            // Not the request's token: the save that raised the event has happened, and a
            // client that hangs up now must not take its event with it.
            await _messages.AddAsync(new OutboundMessage
            {
                Id = Guid.NewGuid(),
                OrganizationId = org,
                Channel = OutboundChannels.Webhook,
                Source = OutboundSources.Webhook,
                Status = OutboundStatuses.Queued,
                NextAttemptAtUtc = now,
                CreatedAtUtc = now,
                EventName = eventName,
                // Serialized as it always was, so a receiver's mapping of the fields still holds.
                PayloadJson = JsonSerializer.Serialize(payload),
            }, CancellationToken.None);
            _signal.Notify();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outbound webhook {Event} for {OrganizationId} could not be queued", eventName, org);
        }
    }

    // ── Sending ───────────────────────────────────────────────────────────────

    public Task<OutboxAttempt> SendNowAsync(OutboundMessage message, CancellationToken ct = default) =>
        AttemptEmailAsync(message, personWaiting: true, ct);

    public async Task ProcessAsync(DueOutboundMessage due, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (!await _messages.TryClaimAsync(due.Id, due.Attempts, now, now.Add(ClaimFor), ct)) return;
        var message = await _messages.GetAsync(due.Id, ct);
        if (message == null) return;

        try
        {
            if (message.Channel == OutboundChannels.Webhook) await AttemptWebhookAsync(message, ct);
            else await AttemptEmailAsync(message, personWaiting: false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Something of ours failed before the attempt was recorded: it is tried again
            // like a send that did not go through.
            _logger.LogError(ex, "Outbox message {MessageId} could not be attempted", message.Id);
            if (message.Status != OutboundStatuses.Queued) return;
            try
            {
                await RetryLaterAsync(message, "Cadence could not finish this attempt; it will try again.", personWaiting: false, ct);
            }
            catch (Exception saveEx) when (saveEx is not OperationCanceledException)
            {
                _logger.LogError(saveEx, "Outbox message {MessageId} could not be put back; its claim will run out instead", message.Id);
            }
        }
    }

    private async Task<OutboxAttempt> AttemptEmailAsync(OutboundMessage message, bool personWaiting, CancellationToken ct)
    {
        var to = Addresses(Split(message.To));
        var cc = Addresses(Split(message.Cc));
        var bcc = Addresses(Split(message.Bcc));

        // Asked again at the moment it goes: someone may have unsubscribed or bounced
        // since it was queued, and a "stop" has to win over a message already waiting.
        if (message.OrganizationId is Guid org
            && await RefusalAsync(org, to.Concat(cc).Concat(bcc).ToList(), message.LeadId, message.ContactId, ct) is { } refusal)
        {
            Stop(message, OutboundStatuses.Suppressed, refusal.Description);
            await _messages.SaveAsync(ct);
            _logger.LogInformation("Outbox email {MessageId} was not sent: {Reason}", message.Id, refusal.Description);
            // The person who wrote it was told it was on its way.
            if (!personWaiting && message.CountsAsResponse)
                await TellCreatorAsync(message, "An email was not sent", refusal.Description, ct);
            return new OutboxAttempt(OutboxAttemptResult.Suppressed, refusal.Description, refusal.Code);
        }

        var result = await _email.SendOutboxAsync(new OutboxEmail(
            message.FromName, message.FromAddress, Split(message.ReplyTo), to, cc, bcc, message.RecipientName,
            message.Subject ?? "", message.TextBody ?? "", message.HtmlBody ?? "", message.MessageIdHeader ?? $"<{message.Id:N}@{MessageIdDomain(null)}>",
            message.UnsubscribeUrl, message.Id), ct);
        message.LastStatusCode = result.Code;

        switch (result.Outcome)
        {
            case OutboxSendOutcome.Sent:
                message.Status = OutboundStatuses.Sent;
                message.SentAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                message.ClaimedUntilUtc = null;
                message.LastError = null;
                await _messages.SaveAsync(ct);
                await WriteTimelineAsync(message, ct);
                return new OutboxAttempt(OutboxAttemptResult.Sent);

            case OutboxSendOutcome.NotConfigured when personWaiting:
                // The person is told it was not sent and may send it another way, so it
                // must not go later as well. Unattended mail waits for the server instead.
                Stop(message, OutboundStatuses.Failed, result.Error);
                await _messages.SaveAsync(ct);
                return new OutboxAttempt(OutboxAttemptResult.NotConfigured, result.Error);

            case OutboxSendOutcome.Rejected:
                Stop(message, OutboundStatuses.Failed, result.Error);
                await _messages.SaveAsync(ct);
                await AfterGivingUpAsync(message, personWaiting, ct);
                return new OutboxAttempt(OutboxAttemptResult.Failed, result.Error);

            default:
                return await RetryLaterAsync(message, result.Error ?? "The mail server did not take it.", personWaiting, ct);
        }
    }

    private async Task AttemptWebhookAsync(OutboundMessage message, CancellationToken ct)
    {
        var workspace = message.OrganizationId is Guid org ? await _organizations.GetByIdAsync(org, ct) : null;
        var address = workspace?.OutboundWebhookUrl?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            Stop(message, OutboundStatuses.Cancelled, "The workspace stopped posting events before this one went.");
            await _messages.SaveAsync(ct);
            return;
        }
        // Checked again here: the address was public when it was saved, and is checked
        // once more on the address actually dialled.
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !PublicAddress.LooksPublic(uri))
        {
            Stop(message, OutboundStatuses.Failed, "The workspace's address is not a public https URL.");
            await _messages.SaveAsync(ct);
            return;
        }

        var body = WebhookBody(message, DateTime.UtcNow);
        var headers = new Dictionary<string, string>
        {
            ["X-Cadence-Event"] = message.EventName ?? "",
            // The same for every attempt at one event, so a receiver can drop a repeat.
            ["X-Cadence-Event-Id"] = message.Id.ToString("D"),
        };
        if (!string.IsNullOrWhiteSpace(workspace!.OutboundWebhookSecret))
            headers["X-Cadence-Signature"] = Signature(workspace.OutboundWebhookSecret, body);

        var answer = await _webhooks.PostAsync(uri, body, headers, ct);
        message.LastStatusCode = answer.StatusCode;
        if (answer.Succeeded)
        {
            message.Status = OutboundStatuses.Sent;
            message.SentAtUtc = DateTime.UtcNow;
            message.NextAttemptAtUtc = null;
            message.ClaimedUntilUtc = null;
            message.LastError = null;
            await _messages.SaveAsync(ct);
            return;
        }
        if (answer.StatusCode == 410)
        {
            // Zapier and others answer 410 for a hook that has been deleted.
            Stop(message, OutboundStatuses.Failed, "The receiver answered 410 Gone: the address no longer takes events.");
            await _messages.SaveAsync(ct);
            return;
        }
        await RetryLaterAsync(message, answer.Error ?? $"The receiver answered {answer.StatusCode}.", personWaiting: false, ct);
    }

    /// <summary>
    /// The body posted for one attempt. eventName, payload and sentAtUtc are the fields
    /// receivers have always had; id (the same on every attempt), organizationId and
    /// occurredAtUtc came with the outbox.
    /// </summary>
    internal static string WebhookBody(OutboundMessage message, DateTime sentAtUtc)
    {
        using var payload = JsonDocument.Parse(string.IsNullOrWhiteSpace(message.PayloadJson) ? "{}" : message.PayloadJson);
        return JsonSerializer.Serialize(new
        {
            eventName = message.EventName,
            payload = payload.RootElement,
            sentAtUtc,
            id = message.Id,
            organizationId = message.OrganizationId,
            occurredAtUtc = message.CreatedAtUtc,
        });
    }

    /// <summary>Hex HMAC-SHA256 of the body with the workspace's secret, as X-Cadence-Signature has always been.</summary>
    internal static string Signature(string secret, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private async Task<OutboxAttempt> RetryLaterAsync(OutboundMessage message, string error, bool personWaiting, CancellationToken ct)
    {
        message.LastError = Clip(error, 1000);
        message.ClaimedUntilUtc = null;
        if (message.Attempts >= MaxAttempts)
        {
            message.Status = OutboundStatuses.Failed;
            message.NextAttemptAtUtc = null;
            await _messages.SaveAsync(ct);
            _logger.LogWarning("Outbox message {MessageId} failed after {Attempts} attempts: {Error}", message.Id, message.Attempts, message.LastError);
            await AfterGivingUpAsync(message, personWaiting: false, ct);
            return new OutboxAttempt(OutboxAttemptResult.Failed, message.LastError);
        }
        message.NextAttemptAtUtc = DateTime.UtcNow.Add(RetryDelay(message.Attempts));
        await _messages.SaveAsync(ct);
        return new OutboxAttempt(OutboxAttemptResult.Queued, message.LastError);
    }

    /// <summary>
    /// The wait after <paramref name="attemptsMade"/> attempts, give or take a tenth so
    /// that messages that failed together are not all tried again in the same second.
    /// </summary>
    internal static TimeSpan RetryDelay(int attemptsMade)
    {
        var delay = RetryDelays[Math.Clamp(attemptsMade - 1, 0, RetryDelays.Length - 1)];
        return TimeSpan.FromTicks((long)(delay.Ticks * (0.9 + Random.Shared.NextDouble() * 0.2)));
    }

    /// <summary>
    /// A message the outbox has given up on. A sequence does not carry on as if its
    /// email had gone, and whoever queued it hears about it, unless they were told already.
    /// </summary>
    private async Task AfterGivingUpAsync(OutboundMessage message, bool personWaiting, CancellationToken ct)
    {
        if (message.Channel != OutboundChannels.Email) return;
        try
        {
            if (message.Source == OutboundSources.Sequence && message.SourceId is Guid enrollmentId)
            {
                // Completed too: a sequence whose last email never arrived did not complete.
                var enrollment = await _targets.GetEnrollmentAsync(enrollmentId, message.OrganizationId, ct);
                if (enrollment is { Status: "active" or "paused" or "completed" })
                {
                    enrollment.Status = "stopped";
                    enrollment.NextActionAtUtc = null;
                    await _targets.SaveEnrollmentAsync(enrollment, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The sequence behind outbox email {MessageId} could not be stopped", message.Id);
        }
        if (!personWaiting)
            await TellCreatorAsync(message, "An email could not be sent", message.LastError ?? "It did not go through.", ct);
    }

    /// <summary>A notification to whoever queued the message, or the workspace's owner when they have left.</summary>
    private async Task TellCreatorAsync(OutboundMessage message, string title, string reason, CancellationToken ct)
    {
        try
        {
            if (message.OrganizationId is not Guid org) return;
            Guid? recipient = message.CreatedByUserId is Guid creator && await _organizations.GetMemberRoleAsync(creator, org, ct) != null
                ? creator
                : (await _organizations.GetByIdAsync(org, ct))?.OwnerUserId;
            if (recipient is not Guid userId) return;
            await _notifications.CreateAsync(userId, org, new CreateNotificationRequest(
                "system", title, Clip($"\"{message.Subject}\" to {message.To}: {reason}", 1000), RecordLink(message),
                SourceKey: $"outbox:{message.Id}"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nobody could be told about outbox email {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// The email on the record's timeline, once, now that it has gone. A person's email
    /// is their response, as when they log one by hand: the first-response clock stops
    /// and the person's sequence ends. Sequence and automation mail is automated.
    /// </summary>
    private async Task WriteTimelineAsync(OutboundMessage message, CancellationToken ct)
    {
        if (message.LeadId == null && message.ContactId == null && message.DealId == null) return;
        try
        {
            var author = message.CreatedByUserId
                ?? (message.OrganizationId is Guid org ? (await _organizations.GetByIdAsync(org, ct))?.OwnerUserId : null);
            if (author is not Guid userId) return;
            var created = await _activities.CreateAsync(userId, message.OrganizationId, new CreateActivityRequest
            {
                Type = "email",
                // As each sender wrote it before: "Email: …" for what a person sent from
                // the composer, the subject alone for a sequence's email.
                Subject = Clip(message.CountsAsResponse ? $"Email: {message.Subject}" : message.Subject, 200),
                Body = message.Body,
                LeadId = message.LeadId,
                ContactId = message.ContactId,
                DealId = message.DealId,
                Participants = Clip(string.Join(", ", Split(message.To).Concat(Split(message.Cc))), 500),
                IsAutomated = !message.CountsAsResponse,
            }, ct);
            if (created.IsSuccess)
            {
                message.ActivityId = created.Value.Id;
                await _messages.SaveAsync(ct);
            }
            else
            {
                _logger.LogWarning("Outbox email {MessageId} went, but its timeline entry was refused: {Error}", message.Id, created.Error.Description);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Outbox email {MessageId} went, but its timeline entry could not be written", message.Id);
        }
    }

    // ── People ────────────────────────────────────────────────────────────────

    public async Task<Result> CancelAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var message = await _messages.GetAsync(id, ct);
        if (message == null || message.OrganizationId != organizationId || message.Channel != OutboundChannels.Email)
            return new Error("Email.NotFound", "That email does not exist.");
        var role = await _organizations.GetMemberRoleAsync(userId, organizationId, ct);
        if (message.CreatedByUserId != userId && role is not (OrgMemberRole.Owner or OrgMemberRole.Manager))
            return new Error("Email.NotOwner", "Only whoever sent it, or a manager, can cancel it.");
        if (message.Status != OutboundStatuses.Queued)
            return new Error("Email.Conflict", message.Status == OutboundStatuses.Sent ? "It has already gone." : "It is no longer waiting to be sent.");
        if (!await _messages.TryCancelAsync(id, DateTime.UtcNow, ct))
            return new Error("Email.Conflict", "It is being sent right now, so it may already have gone.");
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<WebhookDeliveryDto>>> WebhookDeliveriesAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        // The same people who may change where events go.
        var role = await _organizations.GetMemberRoleAsync(userId, organizationId, ct);
        if (role is not (OrgMemberRole.Owner or OrgMemberRole.Manager)) return DomainErrors.Organization.NotOwnerOrManager;
        var rows = await _messages.WebhookDeliveriesAsync(organizationId, 50, ct);
        return rows.Select(m => new WebhookDeliveryDto(
            m.Id, m.EventName ?? "", m.Status, m.Attempts, m.LastStatusCode, m.LastError, m.CreatedAtUtc, m.SentAtUtc,
            m.Status == OutboundStatuses.Queued ? m.NextAttemptAtUtc : null)).ToList();
    }

    public Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default) => _messages.HasQueuedAsync(sourceId, ct);

    // ── Rules ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Why nothing may go to these people, or null: the one suppression list every sender
    /// reads. A person marked do not contact (an unsubscribe marks them so) by their
    /// address, or through the lead or contact the message is about; and an address the
    /// mail service reported as bounced, invalid or complaining.
    /// </summary>
    private async Task<Error?> RefusalAsync(Guid organizationId, IReadOnlyList<string> addresses, Guid? leadId, Guid? contactId, CancellationToken ct)
    {
        if (await _contacts.FindDoNotContactAddressAsync(organizationId, addresses, ct) is { } stopped)
            return new Error(DomainErrors.Contact.DoNotContact.Code, $"{stopped} is marked do not contact, so nothing was sent.");
        if (await _contacts.IsDoNotContactAsync(organizationId, leadId, contactId, ct))
            return new Error(DomainErrors.Contact.DoNotContact.Code, "This person is marked do not contact, so nothing was sent.");
        if (await _messages.FindSuppressionAsync(organizationId, addresses, ct) is { } suppressed)
            return new Error("Email.Suppressed", $"{suppressed.Email} {SuppressedBecause(suppressed)}, so email to it is suppressed and nothing was sent.");
        return null;
    }

    private static string SuppressedBecause(EmailSuppression suppression)
    {
        var on = suppression.CreatedAtUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return suppression.Reason switch
        {
            EmailSuppressionReasons.SpamReport => $"marked this workspace's email as spam on {on}",
            EmailSuppressionReasons.Invalid => $"was refused by the mail service as not deliverable on {on}",
            EmailSuppressionReasons.Unsubscribed => $"unsubscribed through the mail service on {on}",
            _ => $"bounced on {on}",
        };
    }

    private async Task<IReadOnlyDictionary<string, string?>> MergeValuesAsync(
        OutboundEmailRequest request, IReadOnlyList<string> to, OutreachSender sender, CancellationToken ct)
    {
        SequenceTargetState? record = null;
        if (request.LeadId is Guid leadId) record = await _targets.TargetStateAsync("lead", leadId, request.OrganizationId, ct);
        else if (request.ContactId is Guid contactId) record = await _targets.TargetStateAsync("contact", contactId, request.OrganizationId, ct);
        var known = record is { Exists: true } ? record : null;
        return MergeFields.For(
            known?.Name ?? request.RecipientName,
            known?.Email ?? (to.Count == 1 ? to[0] : null),
            known?.CompanyName,
            sender.ReplyToName,
            sender.WorkspaceName);
    }

    private static Error UnfilledError(IReadOnlyList<string> unfilled)
    {
        var list = string.Join(", ", unfilled);
        var example = unfilled[0].TrimEnd('}') + "|…}}";
        return new Error("Email.MergeField", unfilled.Count == 1
            ? $"This email uses {list}, which has nothing to fill it with here, so it was not sent. Fill it in on the record, remove it, or give it a fallback: {example}"
            : $"This email uses {list}, which have nothing to fill them with here, so it was not sent. Fill them in on the record, remove them, or give each a fallback: {example}");
    }

    private string MessageIdDomain(string? fromAddress)
    {
        var address = (string.IsNullOrWhiteSpace(fromAddress) ? _settings.FromAddress : fromAddress) ?? "";
        var at = address.LastIndexOf('@');
        var domain = at < 0 ? "" : address[(at + 1)..].Trim().ToLowerInvariant();
        return domain.Length == 0 ? "cadence.invalid" : domain;
    }

    /// <summary>Random enough that nobody can guess another message's reply address: 105 bits, written in lower case, as mail systems may fold case.</summary>
    internal static string NewReplyToken()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var bytes = RandomNumberGenerator.GetBytes(21);
        return string.Concat(bytes.Select(b => alphabet[b % 32]));
    }

    private static void Stop(OutboundMessage message, string status, string? why)
    {
        message.Status = status;
        message.NextAttemptAtUtc = null;
        message.ClaimedUntilUtc = null;
        message.LastError = Clip(why, 1000);
    }

    private static string? RecordLink(OutboundMessage message) =>
        message.LeadId is Guid lead ? $"/leads/{lead}"
        : message.ContactId is Guid contact ? $"/contacts/{contact}"
        : message.DealId is Guid deal ? $"/deals/{deal}"
        : null;

    /// <summary>"a@x.ch, b@y.ch; c@z.ch" or a list → each address once, trimmed.</summary>
    internal static List<string> Addresses(IEnumerable<string>? list) =>
        (list ?? [])
            .SelectMany(a => (a ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> Split(string? list) =>
        string.IsNullOrWhiteSpace(list) ? [] : list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A line break in a subject would start a new header line, so a subject stays one line.</summary>
    internal static string OneLine(string? text) =>
        string.Join(' ', (text ?? "").Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();

    private static string? Clip(string? text, int max) =>
        text == null ? null : text.Length <= max ? text : text[..max];
}
