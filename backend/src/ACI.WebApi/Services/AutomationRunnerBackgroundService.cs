using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;

namespace ACI.WebApi.Services;

/// <summary>Wakes the automation runner on this instance (see <see cref="IAutomationSignal"/>).</summary>
public sealed class AutomationSignal : IAutomationSignal
{
    // At most one wake-up waiting: two runs made due at once need one pass, not two.
    private readonly SemaphoreSlim _wake = new(0, 1);

    public void Notify()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already waiting.
        }
    }

    /// <summary>Waits until a run is made due now, or <paramref name="timeout"/> passes.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _wake.WaitAsync(timeout, ct);
}

/// <summary>
/// Carries automation runs on: the ones whose wait is over, whose email's send window has
/// opened, or whose failed step is due to be tried again; and sets off the renewal rules.
/// </summary>
/// <remarks>
/// Each run is carried on in a scope of its own, acting for its own workspace, so one that
/// fails cannot leave something behind in the next one's unit of work. The claim inside
/// <see cref="IAutomationEngine.ResumeAsync"/> makes it safe for every instance to run this at
/// once, and for one pass to carry on several runs at once.
/// </remarks>
public sealed class AutomationRunner
{
    public const int BatchSize = 50;

    /// <summary>Runs one instance carries on at the same time.</summary>
    public const int Concurrency = 4;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AutomationRunner> _logger;
    private readonly TimeProvider _clock;

    public AutomationRunner(IServiceScopeFactory scopes, ILogger<AutomationRunner> logger, TimeProvider? clock = null)
    {
        _scopes = scopes;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>One pass. Returns how many runs were due, carried on or not.</summary>
    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DueAutomationRun> due;
        using (var scope = _scopes.CreateScope())
            due = await scope.ServiceProvider.GetRequiredService<IAutomationRunStore>().DueAsync(_clock.GetUtcNow().UtcDateTime, BatchSize, ct);

        await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = ct }, async (run, token) =>
        {
            using var scope = _scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IAutomationEngine>().ResumeAsync(run, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Its claim runs out and it is carried on by a later pass.
                _logger.LogError(ex, "Automation run {RunId} could not be carried on", run.Id);
            }
        });
        return due.Count;
    }

    /// <summary>
    /// Sets off the renewal rules for the running subscriptions whose period ends within the
    /// days a rule says, in each workspace with such a rule on. A rule fires once for each
    /// subscription and period, however often this looks.
    /// </summary>
    /// <returns>How many subscriptions coming up for renewal were looked at.</returns>
    public async Task<int> ScanRenewalsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Guid> workspaces;
        using (var scope = _scopes.CreateScope())
            workspaces = await scope.ServiceProvider.GetRequiredService<IAutomationRunStore>().WorkspacesWithRulesAsync(AutomationCatalog.RenewalDue, ct);

        var seen = 0;
        foreach (var organizationId in workspaces)
        {
            // A scope per workspace: its rules run acting for it alone.
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                // A club turned off sets nothing off: its pages, where a rule's work is seen, are gone.
                if (!await services.GetRequiredService<IWorkspaceModules>().IsOnAsync(organizationId, WorkspaceModules.Club, ct)) continue;
                var reader = services.GetRequiredService<IAutomationRecordReader>();
                // The system sets these rules off, so they act as the workspace's owner, as a lead
                // from the website does.
                if (await reader.WorkspaceOwnerAsync(organizationId, ct) is not Guid owner) continue;
                using var acting = services.GetRequiredService<ITenantContext>().ActFor(organizationId, owner);

                var rules = await services.GetRequiredService<IAutomationRepository>().EnabledForTriggerAsync(organizationId, AutomationCatalog.RenewalDue, ct);
                if (rules.Count == 0) continue;
                var days = rules.Max(r => AutomationCatalog.DaysBefore(AutomationEngine.TriggerConfig(r)));
                var now = _clock.GetUtcNow().UtcDateTime;
                var engine = services.GetRequiredService<IAutomationEngine>();
                foreach (var renewal in await reader.RenewalsDueAsync(now, now.AddDays(days), ct))
                {
                    seen++;
                    await engine.DispatchAsync(AutomationCatalog.RenewalDue, new AutomationEvent(
                        organizationId, owner,
                        // Once for each period: the next period of the same subscription is another key.
                        $"{renewal.SubscriptionId}:{renewal.PeriodEndsAtUtc:yyyy-MM-dd}",
                        renewal.MemberName,
                        MemberId: renewal.MemberId,
                        RecordId: renewal.SubscriptionId,
                        DueAtUtc: renewal.PeriodEndsAtUtc), ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Renewal automations could not be set off for workspace {OrganizationId}", organizationId);
            }
        }
        return seen;
    }
}

/// <summary>Runs <see cref="AutomationRunner"/>: every half minute, at once when woken, and the renewal scan hourly.</summary>
/// <remarks>Needs App Service Always On (already provisioned), like the other jobs.</remarks>
public sealed class AutomationRunnerBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenewalScanEvery = TimeSpan.FromHours(1);

    /// <summary>Full batches worked through in one pass before waiting again, so a backlog clears but never spins.</summary>
    private const int MaxBatchesPerPass = 20;

    private readonly AutomationRunner _runner;
    private readonly AutomationSignal _signal;
    private readonly ILogger<AutomationRunnerBackgroundService> _logger;

    public AutomationRunnerBackgroundService(AutomationRunner runner, AutomationSignal signal, ILogger<AutomationRunnerBackgroundService> logger)
    {
        _runner = runner;
        _signal = signal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _signal.WaitAsync(FirstPass, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var lastScan = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var batch = 0; batch < MaxBatchesPerPass; batch++)
                {
                    if (await _runner.RunDueAsync(stoppingToken) < AutomationRunner.BatchSize) break;
                }
                if (DateTime.UtcNow - lastScan >= RenewalScanEvery)
                {
                    lastScan = DateTime.UtcNow;
                    await _runner.ScanRenewalsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automation runner pass failed");
            }

            try
            {
                await _signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
