using System.Text.Json;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Monitoring;

/// <inheritdoc cref="ISystemHealth"/>
/// <remarks>
/// <para>
/// A workspace's own rows, read as that workspace (the request acts for it) and named by its id
/// besides, so nothing of another workspace's ever shows. The jobs are the server's, shared by
/// every workspace, and so is what they say: one line each, as the registry keeps it.
/// </para>
/// <para>
/// Each area is a count and its first rows, newest first. A message the outbox gave up on has no
/// time of its own for that, so "in the last 7 days" counts the messages queued in them: the
/// outbox gives up within about two days of queuing one.
/// </para>
/// </remarks>
public sealed class SystemHealthService : ISystemHealth, IMonitoringReadings
{
    /// <summary>Longer than the outbox keeps trying a message (about two days), so every one it gives up on is still in it.</summary>
    private static readonly TimeSpan FailedMessageWindow = TimeSpan.FromDays(3);
    private const int MaxIds = 5000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IJobHeartbeats _heartbeats;
    private readonly IPortalPings _pings;

    public SystemHealthService(AppDbContext db, IJobHeartbeats heartbeats, IPortalPings pings)
    {
        _db = db;
        _heartbeats = heartbeats;
        _pings = pings;
    }

    // ── The page ──────────────────────────────────────────────────────────────

