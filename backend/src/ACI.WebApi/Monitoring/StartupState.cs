namespace ACI.WebApi.Monitoring;

/// <summary>
/// Whether this instance's start-up work on the database (creating it in development, the
/// migrations, the start-up DDL and the seeding) finished. Program.cs marks it failed where it
/// keeps the error for /db-status; the "startup" health check reads it.
/// </summary>
/// <remarks>
/// The app starts either way, so that /db-status and the log can say what went wrong, which
/// left /health answering 200 over a database nobody had set up. Only the fact and the time
/// are kept here: the error itself names the server and the login, and is in the log.
/// </remarks>
public sealed class StartupState
{
    private long _databaseFailedAtTicks;

    /// <summary>When start-up database initialisation failed; null while it has not.</summary>
    public DateTime? DatabaseFailedAtUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _databaseFailedAtTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>Whether start-up database initialisation failed.</summary>
    public bool DatabaseFailed => DatabaseFailedAtUtc != null;

    /// <summary>Start-up database initialisation threw; the error is logged where it was caught.</summary>
    public void MarkDatabaseFailed(DateTime atUtc) => Interlocked.Exchange(ref _databaseFailedAtTicks, atUtc.Ticks);
}
