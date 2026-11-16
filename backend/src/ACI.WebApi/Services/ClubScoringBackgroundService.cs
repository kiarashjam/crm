using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// The daily at-risk scoring (see <see cref="IClubRetention.RunScheduledAsync"/>): for each
/// workspace whose club is on and that has active members, once a day after 05:00 in its time
/// zone (after the billing run has marked what is overdue), works out every active member's
/// score, runs the club's save actions for those who have just become at risk, and notes the
/// renewals coming up for the renewals board.
/// </summary>
/// <remarks>
/// <para>
/// Every instance runs this; each workspace's day is claimed with a row the database takes
/// once (one scoring run per workspace and day), and each crossing into at risk with one
/// conditional update, so two instances, or a run repeated after a crash, never make a second
/// task or send a second email.
/// </para>
/// <para>
/// It looks every hour, so a workspace's run goes within the hour after 05:00 its time. The
/// first look waits a few minutes after start, so a test host stops before it ever runs.
/// </para>
/// </remarks>
public sealed class ClubScoringBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(16);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ClubScoringBackgroundService> _logger;

    public ClubScoringBackgroundService(IServiceScopeFactory scopes, ILogger<ClubScoringBackgroundService> logger)
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
                var ran = await scope.ServiceProvider.GetRequiredService<IClubRetention>().RunScheduledAsync(DateTime.UtcNow, stoppingToken);
                if (ran > 0) _logger.LogInformation("Worked out the members' at-risk scores for {Count} workspace(s)", ran);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The at-risk scoring run failed");
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
