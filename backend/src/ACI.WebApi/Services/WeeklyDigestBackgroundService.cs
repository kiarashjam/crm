using ACI.Infrastructure.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Sends the weekly report digests that are due (see <see cref="WeeklyDigestService"/>).
/// </summary>
/// <remarks>
/// Once an hour is enough: a digest is due from 07:00 on a Monday in each person's
/// time zone, and the hour it arrives within does not matter. The first pass waits a
/// few minutes after start, so a deploy does not start with a burst of mail before
/// the app has settled, and a test host stops before it ever runs.
/// </remarks>
public sealed class WeeklyDigestBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyDigestBackgroundService> _logger;

    public WeeklyDigestBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WeeklyDigestBackgroundService> logger)
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
                var sent = await scope.ServiceProvider.GetRequiredService<WeeklyDigestService>().SendDueAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0) _logger.LogInformation("Sent {Count} weekly report digests", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending weekly report digests");
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
