using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DealRepository : IDealRepository
{
    private readonly AppDbContext _db;

    public DealRepository(AppDbContext db) => _db = db;

    private IQueryable<Deal> FilterByUserAndOrg(IQueryable<Deal> q, Guid userId, Guid? organizationId) =>
        q.VisibleTo(_db, userId, organizationId);

    private static IQueryable<Deal> IncludeRelated(IQueryable<Deal> q) =>
        q.Include(d => d.Assignee)
         .Include(d => d.Company)
         .Include(d => d.Contact)
         .Include(d => d.Pipeline)
         .Include(d => d.DealStage);

    private static IQueryable<Deal> ApplySearch(IQueryable<Deal> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(d => d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q)
            || (d.Company != null && d.Company.Name.ToLower().Contains(q))
            || (d.Contact != null && d.Contact.Name.ToLower().Contains(q))
            || (d.Stage != null && d.Stage.ToLower().Contains(q)));
    }

    public async Task<(IReadOnlyList<Deal> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        Guid? companyId = null,
        Guid? contactId = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Deals, userId, organizationId);
        if (companyId.HasValue) query = query.Where(d => d.CompanyId == companyId.Value);
        if (contactId.HasValue) query = query.Where(d => d.ContactId == contactId.Value);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await IncludeRelated(query)
            .OrderBy(d => d.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        
        return (items, totalCount);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Deals, userId, organizationId);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Deal>> GetOpenAsync(Guid userId, Guid? organizationId, bool mineOnly, int take, CancellationToken ct = default)
    {
        // A lost deal with no reason still owes one: "record why it was lost" is a step.
        var query = FilterByUserAndOrg(_db.Deals.AsNoTracking(), userId, organizationId)
            .Where(d => d.IsWon == null || (d.IsWon == false && (d.ClosedReason == null || d.ClosedReason.Trim() == "")));
        if (mineOnly) query = query.Where(WorkScope.DealOf(userId));
        return await IncludeRelated(query)
            .OrderBy(d => d.ExpectedCloseDateUtc ?? DateTime.MaxValue).ThenBy(d => d.Name).ThenBy(d => d.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Deal>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            if (take is not > 0)
                return await GetByUserIdAsync(userId, organizationId, ct);
            return await IncludeRelated(FilterByUserAndOrg(_db.Deals.AsNoTracking(), userId, organizationId))
                .OrderBy(d => d.Name)
                .Take(take.Value)
                .ToListAsync(ct);
        }
        var q = query.Trim().ToLowerInvariant();
        var matches = IncludeRelated(FilterByUserAndOrg(_db.Deals.AsNoTracking(), userId, organizationId)
            .Where(d => d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q)
                || (d.Company != null && d.Company.Name.ToLower().Contains(q))
                || (d.Contact != null && d.Contact.Name.ToLower().Contains(q))
                || (d.Stage != null && d.Stage.ToLower().Contains(q))))
            .OrderBy(d => d.Name);
        if (take is > 0)
            return await matches.Take(take.Value).ToListAsync(ct);
        return await matches.ToListAsync(ct);
    }

    public async Task<Deal?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Deal> AddAsync(Deal deal, CancellationToken ct = default)
    {
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Deal?> UpdateAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).FirstOrDefaultAsync(d => d.Id == deal.Id, ct);
        if (existing == null) return null;
        existing.Name = deal.Name;
        existing.Value = deal.Value;
        existing.Amount = deal.Amount;
        existing.Currency = deal.Currency;
        existing.Stage = deal.Stage;
        existing.PipelineId = deal.PipelineId;
        existing.DealStageId = deal.DealStageId;
        existing.CompanyId = deal.CompanyId;
        existing.ContactId = deal.ContactId;
        existing.AssigneeId = deal.AssigneeId;
        existing.ExpectedCloseDateUtc = deal.ExpectedCloseDateUtc;
        existing.IsWon = deal.IsWon;
        existing.Description = deal.Description;
        existing.Probability = deal.Probability;
        existing.ClosedReason = deal.ClosedReason;
        existing.ClosedReasonCategory = deal.ClosedReasonCategory;
        existing.ClosedAtUtc = deal.ClosedAtUtc;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (existing == null) return false;
        var linkedTasks = await _db.TaskItems.Where(t => t.DealId == id).ToListAsync(ct);
        foreach (var t in linkedTasks) t.DealId = null;
        var linkedActivities = await _db.Activities.Where(a => a.DealId == id).ToListAsync(ct);
        foreach (var a in linkedActivities) a.DealId = null;
        _db.Deals.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        RecordMerge.InTransactionAsync(_db, async () =>
        {
            if (keepId == dropId) return false;
            // Both through visibility: a private deal someone cannot see is not theirs to fold away.
            var keep = await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == keepId, ct);
            var drop = await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == dropId, ct);
            if (keep == null || drop == null) return false;

            foreach (var task in await _db.TaskItems.Where(t => t.DealId == dropId).ToListAsync(ct))
                task.DealId = keepId;
            foreach (var activity in await _db.Activities.Where(a => a.DealId == dropId).ToListAsync(ct))
                activity.DealId = keepId;
            // Stage history cascades on delete and line items go with a deleted deal, so
            // both have to move before the other deal is removed.
            foreach (var change in await _db.DealStageChanges.Where(c => c.DealId == dropId).ToListAsync(ct))
                change.DealId = keepId;
            foreach (var item in await _db.DealLineItems.Where(i => i.DealId == dropId).ToListAsync(ct))
                item.DealId = keepId;
            foreach (var message in await _db.SentMessages.Where(m => m.DealId == dropId).ToListAsync(ct))
                message.DealId = keepId;
            foreach (var message in await _db.OutboundMessages.Where(m => m.DealId == dropId).ToListAsync(ct))
                message.DealId = keepId;
            foreach (var contract in await _db.Contracts.Where(c => c.DealId == dropId).ToListAsync(ct))
                contract.DealId = keepId;
            foreach (var lead in await _db.Leads.Where(l => l.ConvertedToDealId == dropId).ToListAsync(ct))
                lead.ConvertedToDealId = keepId;

            await RecordMerge.MoveDependentsAsync(_db, RecordTypes.Deal, keepId, dropId, ct);

            // The kept deal's own values stay — its name, stage and outcome among them;
            // what it is missing is filled in. An amount of zero, or none at all, is missing.
            if (IsBlankAmount(keep) && !IsBlankAmount(drop))
            {
                keep.Amount = drop.Amount;
                keep.Value = drop.Value;
                keep.Currency = drop.Currency ?? keep.Currency;
            }
            keep.Currency = RecordMerge.Fill(keep.Currency, drop.Currency);
            keep.CompanyId ??= drop.CompanyId;
            keep.ContactId ??= drop.ContactId;
            keep.AssigneeId ??= drop.AssigneeId;
            keep.TeamId ??= drop.TeamId;
            keep.ExpectedCloseDateUtc ??= drop.ExpectedCloseDateUtc;
            keep.Description = RecordMerge.Fill(keep.Description, drop.Description);
            keep.Probability ??= drop.Probability;
            // Either deal being private is someone saying it is private. Folding it into a
            // shared deal must not quietly show it to the team.
            if (drop.IsPrivate) keep.IsPrivate = true;
            keep.CreatedAtUtc = RecordMerge.Earliest(keep.CreatedAtUtc, drop.CreatedAtUtc)!.Value;
            keep.UpdatedAtUtc = DateTime.UtcNow;
            keep.UpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);

            _db.Deals.Remove(drop);
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);

    // Read from the stored amount, not by parsing the text again: the text of an older deal
    // can be in any notation, and "1.234,56" read as invariant text is not 1234.56.
    private static bool IsBlankAmount(Deal deal) =>
        deal.Amount == 0m || (deal.Amount == null && string.IsNullOrWhiteSpace(deal.Value));

    public async Task<IReadOnlyDictionary<Guid, DealPaceInfo>> GetPaceAsync(Guid userId, Guid? organizationId, IEnumerable<Guid> dealIds, CancellationToken ct = default)
    {
        var ids = dealIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DealPaceInfo>();
        var rows = await FilterByUserAndOrg(_db.Deals.AsNoTracking(), userId, organizationId)
            .Where(d => ids.Contains(d.Id))
            .PaceOf(_db, userId, organizationId)
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.DealId, r => new DealPaceInfo(r.StageEnteredAtUtc, r.LastMoveAtUtc));
    }

    public async Task<IReadOnlyList<DealStageChange>> GetStageChangesAsync(
        Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Validate deal belongs to user
        var dealExists = await FilterByUserAndOrg(_db.Deals, userId, organizationId)
            .AnyAsync(d => d.Id == dealId, ct);
        if (!dealExists) return Array.Empty<DealStageChange>();

        return await _db.DealStageChanges.AsNoTracking()
            .Where(sc => sc.DealId == dealId)
            .Include(sc => sc.ChangedByUser)
            .OrderByDescending(sc => sc.ChangedAtUtc)
            .ThenByDescending(sc => sc.Id)
            .ToListAsync(ct);
    }
}
