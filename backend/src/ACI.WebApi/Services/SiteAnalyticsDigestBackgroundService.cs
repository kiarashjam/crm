using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// The site analytics' daily work: yesterday's email to the platform admins from 07:30
/// Europe/Zurich (see <see cref="ISiteAnalyticsDigest"/>), and, once a day, deleting the events
/// older than 400 days (<see cref="ISiteAnalytics.PurgeAsync"/>).
/// </summary>
/// <remarks>
/// Every quarter of an hour, so the email goes soon after half past seven, and later the same
/// morning when the server was down then. The day's email is claimed by a row the database takes
/// once, so a restart or a second server never sends it twice. The clean-up runs on the first
/// pass of each Zurich day; deleting what is already gone deletes nothing, so a second server
/// doing it too costs one statement. The first pass waits a few minutes after start, so a deploy
/// does not begin with it, and a test host stops before it runs.
/// </remarks>
public sealed class SiteAnalyticsDigestBackgroundService : BackgroundService
{
    /// <summary>Its name in the heartbeats: a daily job, though its loop turns every quarter of an hour.</summary>
    public const string Job = "site-analytics-digest";

    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<SiteAnalyticsDigestBackgroundService> _logger;
    private DateOnly? _purged;

    public SiteAnalyticsDigestBackgroundService(IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, ILogger<SiteAnalyticsDigestBackgroundService> logger)
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
                _logger.LogError(ex, "Error in the site analytics' daily work");
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

    /// <summary>One pass: the day's clean-up if this instance has not done it, then the email if it is due. Returns how many admins it was queued for.</summary>
    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var today = SiteAnalyticsRules.DayOf(nowUtc);
        if (_purged != today)
        {
            var deleted = await scope.ServiceProvider.GetRequiredService<ISiteAnalytics>().PurgeAsync(nowUtc, ct);
            if (deleted > 0) _logger.LogInformation("Deleted {Count} site analytics events older than {Days} days", deleted, SiteAnalyticsRules.RetentionDays);
            _purged = today;
        }
        var sent = await scope.ServiceProvider.GetRequiredService<ISiteAnalyticsDigest>().SendDueAsync(nowUtc, ct);
        if (sent > 0) _logger.LogInformation("Queued the site analytics email for {Count} platform admin(s)", sent);
        return sent;
    }
}
