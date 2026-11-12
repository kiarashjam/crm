using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// The daily retention run (see <see cref="IRetention.RunScheduledAsync"/>): for each workspace
/// that set a retention period, once a day after 04:00 its time, removes what its settings say
/// it no longer keeps.
/// </summary>
/// <remarks>
/// Every instance runs this; each workspace's day is claimed with a row the database takes
/// once (one run per workspace and day), so two instances never run one day twice. It looks
/// every hour; the first look waits a few minutes after start, so a test host stops before it
/// ever runs. Nothing happens for a workspace that set no period, which is every workspace
/// until someone does.
/// </remarks>
public sealed class RetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(14);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RetentionBackgroundService> _logger;

    public RetentionBackgroundService(IServiceScopeFactory scopes, ILogger<RetentionBackgroundService> logger)
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
                // A scope per pass: each workspace's run acts for that workspace alone, and
                // nothing one pass tracked is carried into the next.
                using var scope = _scopes.CreateScope();
                var ran = await scope.ServiceProvider.GetRequiredService<IRetention>().RunScheduledAsync(DateTime.UtcNow, stoppingToken);
                if (ran > 0) _logger.LogInformation("Ran the retention settings for {Count} workspace(s)", ran);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The retention run failed");
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
