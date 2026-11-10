using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CampaignRepository : ICampaignRepository
{
    /// <summary>
    /// The in-memory provider has no conditional UPDATE, so its claims are taken one at a
    /// time instead. Only tests use it, and they run in one process.
    /// </summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    private readonly AppDbContext _db;

    public CampaignRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Campaign>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Campaigns.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<Campaign?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);

    public Task<Campaign?> ReadAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);

    public async Task AddAsync(Campaign campaign, CancellationToken ct = default)
    {
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(Campaign campaign, Guid? expectedVersion, CancellationToken ct = default)
    {
        // Checked against the version the person edited, not only against this read: what
        // someone saved in between must not be written over with an older copy.
        if (expectedVersion is Guid version)
            _db.Entry(campaign).Property(c => c.Version).OriginalValue = version;
        await _db.SaveRefusingStaleAsync(ct);
    }

    public async Task<bool> DeleteUnsentAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        // Only while nothing of it has gone: a delete racing a Send loses to it.
        if (_db.Database.IsRelational())
        {
            var deleted = await _db.Campaigns
                .Where(c => c.Id == id && c.OrganizationId == organizationId
                    && (c.Status == CampaignStatuses.Draft || c.Status == CampaignStatuses.Scheduled))
                .ExecuteDeleteAsync(ct);
            return deleted == 1;
        }
        var row = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);
        if (row == null || !CampaignStatuses.IsEditable(row.Status)) return false;
        _db.Campaigns.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> TryClaimToSendAsync(Guid organizationId, Guid id, Guid? expectedVersion, Guid sentByUserId, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default) =>
        ClaimAsync(
            c => c.Id == id && c.OrganizationId == organizationId
                && (c.Status == CampaignStatuses.Draft || c.Status == CampaignStatuses.Scheduled)
                && (expectedVersion == null || c.Version == expectedVersion),
            sentByUserId, utcNow, claimedUntilUtc, ct);

    // By id, across workspaces: only the scheduler claims this way, and it knows the campaign alone.
    public Task<bool> TryClaimDueAsync(Guid id, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default) =>
        ClaimAsync(
            c => c.Id == id
                && ((c.Status == CampaignStatuses.Scheduled && c.ScheduledAtUtc != null && c.ScheduledAtUtc <= utcNow)
                    || (c.Status == CampaignStatuses.Sending && (c.ClaimedUntilUtc == null || c.ClaimedUntilUtc <= utcNow))),
            null, utcNow, claimedUntilUtc, ct);

    /// <summary>
    /// One conditional UPDATE, atomic on the database: the row moves only if it still matches,
    /// so of two instances, two presses or a retry, exactly one sees a row affected and sends.
    /// </summary>
    private async Task<bool> ClaimAsync(System.Linq.Expressions.Expression<Func<Campaign, bool>> claimable,
        Guid? sentByUserId, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct)
    {
        var version = Guid.NewGuid();
        if (_db.Database.IsRelational())
        {
            var query = _db.Campaigns.IgnoreQueryFilters().Where(claimable);
            var moved = sentByUserId is Guid sender
                ? await query.ExecuteUpdateAsync(set => set
                    .SetProperty(c => c.Status, CampaignStatuses.Sending)
                    .SetProperty(c => c.SendingStartedAtUtc, c => c.SendingStartedAtUtc ?? (DateTime?)utcNow)
                    .SetProperty(c => c.SentByUserId, sender)
                    .SetProperty(c => c.ClaimedUntilUtc, claimedUntilUtc)
                    .SetProperty(c => c.UpdatedAtUtc, utcNow)
                    .SetProperty(c => c.Version, version), ct)
                : await query.ExecuteUpdateAsync(set => set
                    .SetProperty(c => c.Status, CampaignStatuses.Sending)
                    .SetProperty(c => c.SendingStartedAtUtc, c => c.SendingStartedAtUtc ?? (DateTime?)utcNow)
                    .SetProperty(c => c.ClaimedUntilUtc, claimedUntilUtc)
                    .SetProperty(c => c.UpdatedAtUtc, utcNow)
                    .SetProperty(c => c.Version, version), ct);
            return moved == 1;
        }

        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.Campaigns.IgnoreQueryFilters().Where(claimable).FirstOrDefaultAsync(ct);
            if (row == null) return false;
            row.Status = CampaignStatuses.Sending;
            row.SendingStartedAtUtc ??= utcNow;
            if (sentByUserId is Guid sender) row.SentByUserId = sender;
            row.ClaimedUntilUtc = claimedUntilUtc;
            row.UpdatedAtUtc = utcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    public Task ExtendClaimAsync(Guid id, DateTime claimedUntilUtc, CancellationToken ct = default) =>
        UpdateSendingAsync(id, c => c.ClaimedUntilUtc = claimedUntilUtc,
            q => q.ExecuteUpdateAsync(set => set.SetProperty(c => c.ClaimedUntilUtc, claimedUntilUtc), ct), ct);

    public Task ReleaseClaimAsync(Guid id, DateTime retryAtUtc, CancellationToken ct = default) =>
        UpdateSendingAsync(id, c => c.ClaimedUntilUtc = retryAtUtc,
            q => q.ExecuteUpdateAsync(set => set.SetProperty(c => c.ClaimedUntilUtc, retryAtUtc), ct), ct);

    public Task FinishAsync(Guid id, DateTime sentAtUtc, int recipientCount, CancellationToken ct = default)
    {
        var version = Guid.NewGuid();
        return UpdateSendingAsync(id,
            c =>
            {
                c.Status = CampaignStatuses.Sent;
                c.SentAtUtc = sentAtUtc;
                c.RecipientCount = recipientCount;
                c.ClaimedUntilUtc = null;
                c.UpdatedAtUtc = sentAtUtc;
            },
            q => q.ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Status, CampaignStatuses.Sent)
                .SetProperty(c => c.SentAtUtc, sentAtUtc)
                .SetProperty(c => c.RecipientCount, recipientCount)
                .SetProperty(c => c.ClaimedUntilUtc, (DateTime?)null)
                .SetProperty(c => c.UpdatedAtUtc, sentAtUtc)
                .SetProperty(c => c.Version, version), ct),
            ct);
    }

    // Only a campaign still sending: one finished by another instance meanwhile stays as it is.
    private async Task UpdateSendingAsync(Guid id, Action<Campaign> change, Func<IQueryable<Campaign>, Task<int>> update, CancellationToken ct)
    {
        var query = _db.Campaigns.IgnoreQueryFilters().Where(c => c.Id == id && c.Status == CampaignStatuses.Sending);
        if (_db.Database.IsRelational())
        {
            await update(query);
            return;
        }
        var row = await query.FirstOrDefaultAsync(ct);
        if (row == null) return;
        change(row);
        await _db.SaveChangesAsync(ct);
    }

    // The scheduler reads every workspace's campaigns, then sends each acting for its own.
    public async Task<IReadOnlyList<DueCampaign>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.Campaigns.AsNoTracking().IgnoreQueryFilters()
            .Where(c => (c.Status == CampaignStatuses.Scheduled && c.ScheduledAtUtc != null && c.ScheduledAtUtc <= utcNow)
                || (c.Status == CampaignStatuses.Sending && (c.ClaimedUntilUtc == null || c.ClaimedUntilUtc <= utcNow)))
            .OrderBy(c => c.ScheduledAtUtc)
            .Take(take)
            .Select(c => new DueCampaign(c.Id, c.OrganizationId))
            .ToListAsync(ct);

    // ── Member messages ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MemberMessage>> MessagesAsync(Guid organizationId, Guid? memberId, Guid? campaignId, int take, CancellationToken ct = default)
    {
        var query = _db.MemberMessages.AsNoTracking().Where(m => m.OrganizationId == organizationId);
        if (memberId is Guid member) query = query.Where(m => m.MemberId == member);
        if (campaignId is Guid campaign) query = query.Where(m => m.CampaignId == campaign);
        return await query.OrderByDescending(m => m.CreatedAtUtc).Take(take).ToListAsync(ct);
    }

    public Task<MemberMessage?> GetMessageAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.MemberMessages.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);

    // By campaign alone: a campaign's messages are in its workspace, which the caller acts for.
    public async Task<IReadOnlyList<(Guid MemberId, string? Email, string Status)>> RecipientsOfAsync(Guid campaignId, CancellationToken ct = default) =>
        (await _db.MemberMessages.AsNoTracking()
            .Where(m => m.CampaignId == campaignId)
            .Select(m => new { m.MemberId, m.Email, m.Status })
            .ToListAsync(ct))
        .Select(m => (m.MemberId, m.Email, m.Status))
        .ToList();

    public async Task<bool> AddMessageAsync(MemberMessage message, CancellationToken ct = default)
    {
        _db.MemberMessages.Add(message);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // The campaign already has a message for this member (IX_MemberMessages_CampaignId_MemberId):
            // another sender wrote it first. Left in the unit of work, it would fail every later save.
            _db.Entry(message).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveMessageAsync(MemberMessage message, CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task RemoveMessageAsync(MemberMessage message, CancellationToken ct = default)
    {
        _db.MemberMessages.Remove(message);
        await _db.SaveChangesAsync(ct);
    }

    public void Forget(MemberMessage message) => _db.Entry(message).State = EntityState.Detached;

    public async Task<IReadOnlyList<OutboundDelivery>> DeliveriesAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken ct = default)
    {
        if (outboundIds.Count == 0) return [];
        return await _db.OutboundMessages.AsNoTracking()
            .Where(m => outboundIds.Contains(m.Id))
            .Select(m => new OutboundDelivery(m.Id, m.Status, m.DeliveryStatus, m.DeliveryDetail, m.LastError, m.SentAtUtc, m.DeliveryUpdatedAtUtc, m.OpenedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CampaignTally>> TallyAsync(Guid organizationId, IReadOnlyCollection<Guid> campaignIds, CancellationToken ct = default)
    {
        if (campaignIds.Count == 0) return [];
        // Counted in the database: a club's campaigns can hold tens of thousands of messages,
        // and the list needs only how many stand where.
        var rows = await (
                from m in _db.MemberMessages.AsNoTracking()
                where m.OrganizationId == organizationId && m.CampaignId != null && campaignIds.Contains(m.CampaignId.Value)
                join o in _db.OutboundMessages.AsNoTracking() on m.OutboundMessageId equals (Guid?)o.Id into sent
                from o in sent.DefaultIfEmpty()
                group m by new
                {
                    CampaignId = m.CampaignId!.Value,
                    m.Status,
                    OutboundStatus = o == null ? null : o.Status,
                    DeliveryStatus = o == null ? null : o.DeliveryStatus,
                    Opened = o != null && o.OpenedAtUtc != null,
                } into g
                select new { g.Key.CampaignId, g.Key.Status, g.Key.OutboundStatus, g.Key.DeliveryStatus, g.Key.Opened, Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new CampaignTally(r.CampaignId, r.Status, r.OutboundStatus, r.DeliveryStatus, r.Opened, r.Count)).ToList();
    }

    public async Task<bool> InTransactionAsync(Func<Task<bool>> work, CancellationToken ct = default)
    {
        // The in-memory provider the tests use has no transactions.
        if (!_db.Database.IsRelational() || _db.Database.CurrentTransaction != null)
            return await work();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var done = await work();
        if (done) await transaction.CommitAsync(ct);
        else await transaction.RollbackAsync(ct);
        return done;
    }
}
