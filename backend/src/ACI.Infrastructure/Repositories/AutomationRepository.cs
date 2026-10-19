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
            existing.ActionsJson = rule.ActionsJson;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await GetAsync(id, organizationId, ct);
        if (row == null) return false;
        var fires = await _db.AutomationFires.Where(f => f.RuleId == id).ToListAsync(ct);
        _db.AutomationFires.RemoveRange(fires);
        _db.AutomationRules.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AutomationRule>> EnabledForTriggerAsync(Guid? organizationId, string trigger, CancellationToken ct = default) =>
        await Scope(_db.AutomationRules.AsNoTracking(), organizationId)
            .Where(r => r.Enabled && r.Trigger == trigger)
            .ToListAsync(ct);

    public async Task<bool> TryMarkFiredAsync(Guid ruleId, string sourceKey, CancellationToken ct = default)
    {
        var exists = await _db.AutomationFires.AnyAsync(f => f.RuleId == ruleId && f.SourceKey == sourceKey, ct);
        if (exists) return false;
        _db.AutomationFires.Add(new AutomationFire
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            SourceKey = sourceKey,
            FiredAtUtc = DateTime.UtcNow,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task IncrementRunCountAsync(Guid ruleId, CancellationToken ct = default)
    {
        var row = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (row == null) return;
        row.RunCount += 1;
        await _db.SaveChangesAsync(ct);
    }
}
