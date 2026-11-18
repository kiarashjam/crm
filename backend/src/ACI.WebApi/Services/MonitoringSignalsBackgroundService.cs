using System.Globalization;
using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Every five minutes, the signals the Azure alert rules watch, across every workspace: one
/// structured log event for each that is on, which Application Insights stores and the log
/// alerts query.
/// </summary>
/// <remarks>
/// <para>
/// The format is what the alert rules match, so it stays as it is:
/// <c>monitoring.alert {Signal} {Count} {Detail}</c> at Warning (Error for job.silent and
/// payments.failed), with Signal one of <see cref="Signals"/>, Count an int, and Detail a short
/// text of counts, job names and ids only, never a person's name or address. A signal that stays
/// on is logged again after an hour, not every five minutes; when it clears,
/// <c>monitoring.resolved {Signal}</c> is logged once at Information.
/// </para>
/// <para>
/// outbox.failed and payments.failed are about what failed since the previous pass. The first
/// pass after a start only learns what has failed already, so a deploy does not raise the last
/// three days' failures again; they are on the System health page and in the daily digest.
/// What it remembers is this instance's: two instances each raise an alert.
/// </para>
/// </remarks>
public sealed class MonitoringSignalsBackgroundService : BackgroundService
{
    /// <summary>Its name in the heartbeats.</summary>
    public const string Job = "monitoring-signals";

    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>How long a signal that stays on waits before it is logged again.</summary>
    public static readonly TimeSpan RepeatAfter = TimeSpan.FromMinutes(60);

    public const string JobSilent = "job.silent";
    public const string JobFailing = "job.failing";
    public const string OutboxBacklog = "outbox.backlog";
    public const string OutboxFailed = "outbox.failed";
    public const string HandoffUndelivered = "handoff.undelivered";
    public const string PaymentsFailed = "payments.failed";
    public const string PortalPingFailed = "portal.ping.failed";

    /// <summary>Every signal, in the order they are evaluated.</summary>
    public static readonly IReadOnlyList<string> Signals =
        [JobSilent, JobFailing, OutboxBacklog, OutboxFailed, HandoffUndelivered, PaymentsFailed, PortalPingFailed];

    public const string AlertTemplate = "monitoring.alert {Signal} {Count} {Detail}";
    public const string ResolvedTemplate = "monitoring.resolved {Signal}";

    /// <summary>The first pass waits a little, so jobs started with the app have registered.</summary>
    private static readonly TimeSpan FirstPass = TimeSpan.FromSeconds(30);
    private const int MaxDetail = 300;
    private const int IdsShown = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly IPortalPings _pings;
    private readonly ILogger<MonitoringSignalsBackgroundService> _logger;

    /// <summary>Each signal that is on, and when it was last logged.</summary>
    private readonly Dictionary<string, DateTime> _on = new(StringComparer.Ordinal);
    private HashSet<Guid>? _failedMessages;
    private HashSet<Guid>? _failedStripeEvents;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MonitoringSignalsBackgroundService(
        IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, IPortalPings pings, ILogger<MonitoringSignalsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _heartbeats = heartbeats;
        _pings = pings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(Job, Interval);
        try
        {
            await Task.Delay(FirstPass, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(DateTime.UtcNow, stoppingToken);
                _heartbeats.Beat(Job);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _heartbeats.Failed(Job, ex);
                _logger.LogError(ex, "Error evaluating the monitoring signals");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One pass: reads every signal and logs what changed. Public for tests.</summary>
    public async Task EvaluateAsync(DateTime nowUtc, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            MonitoringReadings readings;
            using (var scope = _scopeFactory.CreateScope())
                readings = await scope.ServiceProvider.GetRequiredService<IMonitoringReadings>().ReadAsync(nowUtc, ct);

            var jobs = _heartbeats.Snapshot();
            var silent = jobs.Where(j => j.IsSilent(nowUtc)).ToList();
            var failing = jobs.Where(j => j.IsFailing).ToList();

            var newMessages = New(readings.FailedMessageIds, ref _failedMessages);
            var newStripe = New(readings.FailedStripeEventIds, ref _failedStripeEvents);
            var portals = _pings.Snapshot().Where(p => !p.Value.Ok).Select(p => p.Key).OrderBy(k => k).ToList();

            Apply(JobSilent, LogLevel.Error, silent.Count, nowUtc, () =>
                $"{silent.Count} silent: {string.Join(", ", silent.Select(j => j.Job))}");
            Apply(JobFailing, LogLevel.Warning, failing.Count, nowUtc, () =>
                $"{failing.Count} failing: {string.Join(", ", failing.Select(j => $"{j.Job} ({j.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture)} in a row)"))}");
            Apply(OutboxBacklog, LogLevel.Warning, readings.LateEmails, nowUtc, () =>
                $"{readings.LateEmails} emails queued over 30 min; the oldest for {readings.OldestLateEmailMinutes} min");
            Apply(OutboxFailed, LogLevel.Warning, newMessages.Count, nowUtc, () =>
                $"{newMessages.Count} newly failed: {string.Join(", ", newMessages.GroupBy(m => m.Value).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"))}; ids {Ids(newMessages.Select(m => m.Key.ToString()))}");
            Apply(HandoffUndelivered, LogLevel.Warning, readings.LateHandoffs, nowUtc, () =>
                $"{readings.LateHandoffs} handoffs not taken by the portal after 1 h, in {readings.LateHandoffWorkspaces} workspaces");
            Apply(PaymentsFailed, LogLevel.Error, newStripe.Count, nowUtc, () =>
                $"{newStripe.Count} newly failed Stripe events: {Ids(newStripe.Select(e => e.Value))}");
            Apply(PortalPingFailed, LogLevel.Warning, portals.Count, nowUtc, () =>
                $"{portals.Count} workspace portals did not take the last ping: workspaces {Ids(portals.Select(p => p.ToString()))}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>What is in <paramref name="now"/> and was not in the last pass's; nothing on the first pass, which only learns.</summary>
    private static List<KeyValuePair<Guid, string>> New(IReadOnlyDictionary<Guid, string> now, ref HashSet<Guid>? known)
    {
        var before = known;
        var fresh = before == null ? [] : now.Where(kv => !before.Contains(kv.Key)).ToList();
        known = now.Keys.ToHashSet();
        return fresh;
    }

    private void Apply(string signal, LogLevel level, int count, DateTime nowUtc, Func<string> detail)
    {
        if (count > 0)
        {
            if (_on.TryGetValue(signal, out var logged) && nowUtc - logged < RepeatAfter) return;
            _on[signal] = nowUtc;
            var text = detail();
            if (text.Length > MaxDetail) text = text[..(MaxDetail - 3)] + "...";
            _logger.Log(level, AlertTemplate, signal, count, text);
            return;
        }
        if (_on.Remove(signal)) _logger.LogInformation(ResolvedTemplate, signal);
    }

    private static string Ids(IEnumerable<string> ids)
    {
        var all = ids.ToList();
        var shown = string.Join(", ", all.Take(IdsShown));
        return all.Count > IdsShown ? $"{shown} and {all.Count - IdsShown} more" : shown;
    }
}
