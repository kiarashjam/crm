using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Services;

/// <summary>Mails the counterparty once when a sent contract is still unopened after three days.</summary>
public sealed class ContractReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Wait = TimeSpan.FromDays(3);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ContractReminderBackgroundService> _logger;

    public ContractReminderBackgroundService(IServiceScopeFactory scopes, ILogger<ContractReminderBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var due = DateTime.UtcNow.Subtract(Wait);
                var rows = await db.Contracts
                    .Where(c => c.Status == ContractStatuses.Sent
                        && c.SentAtUtc != null
                        && c.SentAtUtc <= due
                        && c.ReminderSentAtUtc == null
                        && c.CounterpartyEmail != "")
                    .OrderBy(c => c.SentAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var contract in rows)
                {
                    var sent = await email.SendMessageAsync(
                        contract.CounterpartyEmail,
                        contract.CounterpartyName,
                        $"Reminder: {contract.Title}",
                        "A contract is waiting for your signature. Please use the link from the original email.",
                        stoppingToken);
                    if (!sent) continue;
                    contract.ReminderSentAtUtc = DateTime.UtcNow;
                }
                if (rows.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Contract reminder run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
