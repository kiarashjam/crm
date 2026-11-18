using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure.Services.Monitoring;

namespace ACI.WebApi.Services;

/// <summary>
/// Sends the daily System health digests (see <see cref="IHealthDigest"/>) from 08:00 Europe/Zurich.
/// </summary>
/// <remarks>
/// Every quarter of an hour, so it goes soon after eight, and later the same day when the
/// server was down at eight. Once a pass has done the day, the passes after it skip it; after a
/// restart the day is looked at again and each person's digest, claimed by its notification,
/// still goes once (as the morning brief does). The first pass waits a few minutes after start,
/// so a deploy does not begin with a burst of mail, and a test host stops before it runs.
/// </remarks>
public sealed class HealthDigestBackgroundService : BackgroundService
{
    /// <summary>Its name in the heartbeats: a daily job, though its loop turns every quarter of an hour.</summary>
    public const string Job = "health-digest";

    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(7);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<HealthDigestBackgroundService> _logger;
    private DateOnly? _done;

    public HealthDigestBackgroundService(IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, ILogger<HealthDigestBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _heartbeats = heartbeats;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(Job, HeartbeatInterval);
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
                _heartbeats.Beat(Job);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _heartbeats.Failed(Job, ex);
                _logger.LogError(ex, "Error sending the System health digests");
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

    /// <summary>One pass: the day's digests, unless this instance has done the day already. Returns how many workspaces' went.</summary>
    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (HealthDigestService.WindowFor(nowUtc) is not { } window || _done == window.Day) return 0;
        using var scope = _scopeFactory.CreateScope();
        var sent = await scope.ServiceProvider.GetRequiredService<IHealthDigest>().SendDueAsync(nowUtc, ct);
        if (sent > 0) _logger.LogInformation("Sent the System health digest for {Count} workspaces", sent);
        _done = window.Day;
        return sent;
    }
}
