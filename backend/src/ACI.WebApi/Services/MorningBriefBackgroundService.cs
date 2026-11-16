using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Sends the morning briefs that are due (see <see cref="IMorningBrief"/>).
/// </summary>
/// <remarks>
/// Every quarter of an hour, so a brief arrives soon after 07:00 on the workspace's calendar
/// rather than any time in the hour; a pass that finds nobody due reads a few small lists. The
/// first pass waits a few minutes after start, so a deploy does not begin with a burst of
/// notifications and email before the app has settled, and a test host stops before it runs.
/// </remarks>
public sealed class MorningBriefBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MorningBriefBackgroundService> _logger;

    public MorningBriefBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MorningBriefBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
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
                using var scope = _scopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IMorningBrief>().SendDueAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0) _logger.LogInformation("Sent {Count} morning briefs", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending morning briefs");
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
