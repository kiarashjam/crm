using System.Globalization;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Monitoring;

/// <inheritdoc cref="IHealthDigest"/>
/// <remarks>
/// <para>
/// Due from 08:00 Europe/Zurich, covering the 24 hours before 08:00 that day, so a digest sent
/// late (the server was down at eight) leaves nothing out and counts nothing twice. A workspace
/// with nothing to report gets none.
/// </para>
/// <para>
/// It goes to each member who may manage the workspace's settings, as a notification (System
/// health, in the bell) and an email through the outbox, written for the team (ForOwnTeam). The
/// notification is the claim, as the morning brief's: its key, one per person, workspace and
/// day, is unique, so after a restart, or with two servers, a person gets the day's digest once.
/// The email is queued after it is saved: a failure there costs that copy by email, never a
/// second one.
/// </para>
/// <para>
/// "Emails that could not be sent" are the emails the outbox gave up on that were queued in the
/// window, or whose sender was told so in it (the outbox's "outbox:{id}" notification): it gives
/// up on some only after two days of retries.
/// </para>
/// </remarks>
public sealed class HealthDigestService : IHealthDigest
{
    /// <summary>When the day's digest falls due, Zurich time.</summary>
    public static readonly TimeSpan DueAt = TimeSpan.FromHours(8);

    private const string Zone = "Europe/Zurich";

    private readonly AppDbContext _db;
    private readonly IOutbox _outbox;
    private readonly EmailSettings _email;
    private readonly ILogger<HealthDigestService> _logger;

    public HealthDigestService(AppDbContext db, IOutbox outbox, IOptions<EmailSettings> email, ILogger<HealthDigestService> logger)
    {
        _db = db;
        _outbox = outbox;
        _email = email.Value;
        _logger = logger;
    }

