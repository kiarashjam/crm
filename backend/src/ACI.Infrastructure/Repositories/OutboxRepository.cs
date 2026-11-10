using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    /// <summary>
    /// The in-memory provider has no conditional UPDATE, so its claims are taken one at
    /// a time instead. Only tests use it, and they run in one process.
    /// </summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    private readonly AppDbContext _db;

    public OutboxRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(OutboundMessage message, CancellationToken ct = default)
    {
        _db.OutboundMessages.Add(message);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Left in the unit of work, a message that failed to save would be tried
            // again by whatever this request saves next, and fail that too.
            _db.Entry(message).State = EntityState.Detached;
            throw;
        }
    }

    public Task<OutboundMessage?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.OutboundMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    // By id, across workspaces: the dispatcher and the mail service's reports know only
    // the message, and act for its workspace once they have it.
    public Task<OutboundMessage?> GetInAnyWorkspaceAsync(Guid id, CancellationToken ct = default) =>
        _db.OutboundMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    // One outbox sends for every workspace, so its queue is read across them; each message
    // is then attempted acting for its own workspace (OutboxService.ProcessAsync).
    public async Task<IReadOnlyList<DueOutboundMessage>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.OutboundMessages.AsNoTracking().IgnoreQueryFilters()
            .Where(m => m.Status == OutboundStatuses.Queued
                && m.NextAttemptAtUtc != null && m.NextAttemptAtUtc <= utcNow
                && (m.ClaimedUntilUtc == null || m.ClaimedUntilUtc <= utcNow))
            .OrderBy(m => m.NextAttemptAtUtc)
            .Take(take)
            .Select(m => new DueOutboundMessage(m.Id, m.Attempts))
            .ToListAsync(ct);

    public async Task<bool> TryClaimAsync(Guid id, int attempts, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default)
    {
        // The dispatcher runs on every app instance. One conditional UPDATE is atomic on
        // the database: the row moves only if nobody has moved it since it was read, so
        // exactly one caller sees a row affected and sends it.
        // By id, across workspaces: only the dispatcher claims, and it knows the message alone.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.OutboundMessages.IgnoreQueryFilters()
                .Where(m => m.Id == id && m.Status == OutboundStatuses.Queued && m.Attempts == attempts
                    && m.NextAttemptAtUtc != null && m.NextAttemptAtUtc <= utcNow
                    && (m.ClaimedUntilUtc == null || m.ClaimedUntilUtc <= utcNow))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(m => m.ClaimedUntilUtc, claimedUntilUtc)
                    .SetProperty(m => m.Attempts, attempts + 1), ct);
            return moved == 1;
        }

        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.OutboundMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id, ct);
            if (row == null || row.Status != OutboundStatuses.Queued || row.Attempts != attempts
                || row.NextAttemptAtUtc == null || row.NextAttemptAtUtc > utcNow
                || (row.ClaimedUntilUtc != null && row.ClaimedUntilUtc > utcNow))
                return false;
            row.ClaimedUntilUtc = claimedUntilUtc;
            row.Attempts = attempts + 1;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    public async Task<bool> TryCancelAsync(Guid id, DateTime utcNow, CancellationToken ct = default)
    {
        // Not while an attempt holds it: that one may be handing it to the mail server
        // right now, and "cancelled" would then be untrue.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.OutboundMessages
                .Where(m => m.Id == id && m.Status == OutboundStatuses.Queued
                    && (m.ClaimedUntilUtc == null || m.ClaimedUntilUtc <= utcNow))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(m => m.Status, OutboundStatuses.Cancelled)
                    .SetProperty(m => m.NextAttemptAtUtc, (DateTime?)null), ct);
            return moved == 1;
        }

        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.OutboundMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (row == null || row.Status != OutboundStatuses.Queued || (row.ClaimedUntilUtc != null && row.ClaimedUntilUtc > utcNow))
                return false;
            row.Status = OutboundStatuses.Cancelled;
            row.NextAttemptAtUtc = null;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    public Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default) =>
        _db.OutboundMessages.AnyAsync(m => m.SourceId == sourceId && m.Status == OutboundStatuses.Queued, ct);

    public Task<OutboundMessage?> FindByMessageIdAsync(string messageIdHeader, CancellationToken ct = default)
    {
        var bare = (messageIdHeader ?? "").Trim().TrimStart('<').TrimEnd('>').Trim();
        if (bare.Length == 0) return Task.FromResult<OutboundMessage?>(null);
        var header = $"<{bare}>";
        // Across workspaces: a reply or a report names the message, which names its workspace.
        return _db.OutboundMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MessageIdHeader == header, ct);
    }

    public Task<OutboundMessage?> FindByReplyTokenAsync(string token, CancellationToken ct = default)
    {
        var wanted = (token ?? "").Trim().ToLowerInvariant();
        if (wanted.Length == 0) return Task.FromResult<OutboundMessage?>(null);
        // Across workspaces, as above: the reply address's token is the message's own.
        return _db.OutboundMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.ReplyToken == wanted, ct);
    }

    public async Task<IReadOnlyList<OutboundMessage>> WebhookDeliveriesAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Channel == OutboundChannels.Webhook)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<EmailSuppression?> FindSuppressionAsync(Guid organizationId, IReadOnlyCollection<string> addresses, CancellationToken ct = default)
    {
        var wanted = addresses.Select(DoNotContactRules.Normalize).Where(a => a.Length > 0).Distinct().ToList();
        if (wanted.Count == 0) return null;
        return await _db.EmailSuppressions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && wanted.Contains(s.Email))
            .OrderBy(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> SuppressAsync(EmailSuppression suppression, CancellationToken ct = default)
    {
        suppression.Email = DoNotContactRules.Normalize(suppression.Email);
        if (suppression.Email.Length == 0) return false;
        if (await _db.EmailSuppressions.AnyAsync(s => s.OrganizationId == suppression.OrganizationId && s.Email == suppression.Email, ct))
            return false;
        _db.EmailSuppressions.Add(suppression);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Two reports about one address at once: the other one put it on the list.
            _db.Entry(suppression).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<EmailSuppression>> SuppressionsAsync(Guid organizationId, int take, CancellationToken ct = default) =>
        await _db.EmailSuppressions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<bool> LiftSuppressionAsync(Guid organizationId, Guid suppressionId, CancellationToken ct = default)
    {
        var row = await _db.EmailSuppressions.FirstOrDefaultAsync(s => s.Id == suppressionId && s.OrganizationId == organizationId, ct);
        if (row == null) return false;
        _db.EmailSuppressions.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> InboundSeenAsync(Guid organizationId, string messageIdHeader, CancellationToken ct = default) =>
        _db.InboundEmails.AnyAsync(i => i.OrganizationId == organizationId && i.MessageIdHeader == messageIdHeader, ct);

    public async Task<bool> TryAddInboundAsync(InboundEmail inbound, CancellationToken ct = default)
    {
        _db.InboundEmails.Add(inbound);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // The same reply posted twice at once: the other post took it in.
            _db.Entry(inbound).State = EntityState.Detached;
            return false;
        }
    }
}
