using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class AutomationRepository : IAutomationRepository
{
    private readonly AppDbContext _db;
    public AutomationRepository(AppDbContext db) => _db = db;

    private static IQueryable<AutomationRule> Scope(IQueryable<AutomationRule> query, Guid? organizationId) =>
        query.Where(r => r.OrganizationId == organizationId);

    public async Task<IReadOnlyList<AutomationRule>> ListAsync(Guid? organizationId, CancellationToken ct = default) =>
        await Scope(_db.AutomationRules.AsNoTracking(), organizationId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<AutomationRule?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        Scope(_db.AutomationRules, organizationId).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task SaveAsync(AutomationRule rule, CancellationToken ct = default)
    {
        var existing = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == rule.Id, ct);
        if (existing == null) _db.AutomationRules.Add(rule);
        else
        {
            existing.Name = rule.Name;
            existing.Enabled = rule.Enabled;
            existing.Trigger = rule.Trigger;
            existing.TriggerConfigJson = rule.TriggerConfigJson;
            existing.ConditionsJson = rule.ConditionsJson;
            existing.ActionsJson = rule.ActionsJson;
            existing.SendWindowJson = rule.SendWindowJson;
            existing.Version = rule.Version;
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Saved by someone else since this edit read it: their rule stands.
            _db.Entry(existing ?? rule).State = EntityState.Detached;
            throw new ACI.Application.Common.ConcurrencyConflictException();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await GetAsync(id, organizationId, ct);
        if (row == null) return false;
        var fires = await _db.AutomationFires.Where(f => f.RuleId == id).ToListAsync(ct);
        _db.AutomationFires.RemoveRange(fires);
        // Its runs go with it, waiting ones included, and their logs. SQL Server would cascade
        // them; the in-memory provider the tests run on does not.
        var runs = await _db.AutomationRuns.Where(r => r.RuleId == id).ToListAsync(ct);
        var runIds = runs.Select(r => r.Id).ToList();
        _db.AutomationRunSteps.RemoveRange(await _db.AutomationRunSteps.Where(s => runIds.Contains(s.RunId)).ToListAsync(ct));
        _db.AutomationRuns.RemoveRange(runs);
        _db.AutomationRules.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AutomationRule>> EnabledForTriggerAsync(Guid? organizationId, string trigger, CancellationToken ct = default) =>
        await Scope(_db.AutomationRules.AsNoTracking(), organizationId)
            .Where(r => r.Enabled && r.Trigger == trigger)
            .ToListAsync(ct);
}
