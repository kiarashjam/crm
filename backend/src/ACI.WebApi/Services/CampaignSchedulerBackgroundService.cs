using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>Wakes the campaign scheduler on this instance when a campaign has just been sent (see <see cref="ICampaignSignal"/>).</summary>
public sealed class CampaignSignal : ICampaignSignal
{
    // At most one wake-up waiting: two campaigns sent at once need one pass, not two.
    private readonly SemaphoreSlim _wake = new(0, 1);

    public void Notify()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already waiting.
        }
    }

    /// <summary>Waits until a campaign is sent, or <paramref name="timeout"/> passes.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _wake.WaitAsync(timeout, ct);
}

/// <summary>
/// Sends club campaigns: at the time set for them, as soon as someone presses Send, and any
/// whose sending stopped part-way (see <see cref="CampaignService.RunDueAsync"/>).
/// </summary>
/// <remarks>
/// Nothing acted on a campaign's planned time: it stayed "Scheduled" for ever. Every
/// instance runs this, and each campaign is claimed with one conditional update before it
/// is sent, so two instances (or a pass that overlaps the press of Send) send it once. It
/// looks every half minute, and at once when Send is pressed on this instance (Send claims
/// the campaign and leaves the sending to this job, so the request answers at once); the
/// emails themselves go out through the outbox's dispatcher.
/// </remarks>
public sealed class CampaignSchedulerBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly CampaignSignal _signal;
    private readonly ILogger<CampaignSchedulerBackgroundService> _logger;

    public CampaignSchedulerBackgroundService(IServiceScopeFactory scopes, CampaignSignal signal, ILogger<CampaignSchedulerBackgroundService> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _signal.WaitAsync(FirstPass, stoppingToken);
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
                await _signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
