using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>
/// Imports the POS's closed tickets (see <see cref="IClubPos.ImportDueAsync"/>) in every
/// workspace that turned the import on, every few minutes.
/// </summary>
/// <remarks>
/// <para>
/// Lightspeed K-Series has no webhook that reports every closed ticket, so the club's tickets
/// are read from its Financial API. Each workspace's run claims the workspace first (a lease on its POS
/// settings, taken by one conditional update), so two instances of the app, or this job and
/// someone pressing "Import now", never import the same workspace at once; and each ticket is
/// saved once by the POS's id for it, so reading one again adds nothing.
/// </para>
/// <para>
/// The first look waits a few minutes after start, so a test host stops before it ever runs;
/// with no workspace connected, a look is one small query.
/// </para>
/// </remarks>
public sealed class ClubPosBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Its name in the job heartbeats (/health, the System health page).</summary>
    public const string JobName = "club-pos";

    private readonly IServiceScopeFactory _scopes;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<ClubPosBackgroundService> _logger;

    public ClubPosBackgroundService(IServiceScopeFactory scopes, IJobHeartbeats heartbeats, ILogger<ClubPosBackgroundService> logger)
    {
        _scopes = scopes;
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
                using var scope = _scopes.CreateScope();
                var imported = await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow, stoppingToken);
                if (imported > 0) _logger.LogInformation("Imported {Count} closed ticket(s) from the POS", imported);
                _heartbeats.Beat(JobName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _heartbeats.Failed(JobName, ex);
                _logger.LogError(ex, "Importing tickets from the POS failed");
            }

            try
            {
                // Each workspace is imported every few minutes (ClubPosService.Every); this only
                // looks for the ones that are due.
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
