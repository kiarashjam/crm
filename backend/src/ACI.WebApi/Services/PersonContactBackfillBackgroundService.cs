using ACI.Application.Interfaces;

namespace ACI.WebApi.Services;

/// <summary>
/// Gives every lead with a name and an email the person contact it should have, when it
/// has none: leads from before every lead got one, and any a tool wrote to the database
/// directly. A lead saved through the API gets its contact as it is saved.
/// </summary>
/// <remarks>
/// This used to run inside the Contacts and Leads list requests, so opening a list wrote
/// to the database (a Viewer's too, attributed to whoever looked) and scanned the leads
/// against every contact on each page and search keystroke. A lead whose contact someone
/// deleted is skipped (see Lead.PersonContactRemovedAtUtc). The first pass waits a few
/// minutes after start, so a deploy settles first and a test host stops before it runs.
/// </remarks>
public sealed class PersonContactBackfillBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    /// <summary>Leads looked at in one run, so one run is a bounded piece of work.</summary>
    public const int LeadsPerRun = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersonContactBackfillBackgroundService> _logger;

    public PersonContactBackfillBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PersonContactBackfillBackgroundService> logger)
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
                // A full run means more may be waiting (a database from before this job),
                // so the next one follows straight away rather than half an hour later.
                int linked;
                do
                {
                    linked = await RunOnceAsync(stoppingToken);
                    if (linked > 0) _logger.LogInformation("Gave {Count} leads their person contact", linked);
                } while (linked == LeadsPerRun && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error giving leads their person contacts");
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

    /// <summary>One run, in a scope of its own. Public for tests. Returns how many leads were given a contact.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IContactRepository>().BackfillPersonContactsAsync(LeadsPerRun, ct);
    }
}
