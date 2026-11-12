using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

/// <summary>Automation runs and their logs (see <see cref="IAutomationRunStore"/>).</summary>
public sealed class AutomationRunStore : IAutomationRunStore
{
    /// <summary>
    /// The in-memory provider has no conditional UPDATE, so its claims are taken one at a time
    /// instead. Only tests use it, and they run in one process.
    /// </summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    private readonly AppDbContext _db;

    public AutomationRunStore(AppDbContext db) => _db = db;

    public async Task<bool> StartAsync(AutomationRun run, CancellationToken ct = default)
    {
        // Checked first because the in-memory provider has no unique index; on SQL Server the
        // index on (RuleId, SourceKey) refuses the second of two instances that got past it.
        if (await _db.AutomationFires.AnyAsync(f => f.RuleId == run.RuleId && f.SourceKey == run.SourceKey, ct)) return false;
        var fire = new AutomationFire { Id = Guid.NewGuid(), RuleId = run.RuleId, SourceKey = run.SourceKey, FiredAtUtc = run.StartedAtUtc };
        _db.AutomationFires.Add(fire);
        _db.AutomationRuns.Add(run);
        try
        {
            // One save: the rule is never marked fired without its run, nor the other way round.
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Left in the unit of work, they would be tried again by whatever the request saves next.
            _db.Entry(fire).State = EntityState.Detached;
            _db.Entry(run).State = EntityState.Detached;
            return false;
        }
    }

    public Task<bool> HasFiredAsync(Guid ruleId, string sourceKey, CancellationToken ct = default) =>
        _db.AutomationFires.AnyAsync(f => f.RuleId == ruleId && f.SourceKey == sourceKey, ct);

