using ACI.Application.Interfaces;
using ACI.Application.Services;

namespace ACI.WebApi.Services;

/// <summary>Wakes this instance's dispatcher; see <see cref="IOutboxSignal"/>.</summary>
public sealed class OutboxSignal : IOutboxSignal
{
    // At most one wake-up waiting: ten messages queued at once need one pass, not ten.
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

    /// <summary>Waits until something is queued, or <paramref name="timeout"/> passes.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => _wake.WaitAsync(timeout, ct);
}

/// <summary>
/// Sends what is due in the outbox: one pass over up to <see cref="BatchSize"/> messages.
/// </summary>
/// <remarks>
/// <para>
/// Each message is attempted in a scope of its own, so one that fails — a database
/// error halfway through, say — cannot leave something behind in the next one's
/// unit of work. The claim inside <see cref="IOutbox.ProcessAsync"/> makes it safe for
/// every instance to run this at once, and for one pass to attempt several at once.
/// </para>
/// <para>
/// Up to <see cref="Concurrency"/> go at a time. One outbox sends for every workspace,
/// and an attempt can wait a long time: ten seconds for a webhook receiver that does not
/// answer, thirty for a mail server that does not. Taken one at a time, a single
/// workspace whose receiver was down held every other workspace's email behind its
/// events. Delivery is at least once and in no promised order, as before.
/// </para>
/// </remarks>
public sealed class OutboxDispatcher
{
    public const int BatchSize = 50;

    /// <summary>Messages one instance attempts at the same time.</summary>
    public const int Concurrency = 8;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>One pass. Returns how many messages were due, sent or not.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DueOutboundMessage> due;
        using (var scope = _scopes.CreateScope())
            due = await scope.ServiceProvider.GetRequiredService<IOutboxRepository>().DueAsync(DateTime.UtcNow, BatchSize, ct);

        await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = ct }, async (message, token) =>
        {
            using var scope = _scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IOutbox>().ProcessAsync(message, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox message {MessageId} failed", message.Id);
            }
        });
        return due.Count;
    }
}

/// <summary>
/// Runs the outbox: sends queued email and webhooks, and tries again later what did not go.
/// </summary>
/// <remarks>
/// It looks every few seconds, and at once when this instance queues something, so an
/// automation's email or a webhook leaves within moments of the save that queued it,
/// without that save waiting for it.
/// </remarks>
public sealed class OutboxDispatcherBackgroundService : BackgroundService
{
    private static readonly TimeSpan FirstPass = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    /// <summary>Full batches worked through in one pass before waiting again, so a backlog clears but never spins.</summary>
    private const int MaxBatchesPerPass = 20;

    private readonly OutboxDispatcher _dispatcher;
    private readonly OutboxSignal _signal;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;

    public OutboxDispatcherBackgroundService(OutboxDispatcher dispatcher, OutboxSignal signal, ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _dispatcher = dispatcher;
        _signal = signal;
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
                for (var batch = 0; batch < MaxBatchesPerPass; batch++)
                {
                    if (await _dispatcher.DispatchDueAsync(stoppingToken) < OutboxDispatcher.BatchSize) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch failed");
            }

            try
            {
                await _signal.WaitAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