    /// <summary>The day a digest due at <paramref name="nowUtc"/> is for, and the 24 hours it covers; null before 08:00.</summary>
    public static (DateOnly Day, DateTime FromUtc, DateTime ToUtc)? WindowFor(DateTime nowUtc)
    {
        var zone = RegionalSettings.ZoneOf(Zone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        if (local.TimeOfDay < DueAt) return null;
        var due = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date + DueAt, DateTimeKind.Unspecified), zone);
        return (DateOnly.FromDateTime(local), due.AddHours(-24), due);
    }

    /// <summary>The notification's key, and the claim: one digest per person, workspace and day.</summary>
    public static string SourceKey(Guid organizationId, DateOnly day) =>
        $"health-digest:{organizationId:N}:{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    public async Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        if (WindowFor(nowUtc) is not { } window) return 0;
        var workspaces = await _db.Organizations.AsNoTracking()
            .Select(o => new { o.Id, o.Name, o.OwnerUserId })
            .ToListAsync(ct);
        var sent = 0;
        foreach (var workspace in workspaces)
        {
            // One workspace's digest failing never holds up another's.
            try
            {
                if (await SendOneAsync(workspace.Id, workspace.Name, workspace.OwnerUserId, window, nowUtc, ct)) sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The System health digest for workspace {OrganizationId} could not be prepared", workspace.Id);
            }
        }
        return sent;
    }

    private async Task<bool> SendOneAsync(Guid organizationId, string workspaceName, Guid ownerId,
        (DateOnly Day, DateTime FromUtc, DateTime ToUtc) window, DateTime nowUtc, CancellationToken ct)
    {
        // Who should hear: the people who may manage its settings, with an address to write to.
        var members = await (
                from m in _db.OrganizationMembers.AsNoTracking()
                where m.OrganizationId == organizationId
                join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
                select new { m.UserId, m.Role, m.PermissionPreset, m.Permissions, u.Name, u.Email })
            .ToListAsync(ct);
        var recipients = members
            .Where(m => MemberPermissions.Effective(m.Role, m.PermissionPreset, m.Permissions).Contains(MemberPermissions.ManageSettings))
            .Where(m => !string.IsNullOrWhiteSpace(m.Email))
            .ToList();
        if (recipients.Count == 0) return false;

        var key = SourceKey(organizationId, window.Day);
        // Read past the filters, before acting for anyone: whose digest has already gone today.
        var done = (await _db.Notifications.IgnoreQueryFilters().AsNoTracking()
                .Where(n => n.SourceKey == key)
                .Select(n => n.UserId)
                .ToListAsync(ct))
            .ToHashSet();
        recipients = recipients.Where(r => !done.Contains(r.UserId)).ToList();
        if (recipients.Count == 0) return false;

        // Everything below as this workspace: its records, the outbox's checks.
        using var acting = _db.ActFor(organizationId, ownerId);
        var counts = await CountAsync(organizationId, window.FromUtc, window.ToUtc, nowUtc, ct);
        if (counts.Total == 0) return false;

        var link = $"{(_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/')}{SystemHealth.PagePath}";
        var any = false;
        foreach (var recipient in recipients)
        {
            var (subject, body) = Compose(recipient.Name, workspaceName, counts, link);
            var claim = await ClaimAsync(recipient.UserId, organizationId, key, subject, counts, nowUtc, ct);
            if (claim == null) continue;
            any = true;
            try
            {
                var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
                {
                    OrganizationId = organizationId,
                    Source = OutboundSources.HealthDigest,
                    SourceId = claim.Id,
                    To = [recipient.Email!],
                    RecipientName = recipient.Name,
                    // A workspace's name is never read as a placeholder.
                    Subject = subject.Replace("{{", "{ {", StringComparison.Ordinal),
                    Body = body.Replace("{{", "{ {", StringComparison.Ordinal),
                    // For someone on the team about the workspace: nothing to unsubscribe from.
                    ForOwnTeam = true,
                }, ct);
                if (queued.IsFailure)
                    _logger.LogWarning("The System health digest for user {UserId} in workspace {OrganizationId} was not queued: {Error}",
                        recipient.UserId, organizationId, queued.Error.Description);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The notification is in their bell already: never a second one by trying the day again.
                _logger.LogWarning(ex, "The System health digest for user {UserId} in workspace {OrganizationId} could not be queued",
                    recipient.UserId, organizationId);
            }
        }
        return any;
    }

    /// <summary>What happened in the workspace in the window.</summary>
    public sealed record DigestCounts(int FailedRuns, int Bounces, int SpamReports, int NewSuppressions, int Handoffs, int FailedEmails)
    {
        public int Total => FailedRuns + Bounces + SpamReports + NewSuppressions + Handoffs + FailedEmails;
    }

    private async Task<DigestCounts> CountAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, DateTime nowUtc, CancellationToken ct)
    {
        var failedRuns = await _db.AutomationRuns.AsNoTracking()
            .CountAsync(r => r.OrganizationId == organizationId && r.Status == AutomationRunStatuses.Failed
                && (r.FinishedAtUtc ?? r.UpdatedAtUtc) >= fromUtc && (r.FinishedAtUtc ?? r.UpdatedAtUtc) < toUtc, ct);

        var reported = _db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Email
                && m.DeliveryUpdatedAtUtc >= fromUtc && m.DeliveryUpdatedAtUtc < toUtc);
        var bounces = await reported.CountAsync(m => m.DeliveryStatus == "bounced", ct);
        var spam = await reported.CountAsync(m => m.DeliveryStatus == "spam_report", ct);

        var suppressions = await _db.EmailSuppressions.AsNoTracking()
            .CountAsync(s => s.OrganizationId == organizationId && s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc < toUtc
                && s.Reason != EmailSuppressionReasons.Erased, ct);

        // Handoffs handed over in the window that need attention now: failed, given up on, or
        // still not taken by the portal an hour on.
        var lateBefore = nowUtc - SystemHealth.HandoffLateAfter;
        var handoffs = await (
                from o in _db.DealOnboardings.AsNoTracking()
                where o.OrganizationId == organizationId && o.UpdatedAtUtc >= fromUtc && o.UpdatedAtUtc < toUtc
                    && (o.Status == OnboardingStatuses.Failed || o.Status == OnboardingStatuses.Queued)
                join m in _db.OutboundMessages.AsNoTracking() on o.OutboundMessageId equals (Guid?)m.Id into sent
                from m in sent.DefaultIfEmpty()
                where o.Status == OnboardingStatuses.Failed
                    || m == null
                    || m.Status == OutboundStatuses.Failed || m.Status == OutboundStatuses.Cancelled
                    || (m.Status == OutboundStatuses.Queued && o.UpdatedAtUtc < lateBefore)
                select o.Id)
            .CountAsync(ct);

        var queuedInWindow = await _db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Email && m.Status == OutboundStatuses.Failed
                && m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtc)
            .Select(m => m.Id)
            .ToListAsync(ct);
        var toldInWindow = (await _db.Notifications.IgnoreQueryFilters().AsNoTracking()
                .Where(n => n.OrganizationId == organizationId && n.SourceKey != null && n.SourceKey.StartsWith("outbox:")
                    && n.CreatedAtUtc >= fromUtc && n.CreatedAtUtc < toUtc)
                .Select(n => n.SourceKey!)
                .ToListAsync(ct))
            .Select(k => Guid.TryParse(k["outbox:".Length..], out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var failedLater = toldInWindow.Count == 0 ? [] : await _db.OutboundMessages.AsNoTracking()
            .Where(m => toldInWindow.Contains(m.Id) && m.OrganizationId == organizationId
                && m.Channel == OutboundChannels.Email && m.Status == OutboundStatuses.Failed)
            .Select(m => m.Id)
            .ToListAsync(ct);
        var failedEmails = queuedInWindow.Concat(failedLater).Distinct().Count();

        return new DigestCounts(failedRuns, bounces, spam, suppressions, handoffs, failedEmails);
    }

    /// <summary>Takes the person's digest for the day by saving its notification; null when it was taken already.</summary>
    private async Task<Notification?> ClaimAsync(Guid userId, Guid organizationId, string key, string title, DigestCounts counts, DateTime nowUtc, CancellationToken ct)
    {
        var row = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = "system",
            Title = title.Length <= 200 ? title : title[..200],
            Message = string.Join(" · ", Lines(counts)),
            Link = SystemHealth.PagePath,
            CreatedAtUtc = nowUtc,
            SourceKey = key,
        };
        _db.Notifications.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another server took it first.
            _db.Entry(row).State = EntityState.Detached;
            return null;
        }
    }

    private static IEnumerable<string> Lines(DigestCounts c)
    {
        if (c.FailedEmails > 0) yield return Count(c.FailedEmails, "email could not be sent", "emails could not be sent");
        if (c.Handoffs > 0) yield return Count(c.Handoffs, "welcome handoff failed or was not delivered", "welcome handoffs failed or were not delivered");
        if (c.FailedRuns > 0) yield return Count(c.FailedRuns, "automation run failed", "automation runs failed");
        if (c.Bounces > 0) yield return Count(c.Bounces, "email bounced", "emails bounced");
        if (c.SpamReports > 0) yield return Count(c.SpamReports, "spam report", "spam reports");
        if (c.NewSuppressions > 0) yield return Count(c.NewSuppressions, "address added to the do-not-send list", "addresses added to the do-not-send list");
    }

    /// <summary>The digest's subject and plain-text body: counts and a link, nothing about anyone.</summary>
    public static (string Subject, string Body) Compose(string? recipientName, string workspaceName, DigestCounts counts, string link)
    {
        var things = counts.Total == 1 ? "1 thing" : $"{counts.Total.ToString(CultureInfo.InvariantCulture)} things";
        var body = new StringBuilder();
        body.Append("Hi").Append(string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName.Trim()).AppendLine(",");
        body.AppendLine();
        body.AppendLine($"In the last 24 hours in {workspaceName}:");
        foreach (var line in Lines(counts)) body.AppendLine("- " + line);
        body.AppendLine();
        body.AppendLine($"Open System health: {link}");
        body.AppendLine();
        body.AppendLine("You receive this because you manage this workspace's settings. It comes only on a day with something to look at.");
        return ($"System health: {things} to look at in {workspaceName}", body.ToString());
    }

    private static string Count(int n, string one, string many) => $"{n.ToString(CultureInfo.InvariantCulture)} {(n == 1 ? one : many)}";
}