    public async Task CountRunAsync(Guid ruleId, CancellationToken ct = default)
    {
        // Past the rule's version: the run count moving must not make an open editor's copy stale.
        if (_db.Database.IsRelational())
        {
            await _db.AutomationRules.Where(r => r.Id == ruleId)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.RunCount, r => r.RunCount + 1), ct);
            return;
        }
        var rule = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule == null) return;
        rule.RunCount += 1;
        await _db.SaveChangesAsync(ct);
    }

    public Task<AutomationRun?> GetAsync(Guid runId, CancellationToken ct = default) =>
        _db.AutomationRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);

    public void Log(AutomationRunStep step) => _db.AutomationRunSteps.Add(step);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another runner took the run after this one's claim ran out. What this one meant to
            // write is dropped, its log lines with it, so nothing later saves them by accident.
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            foreach (var entry in _db.ChangeTracker.Entries<AutomationRunStep>().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException();
        }
    }

    public object Checkpoint() =>
        _db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);

    public void RevertTo(object checkpoint)
    {
        var pendingBefore = (HashSet<object>)checkpoint;
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (pendingBefore.Contains(entry.Entity)) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }

    // The runner works for every workspace, so what is due is read across them; each run is
    // then claimed by id and carried on acting for its own workspace (AutomationEngine).
    // A run whose rule is turned off waits until the rule is on again.
    public async Task<IReadOnlyList<DueAutomationRun>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.AutomationRuns.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.NextAttemptAtUtc != null && r.NextAttemptAtUtc <= utcNow
                && (r.Status == AutomationRunStatuses.Running || r.Status == AutomationRunStatuses.Waiting || r.Status == AutomationRunStatuses.Retrying)
                && (r.ClaimedUntilUtc == null || r.ClaimedUntilUtc <= utcNow)
                && _db.AutomationRules.Any(rule => rule.Id == r.RuleId && rule.Enabled))
            .OrderBy(r => r.NextAttemptAtUtc)
            .Take(take)
            .Select(r => new DueAutomationRun(r.Id, r.ClaimCount))
            .ToListAsync(ct);

    public async Task<bool> TryClaimAsync(Guid runId, int claimCount, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default)
    {
        // Every instance runs the runner. One conditional UPDATE is atomic on the database: the
        // row moves only if nobody has claimed it since it was read, so exactly one runner sees
        // a row affected and runs its step. By id, across workspaces: the runner knows only the run.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.AutomationRuns.IgnoreQueryFilters()
                .Where(r => r.Id == runId && r.ClaimCount == claimCount
                    && r.NextAttemptAtUtc != null && r.NextAttemptAtUtc <= utcNow
                    && (r.Status == AutomationRunStatuses.Running || r.Status == AutomationRunStatuses.Waiting || r.Status == AutomationRunStatuses.Retrying)
                    && (r.ClaimedUntilUtc == null || r.ClaimedUntilUtc <= utcNow))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.ClaimedUntilUtc, claimedUntilUtc)
                    .SetProperty(r => r.ClaimCount, claimCount + 1), ct);
            return moved == 1;
        }

        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.AutomationRuns.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (row == null || row.ClaimCount != claimCount || row.NextAttemptAtUtc == null || row.NextAttemptAtUtc > utcNow
                || !AutomationRunStatuses.Active.Contains(row.Status)
                || (row.ClaimedUntilUtc != null && row.ClaimedUntilUtc > utcNow))
                return false;
            row.ClaimedUntilUtc = claimedUntilUtc;
            row.ClaimCount = claimCount + 1;
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.Entry(row).State = EntityState.Detached;
                return false;
            }
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    // By id, across workspaces: the runner learns the run's workspace here, then acts for it.
    public async Task<(Guid? OrganizationId, Guid ActorUserId)?> OwnerOfAsync(Guid runId, CancellationToken ct = default)
    {
        var owner = await _db.AutomationRuns.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.Id == runId)
            .Select(r => new { r.OrganizationId, r.ActorUserId })
            .FirstOrDefaultAsync(ct);
        return owner == null ? null : (owner.OrganizationId, owner.ActorUserId);
    }

    public Task<AutomationRule?> RuleAsync(Guid ruleId, CancellationToken ct = default) =>
        _db.AutomationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == ruleId, ct);

    public async Task<IReadOnlyList<AutomationRun>> ListAsync(Guid? ruleId, string? status, int take, CancellationToken ct = default)
    {
        var query = _db.AutomationRuns.AsNoTracking().AsQueryable();
        if (ruleId is Guid rule) query = query.Where(r => r.RuleId == rule);
        query = status switch
        {
            "active" => query.Where(r => r.Status == AutomationRunStatuses.Running || r.Status == AutomationRunStatuses.Waiting
                || r.Status == AutomationRunStatuses.Retrying),
            null or "" or "all" => query,
            _ => query.Where(r => r.Status == status),
        };
        return await query.OrderByDescending(r => r.StartedAtUtc).Take(take).ToListAsync(ct);
    }

    public Task<AutomationRun?> FindAsync(Guid runId, CancellationToken ct = default) =>
        _db.AutomationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);

    public async Task<IReadOnlyList<AutomationRunStep>> StepsAsync(Guid runId, CancellationToken ct = default) =>
        await _db.AutomationRunSteps.AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.AtUtc).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> EmailStatusesAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct = default)
    {
        if (messageIds.Count == 0) return new Dictionary<Guid, string>();
        var rows = await _db.OutboundMessages.AsNoTracking()
            .Where(m => messageIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Status, m.DeliveryStatus })
            .ToListAsync(ct);
        // What the mail service reported back once it has, else where the outbox has it.
        return rows.ToDictionary(r => r.Id, r => r.Status == OutboundStatuses.Sent && r.DeliveryStatus != null ? r.DeliveryStatus : r.Status);
    }

    public async Task<IReadOnlyDictionary<Guid, AutomationRuleRunCounts>> CountsAsync(CancellationToken ct = default)
    {
        var rows = await _db.AutomationRuns.AsNoTracking()
            .Where(r => r.Status == AutomationRunStatuses.Running || r.Status == AutomationRunStatuses.Waiting
                || r.Status == AutomationRunStatuses.Retrying || r.Status == AutomationRunStatuses.Failed)
            .GroupBy(r => new { r.RuleId, r.Status })
            .Select(g => new { g.Key.RuleId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.RuleId).ToDictionary(
            g => g.Key,
            g => new AutomationRuleRunCounts(
                g.Where(r => r.Status != AutomationRunStatuses.Failed).Sum(r => r.Count),
                g.Where(r => r.Status == AutomationRunStatuses.Failed).Sum(r => r.Count)));
    }

    public async Task<bool> TryRetryAsync(Guid runId, DateTime utcNow, CancellationToken ct = default)
    {
        // The claim count moves too, so a runner that held this run before cannot write over it.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.AutomationRuns
                .Where(r => r.Id == runId && r.Status == AutomationRunStatuses.Failed)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, AutomationRunStatuses.Retrying)
                    .SetProperty(r => r.NextAttemptAtUtc, utcNow)
                    .SetProperty(r => r.StepAttempts, 0)
                    .SetProperty(r => r.FinishedAtUtc, (DateTime?)null)
                    .SetProperty(r => r.ClaimedUntilUtc, (DateTime?)null)
                    .SetProperty(r => r.UpdatedAtUtc, utcNow)
                    .SetProperty(r => r.ClaimCount, r => r.ClaimCount + 1), ct);
            return moved == 1;
        }
        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.AutomationRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (row == null || row.Status != AutomationRunStatuses.Failed) return false;
            row.Status = AutomationRunStatuses.Retrying;
            row.NextAttemptAtUtc = utcNow;
            row.StepAttempts = 0;
            row.FinishedAtUtc = null;
            row.ClaimedUntilUtc = null;
            row.UpdatedAtUtc = utcNow;
            row.ClaimCount += 1;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    public async Task<bool> TryCancelAsync(Guid runId, string reason, DateTime utcNow, CancellationToken ct = default)
    {
        // Not while a runner holds it: that one may be running a step right now, and "stopped"
        // would then be untrue.
        if (_db.Database.IsRelational())
        {
            var moved = await _db.AutomationRuns
                .Where(r => r.Id == runId
                    && (r.Status == AutomationRunStatuses.Running || r.Status == AutomationRunStatuses.Waiting || r.Status == AutomationRunStatuses.Retrying)
                    && (r.ClaimedUntilUtc == null || r.ClaimedUntilUtc <= utcNow))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.Status, AutomationRunStatuses.Cancelled)
                    .SetProperty(r => r.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(r => r.FinishedAtUtc, utcNow)
                    .SetProperty(r => r.WaitingFor, (string?)null)
                    .SetProperty(r => r.LastError, reason)
                    .SetProperty(r => r.UpdatedAtUtc, utcNow)
                    .SetProperty(r => r.ClaimCount, r => r.ClaimCount + 1), ct);
            return moved == 1;
        }
        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await _db.AutomationRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (row == null || !AutomationRunStatuses.Active.Contains(row.Status) || (row.ClaimedUntilUtc != null && row.ClaimedUntilUtc > utcNow))
                return false;
            row.Status = AutomationRunStatuses.Cancelled;
            row.NextAttemptAtUtc = null;
            row.FinishedAtUtc = utcNow;
            row.WaitingFor = null;
            row.LastError = reason;
            row.UpdatedAtUtc = utcNow;
            row.ClaimCount += 1;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    // Across workspaces: the renewal scan has no workspace of its own; it acts for each one found.
    public async Task<IReadOnlyList<Guid>> WorkspacesWithRulesAsync(string trigger, CancellationToken ct = default) =>
        await _db.AutomationRules.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.Enabled && r.Trigger == trigger && r.OrganizationId != null)
            .Select(r => r.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(ct);
}
