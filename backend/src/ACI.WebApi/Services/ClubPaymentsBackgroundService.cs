using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Charges members' dues to the card they saved for it (see
/// <see cref="IClubPayments.CollectAutoPayAsync"/>), in every workspace with Stripe set up.
/// </summary>
/// <remarks>
/// <para>
/// A member who agreed to have their dues charged to their card used to be a label ("Card on
/// file") and a reminder to call them. Each dues charge billed since they agreed is charged
/// once: the attempt is claimed with a key the database holds once per charge before Stripe
/// is asked, with that claim as Stripe's Idempotency-Key, so two instances, or a pass after a
/// crash, never charge a card twice. A charge that is declined is not tried again on its own;
/// staff see it among the failed online payments.
/// </para>
/// <para>
/// It looks every hour, after the daily billing run has posted the dues; the first look waits a
/// few minutes after start, so a test host stops before it ever runs.
/// </para>
/// </remarks>
public sealed class ClubPaymentsBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ClubPaymentsBackgroundService> _logger;

    public ClubPaymentsBackgroundService(IServiceScopeFactory scopes, ILogger<ClubPaymentsBackgroundService> logger)
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
                using var scope = _scopes.CreateScope();
                var tried = await scope.ServiceProvider.GetRequiredService<IClubPayments>().CollectAutoPayAsync(DateTime.UtcNow, stoppingToken);
                if (tried > 0) _logger.LogInformation("Charged dues to {Count} saved card(s)", tried);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Charging dues to saved cards failed");
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
