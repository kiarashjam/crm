using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Sends club campaigns at the time set for them, and finishes any whose sending stopped
/// part-way (see <see cref="CampaignService.RunDueAsync"/>).
/// </summary>
/// <remarks>
/// Nothing acted on a campaign's planned time: it stayed "Scheduled" for ever. Every
/// instance runs this, and each campaign is claimed with one conditional update before it
/// is sent, so two instances (or a pass that overlaps the press of Send) send it once. It
/// looks every half minute; the emails themselves go out through the outbox's dispatcher.
/// </remarks>
public sealed class CampaignSchedulerBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CampaignSchedulerBackgroundService> _logger;

    public CampaignSchedulerBackgroundService(IServiceScopeFactory scopes, ILogger<CampaignSchedulerBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                // A scope per pass: each campaign is sent acting for its own workspace, and
                // nothing one pass tracked is carried into the next.
                using var scope = _scopes.CreateScope();
                var taken = await scope.ServiceProvider.GetRequiredService<ICampaignService>().RunDueAsync(DateTime.UtcNow, stoppingToken);
                if (taken > 0) _logger.LogInformation("Took up {Count} campaign(s) to send", taken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sending due campaigns failed");
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
}
