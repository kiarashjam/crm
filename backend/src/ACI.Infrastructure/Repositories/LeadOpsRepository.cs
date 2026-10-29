using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadOpsRepository : ILeadOpsRepository
{
    private readonly AppDbContext _db;

    public LeadOpsRepository(AppDbContext db) => _db = db;

    // With no organization the workspace is the user's own. "OrganizationId IS NULL"
    // alone was every personal user's: the duplicate warning named another user's
    // lead, and a new lead was linked to another user's company.

    public Task<Lead?> FindOpenByEmailAsync(Guid userId, Guid? organizationId, string email, Guid? exceptId, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Leads.FirstOrDefaultAsync(l =>
            !l.IsConverted
            && l.Email.ToLower() == normalized
            && (exceptId == null || l.Id != exceptId)
            && (organizationId == null ? l.OrganizationId == null && l.UserId == userId : l.OrganizationId == organizationId), ct);
    }

    public Task<Company?> FindCompanyByDomainAsync(Guid userId, Guid? organizationId, string domain, CancellationToken ct = default)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        return _db.Companies.FirstOrDefaultAsync(c =>
            c.Domain != null
            && c.Domain.ToLower() == normalized
            && (organizationId == null ? c.OrganizationId == null && c.UserId == userId : c.OrganizationId == organizationId), ct);
    }

    public async Task<IReadOnlyList<LeadScoringRule>> GetScoringRulesAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LeadScoringRules.Where(r => r.OrganizationId == organizationId).ToListAsync(ct);

    public async Task ReplaceScoringRulesAsync(Guid? organizationId, IReadOnlyList<LeadScoringRule> rules, CancellationToken ct = default)
    {
        var existing = await _db.LeadScoringRules.Where(r => r.OrganizationId == organizationId).ToListAsync(ct);
        _db.LeadScoringRules.RemoveRange(existing);
        _db.LeadScoringRules.AddRange(rules);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadAssignmentRule>> GetAssignmentRulesAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LeadAssignmentRules
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    // Every save below looks the row up by id AND organisation. By id alone, a
    // request carrying another organisation's rule id rewrote that organisation's
    // rule — and an id is all a former member, or anyone who has seen one, needs.

    public async Task SaveAssignmentRuleAsync(LeadAssignmentRule rule, CancellationToken ct = default)
    {
        var existing = await _db.LeadAssignmentRules
            .FirstOrDefaultAsync(r => r.Id == rule.Id && r.OrganizationId == rule.OrganizationId, ct);
        if (existing == null)
        {
            if (await _db.LeadAssignmentRules.AnyAsync(r => r.Id == rule.Id, ct)) rule.Id = Guid.NewGuid();
            _db.LeadAssignmentRules.Add(rule);
        }
        else
        {
            existing.Name = rule.Name;
            existing.Kind = rule.Kind;
            existing.Priority = rule.Priority;
            existing.SourceMatch = rule.SourceMatch;
            existing.TerritoryId = rule.TerritoryId;
            existing.QueueId = rule.QueueId;
            existing.MemberUserIds = rule.MemberUserIds;
            existing.LastIndex = rule.LastIndex;
            existing.IsActive = rule.IsActive;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAssignmentRuleAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.LeadAssignmentRules.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId, ct);
        if (row == null) return;
        _db.LeadAssignmentRules.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadTerritory>> GetTerritoriesAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LeadTerritories.Where(t => t.OrganizationId == organizationId).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task SaveTerritoryAsync(LeadTerritory territory, CancellationToken ct = default)
    {
        var existing = await _db.LeadTerritories
            .FirstOrDefaultAsync(t => t.Id == territory.Id && t.OrganizationId == territory.OrganizationId, ct);
        if (existing == null)
        {
            if (await _db.LeadTerritories.AnyAsync(t => t.Id == territory.Id, ct)) territory.Id = Guid.NewGuid();
            _db.LeadTerritories.Add(territory);
        }
        else
        {
            existing.Name = territory.Name;
            existing.Countries = territory.Countries;
            existing.Industries = territory.Industries;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTerritoryAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.LeadTerritories.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId, ct);
        if (row == null) return;
        _db.LeadTerritories.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadQueue>> GetQueuesAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LeadQueues.Where(q => q.OrganizationId == organizationId).OrderBy(q => q.Name).ToListAsync(ct);

    public async Task SaveQueueAsync(LeadQueue queue, CancellationToken ct = default)
    {
        var existing = await _db.LeadQueues
            .FirstOrDefaultAsync(q => q.Id == queue.Id && q.OrganizationId == queue.OrganizationId, ct);
        if (existing == null)
        {
            if (await _db.LeadQueues.AnyAsync(q => q.Id == queue.Id, ct)) queue.Id = Guid.NewGuid();
            _db.LeadQueues.Add(queue);
        }
        else
        {
            existing.Name = queue.Name;
            existing.MemberUserIds = queue.MemberUserIds;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteQueueAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.LeadQueues.FirstOrDefaultAsync(q => q.Id == id && q.OrganizationId == organizationId, ct);
        if (row == null) return;
        _db.LeadQueues.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> MemberUserIdsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        if (organizationId == null) return Array.Empty<Guid>();
        // People who can work a lead: current members, not Viewers. A Viewer cannot
        // log activity, so every lead routed to one breached its response deadline.
        return await _db.OrganizationMembers
            .Where(m => m.OrganizationId == organizationId && m.Role != ACI.Domain.Enums.OrgMemberRole.Viewer)
            .Select(m => m.UserId)
            .ToListAsync(ct);
    }

    public async Task<int> GetSlaHoursAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.LeadOpsSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        return row?.SlaHours is > 0 and <= 168 ? row.SlaHours : 4;
    }

    public async Task SetSlaHoursAsync(Guid? organizationId, int hours, CancellationToken ct = default)
    {
        var row = await _db.LeadOpsSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (row == null)
        {
            _db.LeadOpsSettings.Add(new LeadOpsSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SlaHours = hours,
            });
        }
        else row.SlaHours = hours;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddStatusEventAsync(LeadStatusEvent statusEvent, CancellationToken ct = default)
    {
        _db.LeadStatusEvents.Add(statusEvent);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeadStatusEvent>> GetStatusEventsAsync(Guid leadId, CancellationToken ct = default) =>
        await _db.LeadStatusEvents.Where(e => e.LeadId == leadId).OrderByDescending(e => e.ChangedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<LeadCaptureForm>> GetFormsAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LeadCaptureForms.Where(f => f.OrganizationId == organizationId).OrderByDescending(f => f.CreatedAtUtc).ToListAsync(ct);

    public Task<LeadCaptureForm?> GetFormByTokenAsync(string token, CancellationToken ct = default) =>
        _db.LeadCaptureForms.FirstOrDefaultAsync(f => f.PublicToken == token && f.IsActive, ct);

    public async Task SaveFormAsync(LeadCaptureForm form, CancellationToken ct = default)
    {
        var existing = await _db.LeadCaptureForms.FirstOrDefaultAsync(f => f.Id == form.Id, ct);
        if (existing == null) _db.LeadCaptureForms.Add(form);
        else
        {
            existing.Name = form.Name;
            existing.IsActive = form.IsActive;
            existing.SuccessMessage = form.SuccessMessage;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Lead>> LeadsForReportAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.Leads.AsNoTracking()
            .Where(l => organizationId == null ? l.OrganizationId == null : l.OrganizationId == organizationId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Lead>> OverdueFirstResponseAsync(DateTime utcNow, int take, CancellationToken ct = default) =>
        await _db.Leads
            .Where(l => !l.IsConverted
                && l.FirstResponseAtUtc == null
                && l.SlaNotifiedAtUtc == null
                && l.FirstResponseDueAtUtc != null
                && l.FirstResponseDueAtUtc < utcNow)
            .OrderBy(l => l.FirstResponseDueAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task SaveLeadAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Update(lead);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Lead?> GetLeadAsync(Guid id, CancellationToken ct = default) =>
        _db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<int> IncrementEmailOpensAsync(Guid leadId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead == null) return 0;
        lead.EmailOpenCount += 1;
        if (!lead.ScoreIsManual)
            lead.LeadScore = LeadScoreCalculator.Score(lead, await ScoringOrBuiltin(lead.OrganizationId, ct));
        await _db.SaveChangesAsync(ct);
        return lead.EmailOpenCount;
    }

    /// <summary>
    /// Recompute the automatic score of every open lead, and save the ones that
    /// moved. Returns how many moved.
    /// </summary>
    /// <remarks>
    /// A score used to be recomputed only when its lead was saved, so the
    /// time-based rules never changed on their own: a lead called once kept its
    /// "contacted within 7 days" points for good, and an untouched lead never lost
    /// points for going stale. Leads whose score was set by hand, and converted
    /// leads, are left as they are. Only the score changes — UpdatedAtUtc stays,
    /// since nobody edited the lead.
    /// </remarks>
    public async Task<int> RescoreOpenLeadsAsync(DateTime utcNow, CancellationToken ct = default)
    {
        const int page = 500;
        var rulesByOrganization = new Dictionary<Guid, IReadOnlyList<LeadScoringRule>>();
        var moved = 0;
        for (var skip = 0; ; skip += page)
        {
            // Neither filter column changes below, so the pages stay put.
            var leads = await _db.Leads
                .Where(l => !l.ScoreIsManual && !l.IsConverted)
                .OrderBy(l => l.Id)
                .Skip(skip)
                .Take(page)
                .ToListAsync(ct);

            foreach (var lead in leads)
            {
                var key = lead.OrganizationId ?? Guid.Empty;
                if (!rulesByOrganization.TryGetValue(key, out var rules))
                    rulesByOrganization[key] = rules = await GetScoringRulesAsync(lead.OrganizationId, ct);
                var score = LeadScoreCalculator.Score(lead, rules, utcNow);
                if (lead.LeadScore == score) continue;
                lead.LeadScore = score;
                moved++;
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            if (leads.Count < page) return moved;
        }
    }

    private async Task<IReadOnlyList<LeadScoringRule>> ScoringOrBuiltin(Guid? organizationId, CancellationToken ct)
    {
        var rules = await GetScoringRulesAsync(organizationId, ct);
        return rules.Count == 0 ? LeadScoreCalculator.BuiltinRules() : rules;
    }
}
