namespace ACI.Application.Interfaces;

/// <summary>
/// Whether each background job is still running: every loop says when it has run (or failed),
/// so a job that has stopped, or keeps failing, can be seen by /health, by the System health
/// page and by the alerts, instead of only in a log nobody reads.
/// </summary>
/// <remarks>
/// Kept in memory, per server instance: a restart starts every job afresh, and a job counts as
/// silent only once it has had three of its intervals to run (see <see cref="JobHeartbeat.IsSilent"/>).
/// </remarks>
public interface IJobHeartbeats
{
    /// <summary>Names a job and how often it means to run; called once, as the job starts.</summary>
    void Register(string job, TimeSpan interval);

    /// <summary>One run of the job finished without an error.</summary>
    void Beat(string job);

    /// <summary>One run of the job threw. The job is still alive (it will try again), but failing.</summary>
    void Failed(string job, Exception error);

    /// <summary>Every registered job as it stands now, by name.</summary>
    IReadOnlyList<JobHeartbeat> Snapshot();
}

/// <summary>A background job's last signs of life.</summary>
/// <param name="Job">Its name, as registered ("outbox", "automations"…).</param>
/// <param name="Interval">How often it means to run.</param>
/// <param name="RegisteredAtUtc">When it started, on this instance.</param>
/// <param name="LastBeatAtUtc">Its last run that finished well; none yet: null.</param>
/// <param name="LastFailureAtUtc">Its last run that threw; none: null.</param>
/// <param name="LastError">That run's error, in one line (type and message, no stack).</param>
/// <param name="ConsecutiveFailures">Runs in a row that threw, since the last good one.</param>
public sealed record JobHeartbeat(
    string Job,
    TimeSpan Interval,
    DateTime RegisteredAtUtc,
    DateTime? LastBeatAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastError,
    int ConsecutiveFailures)
{
    /// <summary>How many intervals a job may miss before it counts as stopped.</summary>
    public const int SilentAfterIntervals = 3;

    /// <summary>
    /// However short its interval, a job counts as stopped only after this long: a pass that
    /// takes a few minutes (a backlog, a rescoring) is a busy job, not a stopped one.
    /// </summary>
    public static readonly TimeSpan SilentAtLeast = TimeSpan.FromMinutes(10);

    /// <summary>The last sign of life, a good run or a failed one: either says the loop still turns.</summary>
    public DateTime LastSeenAtUtc => new[] { RegisteredAtUtc, LastBeatAtUtc ?? DateTime.MinValue, LastFailureAtUtc ?? DateTime.MinValue }.Max();

    /// <summary>
    /// Silent for three of its intervals, and at least ten minutes: its loop has stopped or hangs.
    /// A job that runs once a day, and so registers a day, is silent after three days.
    /// </summary>
    public bool IsSilent(DateTime nowUtc) =>
        nowUtc - LastSeenAtUtc > TimeSpan.FromTicks(Math.Max(Interval.Ticks * SilentAfterIntervals, SilentAtLeast.Ticks));

    /// <summary>Its last run threw, and none has finished well since.</summary>
    public bool IsFailing => ConsecutiveFailures > 0;
}
