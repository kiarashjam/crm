using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Services;

/// <summary>
/// Once at start-up and then once a day, asks every workspace's portal whether it still takes
/// handoffs: the same signed <c>onboarding.ping</c> as Welcome &amp; portal's "Test the
/// connection", so a portal that moved, or a secret changed on one side only, is found before
/// the next new customer is handed over to it.
/// </summary>
/// <remarks>
/// Only workspaces whose portal is set up (an address and a secret) with the Sales module on,
/// where handoffs are made. Each answer is kept in <see cref="IPortalPings"/> for the System
/// health page and the portal.ping.failed alert. The ping is made as the workspace's owner,
/// who may test the connection; it changes nothing.
/// </remarks>
public sealed class PortalPingBackgroundService : BackgroundService
{
    /// <summary>Its name in the heartbeats.</summary>
    public const string Job = "portal-ping";

    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>A moment after start, so the app has settled; a test host stops before.</summary>
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHeartbeats _heartbeats;
    private readonly IPortalPings _pings;
    private readonly ILogger<PortalPingBackgroundService> _logger;

    public PortalPingBackgroundService(IServiceScopeFactory scopeFactory, IJobHeartbeats heartbeats, IPortalPings pings, ILogger<PortalPingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _heartbeats = heartbeats;
        _pings = pings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(Job, Interval);
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
                var failed = await PingAllAsync(stoppingToken);
                if (failed > 0) _logger.LogWarning("{Count} workspace portals did not take the daily ping", failed);
                _heartbeats.Beat(Job);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _heartbeats.Failed(Job, ex);
                _logger.LogError(ex, "Error pinging the workspaces' portals");
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

    /// <summary>One pass: pings every portal that is set up, and keeps each answer. Returns how many did not take it. Public for tests.</summary>
    public async Task<int> PingAllAsync(CancellationToken ct)
    {
        List<(Guid Organization, Guid Owner)> portals;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Across workspaces: whose portal is set up. Each ping then acts for its own.
            portals = (await (
                    from s in db.OnboardingSettings.IgnoreQueryFilters().AsNoTracking()
                    where s.WebhookUrl != null && s.WebhookUrl != "" && s.WebhookSecret != null && s.WebhookSecret != ""
                    join o in db.Organizations.AsNoTracking() on s.OrganizationId equals o.Id
                    where !o.SalesModuleOff
                    select new { s.OrganizationId, o.OwnerUserId })
                .ToListAsync(ct))
                .Select(p => (p.OrganizationId, p.OwnerUserId))
                .ToList();
        }

        // A portal no longer set up has no answer to show.
        var current = portals.Select(p => p.Organization).ToHashSet();
        foreach (var gone in _pings.Snapshot().Keys.Where(k => !current.Contains(k))) _pings.Forget(gone);

        var failed = 0;
        foreach (var (organization, owner) in portals)
        {
            var result = await PingAsync(organization, owner, ct);
            _pings.Record(organization, result);
            if (!result.Ok) failed++;
        }
        return failed;
    }

    private async Task<PortalPingResult> PingAsync(Guid organizationId, Guid ownerId, CancellationToken ct)
    {
        var at = DateTime.UtcNow;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var acting = scope.ServiceProvider.GetRequiredService<ITenantContext>().ActFor(organizationId, ownerId);
            var answer = await scope.ServiceProvider.GetRequiredService<IOnboardingService>().PingAsync(organizationId, ownerId, ct);
            return answer.IsSuccess
                ? new PortalPingResult(answer.Value.Ok, at, answer.Value.StatusCode, answer.Value.Ok ? null : answer.Value.Error ?? "The portal did not take it.")
                : new PortalPingResult(false, at, null, answer.Error.Description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // One portal that cannot be asked never stops the others being asked.
            _logger.LogWarning(ex, "The portal of workspace {OrganizationId} could not be pinged", organizationId);
            return new PortalPingResult(false, at, null, JobHeartbeats.OneLine(ex));
        }
    }
}
