using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>The outbox's storage as a list, with the same rules for claims and cancels.</summary>
internal sealed class FakeOutboxRepository : IOutboxRepository
{
    public List<OutboundMessage> Messages { get; } = new();
    public List<EmailSuppression> Suppressions { get; } = new();
    public List<InboundEmail> Inbound { get; } = new();
    public int Saves { get; private set; }

    public Task AddAsync(OutboundMessage message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<OutboundMessage?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

    // One list, no workspaces: the same as GetAsync here.
    public Task<OutboundMessage?> GetInAnyWorkspaceAsync(Guid id, CancellationToken ct = default) => GetAsync(id, ct);

    public Task SaveAsync(CancellationToken ct = default)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DueOutboundMessage>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DueOutboundMessage>>(Messages
            .Where(m => m.Status == OutboundStatuses.Queued && m.NextAttemptAtUtc <= utcNow && (m.ClaimedUntilUtc == null || m.ClaimedUntilUtc <= utcNow))
            .OrderBy(m => m.NextAttemptAtUtc).Take(take).Select(m => new DueOutboundMessage(m.Id, m.Attempts)).ToList());

    public Task<bool> TryClaimAsync(Guid id, int attempts, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default)
    {
        var m = Messages.FirstOrDefault(x => x.Id == id);
        if (m == null || m.Status != OutboundStatuses.Queued || m.Attempts != attempts || m.NextAttemptAtUtc > utcNow
            || (m.ClaimedUntilUtc != null && m.ClaimedUntilUtc > utcNow))
            return Task.FromResult(false);
        m.ClaimedUntilUtc = claimedUntilUtc;
        m.Attempts = attempts + 1;
        return Task.FromResult(true);
    }

    public Task<bool> TryCancelAsync(Guid id, DateTime utcNow, CancellationToken ct = default)
    {
        var m = Messages.FirstOrDefault(x => x.Id == id);
        if (m == null || m.Status != OutboundStatuses.Queued || (m.ClaimedUntilUtc != null && m.ClaimedUntilUtc > utcNow))
            return Task.FromResult(false);
        m.Status = OutboundStatuses.Cancelled;
        m.NextAttemptAtUtc = null;
        return Task.FromResult(true);
    }

    public Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default) =>
        Task.FromResult(Messages.Any(m => m.SourceId == sourceId && m.Status == OutboundStatuses.Queued));

    public Task<OutboundMessage?> FindByMessageIdAsync(string messageIdHeader, CancellationToken ct = default)
    {
        var bare = messageIdHeader.Trim().TrimStart('<').TrimEnd('>');
        return Task.FromResult(Messages.FirstOrDefault(m => m.MessageIdHeader == $"<{bare}>"));
    }

    public Task<OutboundMessage?> FindByReplyTokenAsync(string token, CancellationToken ct = default) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.ReplyToken == token.Trim().ToLowerInvariant()));

    public Task<IReadOnlyList<OutboundMessage>> WebhookDeliveriesAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OutboundMessage>>(Messages
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Webhook)
            .OrderByDescending(m => m.CreatedAtUtc).Take(take).ToList());

    public Task<EmailSuppression?> FindSuppressionAsync(Guid organizationId, IReadOnlyCollection<string> addresses, CancellationToken ct = default)
    {
        var wanted = addresses.Select(a => a.Trim().ToLowerInvariant()).ToHashSet();
        return Task.FromResult(Suppressions.FirstOrDefault(s => s.OrganizationId == organizationId && wanted.Contains(s.Email)));
    }

    public Task<bool> SuppressAsync(EmailSuppression suppression, CancellationToken ct = default)
    {
        suppression.Email = suppression.Email.Trim().ToLowerInvariant();
        if (Suppressions.Any(s => s.OrganizationId == suppression.OrganizationId && s.Email == suppression.Email)) return Task.FromResult(false);
        Suppressions.Add(suppression);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<EmailSuppression>> SuppressionsAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EmailSuppression>>(Suppressions.Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAtUtc).Take(take).ToList());

    public Task<bool> LiftSuppressionAsync(Guid organizationId, Guid suppressionId, CancellationToken ct = default) =>
        Task.FromResult(Suppressions.RemoveAll(s => s.Id == suppressionId && s.OrganizationId == organizationId) > 0);

    public Task<bool> InboundSeenAsync(Guid organizationId, string messageIdHeader, CancellationToken ct = default) =>
        Task.FromResult(Inbound.Any(i => i.OrganizationId == organizationId && i.MessageIdHeader == messageIdHeader));

    public Task<bool> TryAddInboundAsync(InboundEmail inbound, CancellationToken ct = default)
    {
        if (inbound.MessageIdHeader != null && Inbound.Any(i => i.OrganizationId == inbound.OrganizationId && i.MessageIdHeader == inbound.MessageIdHeader))
            return Task.FromResult(false);
        Inbound.Add(inbound);
        return Task.FromResult(true);
    }
}
