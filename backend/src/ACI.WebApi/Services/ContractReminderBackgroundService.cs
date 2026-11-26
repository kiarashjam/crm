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

    /// <summary>Its name in the job heartbeats (/health, the System health page).</summary>
    public const string JobName = "contract-reminders";

    private readonly IServiceScopeFactory _scopes;
    private readonly IJobHeartbeats _heartbeats;
    private readonly ILogger<ContractReminderBackgroundService> _logger;

    public ContractReminderBackgroundService(IServiceScopeFactory scopes, IJobHeartbeats heartbeats, ILogger<ContractReminderBackgroundService> logger)
    {
        _scopes = scopes;
        _heartbeats = heartbeats;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _heartbeats.Register(JobName, Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var due = DateTime.UtcNow.Subtract(Wait);
                // Every workspace's contracts: one job reminds every counterparty. It only
                // mails and stamps each contract, so nothing more needs a workspace.
                var rows = await db.Contracts
                    .IgnoreQueryFilters()
                    .Where(c => c.Status == ContractStatuses.Sent
                        && c.SentAtUtc != null
                        && c.SentAtUtc <= due
                        && c.ReminderSentAtUtc == null
                        && c.CounterpartyEmail != "")
                    .OrderBy(c => c.SentAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                // The reminder is the workspace's, under its name, as the request to sign was.
                var workspaceIds = rows.Select(c => c.OrganizationId).Distinct().ToList();
                var workspaces = rows.Count == 0
                    ? new Dictionary<Guid, string>()
                    : await db.Organizations.IgnoreQueryFilters().AsNoTracking()
                        .Where(o => workspaceIds.Contains(o.Id))
                        .ToDictionaryAsync(o => o.Id, o => o.Name, stoppingToken);

                foreach (var contract in rows)
                {
                    var sent = await email.SendContractReminderEmailAsync(
                        contract.CounterpartyEmail,
                        contract.CounterpartyName,
                        workspaces.GetValueOrDefault(contract.OrganizationId, ""),
                        contract.Title,
                        contract.SentAtUtc,
                        contract.SigningTokenExpiresAtUtc,
                        stoppingToken);
                    if (!sent) continue;
                    contract.ReminderSentAtUtc = DateTime.UtcNow;
                }
                if (rows.Count > 0) await db.SaveChangesAsync(stoppingToken);
                _heartbeats.Beat(JobName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _heartbeats.Failed(JobName, ex);
                _logger.LogError(ex, "Contract reminder run failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
