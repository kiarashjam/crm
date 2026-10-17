using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Runs due outreach steps and notifies owners when a new lead misses its first response.
/// </summary>
public sealed class LeadFollowUpBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadFollowUpBackgroundService> _logger;

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
                await scope.ServiceProvider.GetRequiredService<ILeadOpsService>().NotifyOverdueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lead follow-up run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
