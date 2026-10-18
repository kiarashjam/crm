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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadFollowUpBackgroundService> _logger;
    private DateTime _lastRescoreUtc = DateTime.MinValue;

    public LeadFollowUpBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LeadFollowUpBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lead follow-up run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
