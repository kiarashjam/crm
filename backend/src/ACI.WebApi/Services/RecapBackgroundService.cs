using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Sends the recap emails that are due (see <see cref="IRecapEmails"/>): daily, weekly and monthly,
/// from 07:00 in each workspace's time zone.
/// </summary>
/// <remarks>
/// Every quarter of an hour, so a recap arrives soon after seven rather than any time in the hour;
/// a pass that finds every recap taken reads a few small lists. After a restart the latest period
/// of each frequency is looked at again, and each person's recap, taken by its own row, still goes
/// once. The first pass waits a few minutes after start, so a deploy does not begin with a burst of
/// mail, and a test host stops before it runs.
/// </remarks>
public sealed class RecapBackgroundService : BackgroundService
{
    /// <summary>Its name in the job heartbeats (/health, the System health page).</summary>
    public const string JobName = "recaps";

    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<RecapBackgroundService> _logger;

    public RecapBackgroundService(IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, ILogger<RecapBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _heartbeats = heartbeats;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(JobName, Interval);
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
                await RunAsync(DateTime.UtcNow, stoppingToken);
                _heartbeats.Beat(JobName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _heartbeats.Failed(JobName, ex);
                _logger.LogError(ex, "Error sending the recap emails");
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

    /// <summary>One pass: every recap due at <paramref name="nowUtc"/>. Returns how many were queued.</summary>
    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sent = await scope.ServiceProvider.GetRequiredService<IRecapEmails>().SendDueAsync(nowUtc, ct);
        if (sent > 0) _logger.LogInformation("Queued {Count} recap emails", sent);
        return sent;
    }
}
