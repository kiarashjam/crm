using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Writes off what is left on gift cards past their expiry, as an Expiry transaction on
/// each card dated at the expiry (see <see cref="IClubLedger.ExpireGiftCardsAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// A card past its expiry already reads as Expired and cannot be redeemed: the clock says
/// so on every read. What this adds is the entry, so the card's value at any date after
/// its expiry is nothing, and the club's outstanding gift card value drops on the day the
/// card expired rather than whenever someone next looks.
/// </para>
/// <para>
/// It works across every workspace, as the ledger's sweep does. Two instances running it
/// at once write each card's expiry once: the card's version and a unique index on its
/// Expiry transaction let only one save land. Hourly is plenty; the first pass waits a few
/// minutes after start, so a test host stops before it ever runs.
/// </para>
/// </remarks>
public sealed class GiftCardExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GiftCardExpiryBackgroundService> _logger;

    public GiftCardExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<GiftCardExpiryBackgroundService> logger)
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
                var expired = await scope.ServiceProvider.GetRequiredService<IClubLedger>().ExpireGiftCardsAsync(DateTime.UtcNow, stoppingToken);
                if (expired > 0) _logger.LogInformation("Wrote off {Count} expired gift cards", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing off expired gift cards");
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