    public async Task<SystemHealthDto> SnapshotAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct = default)
    {
        var handoffs = await HandoffsAsync(organizationId, nowUtc, ct);
        var outbox = await OutboxAsync(organizationId, nowUtc, ct);
        var webhooks = await WebhooksAsync(organizationId, nowUtc, ct);
        var payments = await PaymentsAsync(organizationId, nowUtc, ct);
        var automations = await AutomationsAsync(organizationId, nowUtc, ct);
        var email = await EmailAsync(organizationId, nowUtc, ct);
        var jobs = Jobs(nowUtc);
        var portal = await PortalAsync(organizationId, ct);

        var jobTrouble = jobs.Count(j => j.Failing || j.Silent);
        var portalTrouble = portal is { Configured: true, Ok: false } ? 1 : 0;
        var counts = new Dictionary<string, int>
        {
            ["handoffs"] = handoffs.Count,
            ["outbox"] = outbox.Count,
            ["webhooks"] = webhooks.Count,
            ["payments"] = payments.Count,
            ["automations"] = automations.Count,
            ["email"] = email.Count,
            ["jobs"] = jobTrouble,
            ["portal"] = portalTrouble,
        };
        var issues = counts.Values.Count(c => c > 0);
        var status = jobTrouble > 0 || portalTrouble > 0 ? "failing" : issues > 0 ? "attention" : "ok";
        return new SystemHealthDto(nowUtc, new SystemHealthOverallDto(status, issues, counts),
            handoffs, outbox, webhooks, payments, automations, email, jobs, portal);
    }

    private async Task<HandoffHealthDto> HandoffsAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc - SystemHealth.HandoffWindow;
        var lateBefore = nowUtc - SystemHealth.HandoffLateAfter;
        // As the deal's Welcome card shows it: the portal reported a failure, or the outbox gave up
        // (or never had it); or it is still being handed over an hour on. Taken by the portal (its
        // message sent) and not yet reported on is fine.
        var query =
            from o in _db.DealOnboardings.AsNoTracking()
            where o.OrganizationId == organizationId && o.UpdatedAtUtc >= since
                && (o.Status == OnboardingStatuses.Failed || o.Status == OnboardingStatuses.Queued)
            join m in _db.OutboundMessages.AsNoTracking() on o.OutboundMessageId equals (Guid?)m.Id into sent
            from m in sent.DefaultIfEmpty()
            where o.Status == OnboardingStatuses.Failed
                || m == null
                || m.Status == OutboundStatuses.Failed || m.Status == OutboundStatuses.Cancelled
                || (m.Status == OutboundStatuses.Queued && o.UpdatedAtUtc < lateBefore)
            join d in _db.Deals.AsNoTracking() on o.DealId equals d.Id into deals
            from d in deals.DefaultIfEmpty()
            select new
            {
                o.Id, o.DealId, DealName = d == null ? null : d.Name, o.Name, o.Status, o.UpdatedAtUtc, o.StepsJson,
                MessageStatus = m == null ? null : m.Status,
                Attempts = m == null ? 0 : m.Attempts,
                MessageError = m == null ? null : m.LastError,
            };

        var count = await query.CountAsync(ct);
        var rows = count == 0 ? [] : await query.OrderByDescending(x => x.UpdatedAtUtc).Take(SystemHealth.ListSize).ToListAsync(ct);
        var items = rows.Select(x =>
        {
            var late = x.Status == OnboardingStatuses.Queued && x.MessageStatus == OutboundStatuses.Queued;
            var error = x.Status == OnboardingStatuses.Failed ? LastFailure(x.StepsJson) ?? x.MessageError
                : x.MessageError ?? (x.MessageStatus == null ? "It could not be queued for the portal." : null);
            return new HandoffIssueDto(x.Id, x.DealId, Blank(x.DealName), x.Name, late ? "late" : "failed", OneLine(error), x.Attempts,
                x.UpdatedAtUtc, $"/deals/{x.DealId}",
                // Resend refuses a handoff still being handed over: there is nothing to retry yet.
                late ? null : new HealthRetryDto("handoff", x.Id));
        }).ToList();
        return new HandoffHealthDto(count, items);
    }

    private async Task<OutboxHealthDto> OutboxAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var cutoff = nowUtc - SystemHealth.QueuedLateAfter;
        var since = nowUtc - SystemHealth.RecentWindow;
        var messages = _db.OutboundMessages.AsNoTracking().Where(m => m.OrganizationId == organizationId);
        // Late (SystemHealth.QueuedLateAfter): tried and still queued, or due half an hour ago and not yet tried.
        var late = messages.Where(m => m.Status == OutboundStatuses.Queued && m.CreatedAtUtc < cutoff
            && (m.Attempts > 0 || m.NextAttemptAtUtc == null || m.NextAttemptAtUtc < cutoff));
        var failed = messages.Where(m => m.Status == OutboundStatuses.Failed && m.CreatedAtUtc >= since);
        var queuedCount = await late.CountAsync(ct);
        var failedCount = await failed.CountAsync(ct);
        if (queuedCount + failedCount == 0) return new OutboxHealthDto(0, 0, 0, []);

        var rows = await messages
            .Where(m => (m.Status == OutboundStatuses.Queued && m.CreatedAtUtc < cutoff
                    && (m.Attempts > 0 || m.NextAttemptAtUtc == null || m.NextAttemptAtUtc < cutoff))
                || (m.Status == OutboundStatuses.Failed && m.CreatedAtUtc >= since))
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(SystemHealth.ListSize)
            .Select(m => new
            {
                m.Id, m.Channel, m.Source, m.SourceId, m.Status, m.To, m.Subject, m.EventName, m.LastError, m.Attempts,
                m.CreatedAtUtc, m.NextAttemptAtUtc, m.LeadId, m.ContactId, m.DealId, m.MemberId,
            })
            .ToListAsync(ct);

        // A failed handoff's message is retried by sending the handoff again, while it is still
        // the handoff's latest attempt.
        var handoffIds = rows.Where(r => r.Source == OutboundSources.Onboarding && r.Status == OutboundStatuses.Failed && r.SourceId != null)
            .Select(r => r.SourceId!.Value).Distinct().ToList();
        var resendable = handoffIds.Count == 0 ? new HashSet<Guid>() : (await _db.DealOnboardings.AsNoTracking()
                .Where(o => o.OrganizationId == organizationId && handoffIds.Contains(o.Id) && o.Status != OnboardingStatuses.Active)
                .Select(o => o.OutboundMessageId)
                .ToListAsync(ct))
            .OfType<Guid>().ToHashSet();

        var items = rows.Select(r =>
        {
            var kind = r.Source == OutboundSources.Onboarding ? "handoff" : r.Channel == OutboundChannels.Webhook ? "webhook" : "email";
            var link = kind switch
            {
                "handoff" => r.DealId is Guid deal ? $"/deals/{deal}" : null,
                "webhook" => "/settings?tab=integrations",
                _ => r.Source == OutboundSources.Automation && r.SourceId is Guid rule ? $"/automations?rule={rule}"
                    : RecordLink(r.LeadId, r.ContactId, r.DealId, r.MemberId)
                      ?? (r.Source is OutboundSources.Campaign ? "/campaigns" : null),
            };
            var retry = kind == "handoff" && r.Status == OutboundStatuses.Failed && r.SourceId is Guid handoff && resendable.Contains(r.Id)
                ? new HealthRetryDto("handoff", handoff)
                : null;
            return new OutboxIssueDto(r.Id, kind, r.Status == OutboundStatuses.Failed ? "failed" : "queued",
                kind == "email" ? r.To : r.EventName, kind == "email" ? r.Subject : null, OneLine(r.LastError), r.Attempts,
                r.CreatedAtUtc, r.Status == OutboundStatuses.Queued ? r.NextAttemptAtUtc : null, link, retry);
        }).ToList();
        return new OutboxHealthDto(queuedCount + failedCount, queuedCount, failedCount, items);
    }

    private async Task<WebhookHealthDto> WebhooksAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc - SystemHealth.RecentWindow;
        // Settings, Integrations' deliveries: handoffs go to the portal's address, and show above.
        var failed = _db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Webhook
                && m.Source != OutboundSources.Onboarding && m.Status == OutboundStatuses.Failed && m.CreatedAtUtc >= since);
        var count = await failed.CountAsync(ct);
        var items = count == 0 ? [] : await failed.OrderByDescending(m => m.CreatedAtUtc).Take(SystemHealth.ListSize)
            .Select(m => new WebhookDeliveryDto(m.Id, m.EventName ?? "", m.Status, m.Attempts, m.LastStatusCode, m.LastError, m.CreatedAtUtc, m.SentAtUtc, null))
            .ToListAsync(ct);
        return new WebhookHealthDto(count, items.Select(i => i with { LastError = OneLine(i.LastError) }).ToList(), "/settings?tab=integrations");
    }

    private async Task<PaymentHealthDto> PaymentsAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc - SystemHealth.RecentWindow;
        // Each Stripe event is the workspace's own: its post names the workspace by its address.
        var failed = _db.ClubProviderEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.Status == "failed" && e.ReceivedAtUtc >= since);
        var count = await failed.CountAsync(ct);
        var items = count == 0 ? [] : await failed.OrderByDescending(e => e.ReceivedAtUtc).Take(SystemHealth.ListSize)
            .Select(e => new PaymentIssueDto(e.Id, e.EventId, e.Type, e.Note, e.Attempts, e.ReceivedAtUtc))
            .ToListAsync(ct);
        return new PaymentHealthDto(count, items.Select(i => i with { Note = OneLine(i.Note) }).ToList(), "/club-payments");
    }

    private async Task<AutomationHealthDto> AutomationsAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc - SystemHealth.RecentWindow;
        var failed = _db.AutomationRuns.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Status == AutomationRunStatuses.Failed
                && (r.FinishedAtUtc ?? r.UpdatedAtUtc) >= since);
        var count = await failed.CountAsync(ct);
        var rows = count == 0 ? [] : await failed.OrderByDescending(r => r.FinishedAtUtc ?? r.UpdatedAtUtc).Take(SystemHealth.ListSize)
            .Select(r => new { r.Id, r.RuleId, r.RuleName, r.RecordName, r.LastError, r.StepAttempts, At = r.FinishedAtUtc ?? r.UpdatedAtUtc })
            .ToListAsync(ct);
        return new AutomationHealthDto(count, rows.Select(r => new AutomationIssueDto(
            r.Id, r.RuleId, r.RuleName, Blank(r.RecordName), OneLine(r.LastError), r.StepAttempts, r.At,
            $"/automations?rule={r.RuleId}", new HealthRetryDto("automation", r.Id))).ToList());
    }

    private async Task<EmailHealthDto> EmailAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc - SystemHealth.RecentWindow;
        var reported = _db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Email
                && m.DeliveryUpdatedAtUtc >= since && (m.DeliveryStatus == "bounced" || m.DeliveryStatus == "spam_report"));
        var bounces = await reported.CountAsync(m => m.DeliveryStatus == "bounced", ct);
        var spam = await reported.CountAsync(m => m.DeliveryStatus == "spam_report", ct);
        // An erasure leaves a fingerprint on the list, not news about mail.
        var suppressions = await _db.EmailSuppressions.AsNoTracking()
            .CountAsync(s => s.OrganizationId == organizationId && s.CreatedAtUtc >= since && s.Reason != EmailSuppressionReasons.Erased, ct);
        var rows = bounces + spam == 0 ? [] : await reported.OrderByDescending(m => m.DeliveryUpdatedAtUtc).Take(SystemHealth.ListSize)
            .Select(m => new { m.Id, m.DeliveryStatus, m.To, m.Subject, m.DeliveryDetail, m.DeliveryUpdatedAtUtc, m.LeadId, m.ContactId, m.DealId, m.MemberId })
            .ToListAsync(ct);
        var items = rows.Select(r => new EmailIssueDto(r.Id, r.DeliveryStatus == "bounced" ? "bounce" : "spam_report", r.To, r.Subject,
            OneLine(r.DeliveryDetail), r.DeliveryUpdatedAtUtc ?? nowUtc, RecordLink(r.LeadId, r.ContactId, r.DealId, r.MemberId))).ToList();
        return new EmailHealthDto(bounces + spam, bounces, spam, suppressions, items, "/settings?tab=organization");
    }

    private IReadOnlyList<JobHealthDto> Jobs(DateTime nowUtc) =>
        _heartbeats.Snapshot().Select(j => new JobHealthDto(
            j.Job, (int)Math.Round(j.Interval.TotalSeconds), j.LastSeenAtUtc, j.LastBeatAtUtc, j.LastFailureAtUtc,
            j.IsFailing, j.IsSilent(nowUtc), j.ConsecutiveFailures, j.IsFailing ? j.LastError : null)).ToList();

    private async Task<PortalHealthDto> PortalAsync(Guid organizationId, CancellationToken ct)
    {
        var settings = await _db.OnboardingSettings.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId)
            .Select(s => new { s.WebhookUrl, s.WebhookSecret })
            .FirstOrDefaultAsync(ct);
        var configured = !string.IsNullOrWhiteSpace(settings?.WebhookUrl) && !string.IsNullOrWhiteSpace(settings?.WebhookSecret);
        var last = configured ? _pings.For(organizationId) : null;
        return new PortalHealthDto(configured, last?.Ok, last?.AtUtc, last?.StatusCode, OneLine(last?.Error), "/settings?tab=welcome");
    }

    // ── The alerts' readings, across every workspace ─────────────────────────

    public async Task<MonitoringReadings> ReadAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // The monitoring job acts for no workspace and reads every one's: counts and ids only.
        var cutoff = nowUtc - SystemHealth.QueuedLateAfter;
        var lateEmails = _db.OutboundMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Channel == OutboundChannels.Email && m.Status == OutboundStatuses.Queued && m.CreatedAtUtc < cutoff
                && (m.Attempts > 0 || m.NextAttemptAtUtc == null || m.NextAttemptAtUtc < cutoff));
        var lateCount = await lateEmails.CountAsync(ct);
        var oldest = lateCount == 0 ? null : await lateEmails.MinAsync(m => (DateTime?)m.CreatedAtUtc, ct);

        var failedSince = nowUtc - FailedMessageWindow;
        var failed = await _db.OutboundMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Status == OutboundStatuses.Failed && m.CreatedAtUtc >= failedSince)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(MaxIds)
            .Select(m => new { m.Id, m.Channel, m.Source })
            .ToListAsync(ct);

        var handoffsSince = nowUtc - SystemHealth.HandoffWindow;
        var lateBefore = nowUtc - SystemHealth.HandoffLateAfter;
        var lateHandoffs = await (
                from o in _db.DealOnboardings.IgnoreQueryFilters().AsNoTracking()
                where o.Status == OnboardingStatuses.Queued && o.UpdatedAtUtc < lateBefore && o.UpdatedAtUtc >= handoffsSince
                join m in _db.OutboundMessages.IgnoreQueryFilters().AsNoTracking() on o.OutboundMessageId equals (Guid?)m.Id
                where m.Status == OutboundStatuses.Queued
                select o.OrganizationId)
            .ToListAsync(ct);

        var stripeSince = nowUtc - SystemHealth.RecentWindow;
        var stripe = await _db.ClubProviderEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Status == "failed" && e.ReceivedAtUtc >= stripeSince)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(MaxIds)
            .Select(e => new { e.Id, e.EventId })
            .ToListAsync(ct);

        return new MonitoringReadings(
            lateCount,
            oldest is DateTime at ? (int)Math.Floor((nowUtc - at).TotalMinutes) : 0,
            failed.ToDictionary(m => m.Id, m => m.Source == OutboundSources.Onboarding ? "handoff" : m.Channel == OutboundChannels.Webhook ? "webhook" : "email"),
            lateHandoffs.Count,
            lateHandoffs.Distinct().Count(),
            stripe.ToDictionary(e => e.Id, e => e.EventId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? RecordLink(Guid? leadId, Guid? contactId, Guid? dealId, Guid? memberId) =>
        leadId is Guid lead ? $"/leads/{lead}"
        : contactId is Guid contact ? $"/contacts/{contact}"
        : dealId is Guid deal ? $"/deals/{deal}"
        : memberId is Guid member ? $"/members/{member}"
        : null;

    /// <summary>The latest failure the portal reported on a handoff, in its own words.</summary>
    private static string? LastFailure(string stepsJson)
    {
        try
        {
            var steps = JsonSerializer.Deserialize<List<OnboardingStepDto>>(stepsJson, Json) ?? [];
            var failure = steps.LastOrDefault(s => s.Key is "failed" or "email_failed" or "undeliverable");
            return failure == null ? null : failure.Detail ?? (failure.Key == "email_failed" ? "The portal could not send the welcome email." : "The portal reported a failure.");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>An error as one short line: what the page lists, never a stack or a page of HTML.</summary>
    internal static string? OneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var line = string.Join(' ', text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length <= 300 ? line : line[..297] + "...";
    }
}
