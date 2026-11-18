using System.Collections.Concurrent;
using ACI.Application.Interfaces;

namespace ACI.WebApi.Services;

/// <summary>The background jobs' heartbeats, in memory: one singleton, written by every job's loop.</summary>
public sealed class JobHeartbeats : IJobHeartbeats
{
    private readonly ConcurrentDictionary<string, JobHeartbeat> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _clock;

    public JobHeartbeats(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public void Register(string job, TimeSpan interval) =>
        _jobs.AddOrUpdate(job,
            _ => new JobHeartbeat(job, interval, Now, null, null, null, 0),
            (_, known) => known with { Interval = interval });

    public void Beat(string job) =>
        _jobs.AddOrUpdate(job,
            _ => new JobHeartbeat(job, TimeSpan.FromMinutes(1), Now, Now, null, null, 0),
            (_, known) => known with { LastBeatAtUtc = Now, ConsecutiveFailures = 0 });

    public void Failed(string job, Exception error)
    {
        var line = OneLine(error);
        _jobs.AddOrUpdate(job,
            _ => new JobHeartbeat(job, TimeSpan.FromMinutes(1), Now, null, Now, line, 1),
            (_, known) => known with { LastFailureAtUtc = Now, LastError = line, ConsecutiveFailures = known.ConsecutiveFailures + 1 });
    }

    public IReadOnlyList<JobHeartbeat> Snapshot() => _jobs.Values.OrderBy(j => j.Job, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The error's type and first line, at most 300 characters: enough to recognise it, never a stack.</summary>
    internal static string OneLine(Exception error)
    {
        var inner = error;
        while (inner is AggregateException { InnerException: { } next }) inner = next;
        var message = (inner.Message ?? "").Split('\n', 2)[0].Trim();
        var line = $"{inner.GetType().Name}: {message}";
        return line.Length <= 300 ? line : line[..297] + "...";
    }
}
