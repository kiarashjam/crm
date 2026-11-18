using ACI.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// "jobs": the background jobs on this instance (<see cref="IJobHeartbeats"/>). Degraded when one
/// is silent (its loop has stopped or hangs) or its last run threw; never Unhealthy.
/// </summary>
/// <remarks>
/// Never Unhealthy, on purpose: /health is what App Service's health check and the availability
/// test call, and an Unhealthy answer has App Service restart or replace the instance. A long
/// pass (a club's billing run, the six-hourly rescoring) reads as silent for a while, and a
/// restart in the middle of it would do harm. A stopped job is alerted on instead: the
/// "job.silent" monitoring signal, at Error severity, names it.
/// </remarks>
/// <remarks>
/// The data gives each job's last sign of life and its failures in a row, by name. Never its
/// last error: /health is anonymous, and an error's message can name a server, a login or a
/// workspace. The System health page reads that from the registry.
/// </remarks>
public sealed class JobsHealthCheck : IHealthCheck
{
    public const string Name = "jobs";

    /// <summary>
    /// How long after it registers a job that has not run yet is left alone. Several wait a few
    /// minutes after start before their first pass, so a deploy does not begin with a burst of
    /// work; the POS import waits four minutes and then looks every minute, which by its interval
    /// alone would read as stopped for the minute before its first pass.
    /// </summary>
    public static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(10);

    private readonly IJobHeartbeats _heartbeats;
    private readonly TimeProvider _clock;

    public JobsHealthCheck(IJobHeartbeats heartbeats, TimeProvider? clock = null)
    {
        _heartbeats = heartbeats;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var jobs = _heartbeats.Snapshot();
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var silent = new List<string>();
        var failing = new List<string>();

        foreach (var job in jobs)
        {
            var state = StateOf(job, now);
            if (state == "silent") silent.Add(job.Job);
            if (job.IsFailing) failing.Add(job.Job);
            data[job.Job] = new
            {
                state,
                lastSeenUtc = job.LastSeenAtUtc,
                lastSuccessUtc = job.LastBeatAtUtc,
                failures = job.ConsecutiveFailures,
                intervalSeconds = (int)job.Interval.TotalSeconds,
            };
        }

        if (silent.Count > 0)
        {
            var also = failing.Count > 0 ? $" Failing: {string.Join(", ", failing)}." : "";
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Not heard from for three of their intervals: {string.Join(", ", silent)}.{also}", data: data));
        }
        if (failing.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Their last run failed: {string.Join(", ", failing)}.", data: data));
        }
        return Task.FromResult(HealthCheckResult.Healthy(
            jobs.Count == 0 ? "No background jobs run on this instance." : $"All {jobs.Count} background jobs are running.", data));
    }

    /// <summary>"silent", "failing", "waiting" (not run yet, within its grace) or "ok".</summary>
    public static string StateOf(JobHeartbeat job, DateTime nowUtc)
    {
        var neverRan = job.LastBeatAtUtc == null && job.LastFailureAtUtc == null;
        if (job.IsSilent(nowUtc) && !(neverRan && nowUtc - job.RegisteredAtUtc < StartupGrace)) return "silent";
        if (job.IsFailing) return "failing";
        return neverRan ? "waiting" : "ok";
    }
}
