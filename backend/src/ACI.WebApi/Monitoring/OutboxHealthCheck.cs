using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// "outbox": Degraded when an email has waited to go for more than half an hour, or any
/// message was given up on (Failed) in the last day. Read across every workspace, as the
/// dispatcher reads it: a health check acts for none.
/// </summary>
/// <remarks>
/// <para>
/// An email waits from when it was first due: a message not tried yet waits from its
/// <see cref="OutboundMessage.NextAttemptAtUtc"/>, so a newsletter spread over an hour is not
/// late before its turn; one that has been tried waits from when it was queued, so mail that
/// keeps failing on its way to the server counts while it is still being retried.
/// </para>
/// <para>
/// A message records no time it failed, so "in the last day" is the failed messages queued in
/// the last day; one that gives up after its two days of retries is not counted by this.
/// </para>
/// <para>
/// Counts and a minimum, on the (Status, NextAttemptAtUtc) index; and kept for half a minute,
/// because /health is anonymous and nobody should make the database count on every request.
/// </para>
/// </remarks>
public sealed class OutboxHealthCheck : IHealthCheck
{
    public const string Name = "outbox";

    /// <summary>How long an email may wait to go before the outbox counts as behind.</summary>
    public static readonly TimeSpan LateAfter = TimeSpan.FromMinutes(30);

    /// <summary>How far back a failed message makes the outbox Degraded.</summary>
    public static readonly TimeSpan FailedWithin = TimeSpan.FromHours(24);

    /// <summary>How long one answer is reused.</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private const string CacheKey = "health:outbox";

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    public OutboxHealthCheck(AppDbContext db, IMemoryCache cache, TimeProvider? clock = null)
    {
        _db = db;
        _cache = cache;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out HealthCheckResult cached)) return cached;
        var result = await ReadAsync(cancellationToken);
        _cache.Set(CacheKey, result, CacheFor);
        return result;
    }

    private async Task<HealthCheckResult> ReadAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var lateBefore = now - LateAfter;
        var failedSince = now - FailedWithin;
        var messages = _db.OutboundMessages.AsNoTracking().IgnoreQueryFilters();

        // When each queued email began to wait (see the remarks), for those already due.
        var waitingSince = messages
            .Where(m => m.Status == OutboundStatuses.Queued && m.Channel == OutboundChannels.Email)
            .Select(m => m.Attempts == 0 ? m.NextAttemptAtUtc ?? m.CreatedAtUtc : m.CreatedAtUtc)
            .Where(since => since <= now);

        var waiting = await waitingSince.CountAsync(ct);
        var late = waiting == 0 ? 0 : await waitingSince.CountAsync(since => since <= lateBefore, ct);
        var oldest = waiting == 0 ? (DateTime?)null : await waitingSince.MinAsync(since => (DateTime?)since, ct);
        var failed = await messages.CountAsync(m => m.Status == OutboundStatuses.Failed && m.CreatedAtUtc >= failedSince, ct);

        var data = new Dictionary<string, object>
        {
            ["queuedEmails"] = waiting,
            ["lateEmails"] = late,
            ["oldestQueuedMinutes"] = oldest is { } since ? (int)Math.Floor((now - since).TotalMinutes) : 0,
            ["failedLast24h"] = failed,
        };

        var problems = new List<string>();
        if (late > 0) problems.Add($"{late} email{(late == 1 ? " has" : "s have")} waited more than {LateAfter.TotalMinutes:0} minutes to go");
        if (failed > 0) problems.Add($"{failed} message{(failed == 1 ? " was" : "s were")} given up on in the last 24 hours");

        return problems.Count == 0
            ? HealthCheckResult.Healthy("The outbox is keeping up.", data)
            : HealthCheckResult.Degraded(string.Join("; ", problems) + ".", data: data);
    }
}
