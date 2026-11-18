using ACI.Application.DTOs;

namespace ACI.Application.Services;

/// <summary>
/// System health: what has gone wrong in a workspace, in one place, so the team finds a
/// failure before a customer does. Handoffs the portal has not taken, email and webhooks the
/// outbox gave up on or still holds, Stripe events that failed, automation runs that failed,
/// bounces and spam reports; and, for everyone, the background jobs and the portal's last ping.
/// </summary>
/// <remarks>
/// Each list is counted in full and its first <see cref="SystemHealth.ListSize"/> rows read,
/// newest first, so the page stays cheap however much has gone wrong.
/// </remarks>
public interface ISystemHealth
{
    /// <summary>The workspace's health as it stands at <paramref name="nowUtc"/>.</summary>
    Task<SystemHealthDto> SnapshotAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>
/// The signals the alert rules watch, read across every workspace (see
/// MonitoringSignalsBackgroundService): counts and ids only, never a person's details.
/// </summary>
public interface IMonitoringReadings
{
    Task<MonitoringReadings> ReadAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>
/// The daily System health email: from 08:00 Europe/Zurich, to the people who manage each
/// workspace's settings, for a workspace with something to report from the day before.
/// </summary>
public interface IHealthDigest
{
    /// <summary>Sends every digest that is due. Returns how many workspaces' went.</summary>
    Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>The limits the page, the alerts and the digest share.</summary>
public static class SystemHealth
{
    /// <summary>The rows each list shows; the count says how many there are in all.</summary>
    public const int ListSize = 20;

    /// <summary>How far back a failure is still news: failed messages, runs, Stripe events, bounces.</summary>
    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(7);

    /// <summary>How long a handoff that needs attention stays on the page.</summary>
    public static readonly TimeSpan HandoffWindow = TimeSpan.FromDays(30);

    /// <summary>A handoff the portal has not taken after this long is late.</summary>
    public static readonly TimeSpan HandoffLateAfter = TimeSpan.FromHours(1);

    /// <summary>
    /// A message still queued after this long is stuck: tried and still waiting to go, or due
    /// that long ago and not yet tried. One held for later (a newsletter spread over minutes) is
    /// not late before it is due.
    /// </summary>
    public static readonly TimeSpan QueuedLateAfter = TimeSpan.FromMinutes(30);

    /// <summary>The page's own address, in the app.</summary>
    public const string PagePath = "/settings?tab=health";
}

/// <summary>What the monitoring job reads on each pass, across every workspace.</summary>
/// <param name="LateEmails">Emails queued more than 30 minutes and not gone.</param>
/// <param name="OldestLateEmailMinutes">How long the oldest of them has waited.</param>
/// <param name="FailedMessageIds">Messages (of every kind) given up on, queued in the last few days, by id and kind.</param>
/// <param name="LateHandoffs">Handoffs the portal has not taken after an hour.</param>
/// <param name="LateHandoffWorkspaces">The workspaces they are in.</param>
/// <param name="FailedStripeEventIds">Stripe events that failed, received in the last week: Cadence's row id and Stripe's evt_ id.</param>
public sealed record MonitoringReadings(
    int LateEmails,
    int OldestLateEmailMinutes,
    IReadOnlyDictionary<Guid, string> FailedMessageIds,
    int LateHandoffs,
    int LateHandoffWorkspaces,
    IReadOnlyDictionary<Guid, string> FailedStripeEventIds);
