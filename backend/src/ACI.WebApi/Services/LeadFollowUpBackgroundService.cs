using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Runs due outreach steps, notifies owners when a new lead misses its first
/// response, and keeps automatic lead scores current.
/// </summary>
public sealed class LeadFollowUpBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    /// <summary>Time-based score rules count in days, so a few runs a day is plenty.</summary>
    private static readonly TimeSpan RescoreInterval = TimeSpan.FromHours(6);

    /// <summary>Its name in the job heartbeats (/health, the System health page).</summary>
    public const string JobName = "lead-follow-up";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<LeadFollowUpBackgroundService> _logger;
    private DateTime _lastRescoreUtc = DateTime.MinValue;

    public LeadFollowUpBackgroundService(IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, ILogger<LeadFollowUpBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _heartbeats = heartbeats;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(JobName, Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISequenceService>().RunDueAsync(stoppingToken);
                var ops = scope.ServiceProvider.GetRequiredService<ILeadOpsService>();
                await ops.NotifyOverdueAsync(stoppingToken);

                // The first run is at startup, which is also what picks up the scores
                // the one-time unfreeze (DataFixes) handed back to automatic.
                if (DateTime.UtcNow - _lastRescoreUtc >= RescoreInterval)
                {
                    _lastRescoreUtc = DateTime.UtcNow;
                    var moved = await ops.RescoreOpenLeadsAsync(stoppingToken);
                    if (moved > 0) _logger.LogInformation("Rescored {Count} leads", moved);
                }
                _heartbeats.Beat(JobName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _heartbeats.Failed(JobName, ex);
                _logger.LogError(ex, "Lead follow-up run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
