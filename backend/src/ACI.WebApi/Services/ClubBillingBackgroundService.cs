using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// The club's daily billing run (see <see cref="IClubBilling.RunScheduledAsync"/>): for each
/// workspace whose settings switch something on, once a day after 03:00 in its time zone,
/// bills the subscriptions whose period has started, sets dues past due, sends payment
/// reminders, lapses and reinstates members, and sends the monthly statements.
/// </summary>
/// <remarks>
/// <para>
/// Dues used to be billed only when someone pressed a button, and nothing ever marked a
/// charge overdue, chased it or lapsed anyone. Every instance runs this; each workspace's
/// day is claimed with a row the database takes once (one daily run per workspace and
/// day), and everything a run does once is keyed the same way, so two instances, or a run
/// repeated after a crash, never bill, remind or send anything twice.
/// </para>
/// <para>
/// It looks every hour, so a workspace's run goes within the hour after 03:00 its time. The
/// first look waits a few minutes after start, so a test host stops before it ever runs.
/// </para>
/// </remarks>
public sealed class ClubBillingBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ClubBillingBackgroundService> _logger;

    public ClubBillingBackgroundService(IServiceScopeFactory scopes, ILogger<ClubBillingBackgroundService> logger)
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
                var ran = await scope.ServiceProvider.GetRequiredService<IClubBilling>().RunScheduledAsync(DateTime.UtcNow, stoppingToken);
                if (ran > 0) _logger.LogInformation("Ran the club billing for {Count} workspace(s)", ran);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The club billing run failed");
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
