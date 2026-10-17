using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public interface ILeadAutomation
{
    Task<Lead?> FindOpenDuplicateAsync(Guid? organizationId, string email, Guid? exceptId, CancellationToken ct = default);
    Task PrepareNewAsync(Lead lead, CancellationToken ct = default);
    Task RecordStatusAsync(Lead lead, string? fromStatus, string toStatus, Guid actorId, CancellationToken ct = default);
    Task RescoreAsync(Lead lead, CancellationToken ct = default);
    Task MarkFirstResponseAsync(Guid leadId, Guid userId, Guid? organizationId, string activityType, CancellationToken ct = default);
}

public sealed class LeadAutomation : ILeadAutomation
{
    private static readonly HashSet<string> ResponseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "call", "meeting", "email", "video", "demo",
    };

    private readonly ILeadOpsRepository _ops;
    private readonly ICompanyRepository _companies;

    public LeadAutomation(ILeadOpsRepository ops, ICompanyRepository companies)
    {
        _ops = ops;
        _companies = companies;
    }

    public Task<Lead?> FindOpenDuplicateAsync(Guid? organizationId, string email, Guid? exceptId, CancellationToken ct = default) =>
        _ops.FindOpenByEmailAsync(organizationId, email, exceptId, ct);

    public async Task PrepareNewAsync(Lead lead, CancellationToken ct = default)
    {
        await EnrichAsync(lead, ct);
        if (lead.AssignedToUserId == null)
            await AssignAsync(lead, ct);
        if (!lead.ScoreIsManual)
            await RescoreAsync(lead, ct);
        var hours = await _ops.GetSlaHoursAsync(lead.OrganizationId, ct);
        lead.FirstResponseDueAtUtc ??= lead.CreatedAtUtc.AddHours(hours);
    }

    public async Task RecordStatusAsync(Lead lead, string? fromStatus, string toStatus, Guid actorId, CancellationToken ct = default)
    {
        if (string.Equals(fromStatus, toStatus, StringComparison.OrdinalIgnoreCase) && fromStatus != null)
            return;
        await _ops.AddStatusEventAsync(new LeadStatusEvent
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            OrganizationId = lead.OrganizationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedByUserId = actorId,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);
    }

    public async Task RescoreAsync(Lead lead, CancellationToken ct = default)
    {
        if (lead.ScoreIsManual) return;
        var rules = await _ops.GetScoringRulesAsync(lead.OrganizationId, ct);
        lead.LeadScore = LeadScoreCalculator.Score(lead, rules);
    }

    public async Task MarkFirstResponseAsync(Guid leadId, Guid userId, Guid? organizationId, string activityType, CancellationToken ct = default)
    {
        if (!ResponseTypes.Contains(activityType)) return;
        var lead = await _ops.GetLeadAsync(leadId, ct);
        if (lead == null) return;
        if (organizationId != null && lead.OrganizationId != organizationId) return;

        var now = DateTime.UtcNow;
        lead.LastContactedAt = now;
        if (lead.FirstResponseAtUtc == null)
        {
            lead.FirstResponseAtUtc = now;
            lead.SlaBreached = false;
        }
        await RescoreAsync(lead, ct);
        await _ops.SaveLeadAsync(lead, ct);
    }

    private async Task EnrichAsync(Lead lead, CancellationToken ct)
    {
        if (lead.CompanyId != null) return;
        if (!LeadDomainEnricher.TryCompanyFromEmail(lead.Email, out var domain, out var companyName)) return;

        var company = await _ops.FindCompanyByDomainAsync(lead.OrganizationId, domain, ct);
        if (company == null)
        {
            company = await _companies.AddAsync(new Company
            {
                Id = Guid.NewGuid(),
                UserId = lead.UserId,
                OrganizationId = lead.OrganizationId,
                Name = companyName,
                Domain = domain,
                Website = "https://" + domain,
                CreatedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        lead.CompanyId = company.Id;
        lead.EnrichedDomain = domain;
        if (string.IsNullOrWhiteSpace(lead.Industry) && !string.IsNullOrWhiteSpace(company.Industry))
            lead.Industry = company.Industry;
        if (string.IsNullOrWhiteSpace(lead.CompanySize) && !string.IsNullOrWhiteSpace(company.Size))
            lead.CompanySize = company.Size;
    }

    private async Task AssignAsync(Lead lead, CancellationToken ct)
    {
        var rules = (await _ops.GetAssignmentRulesAsync(lead.OrganizationId, ct))
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToList();
        if (rules.Count == 0) return;

        var territories = await _ops.GetTerritoriesAsync(lead.OrganizationId, ct);
        var queues = await _ops.GetQueuesAsync(lead.OrganizationId, ct);
        var members = await _ops.MemberUserIdsAsync(lead.OrganizationId, ct);

        foreach (var rule in rules)
        {
            if (!RuleMatches(rule, lead, territories)) continue;
            var pool = Pool(rule, queues, members);
            if (pool.Count == 0) continue;

            var index = Math.Abs(rule.LastIndex) % pool.Count;
            lead.AssignedToUserId = pool[index];
            rule.LastIndex = index + 1;
            if (rule.QueueId != null) lead.QueueId = rule.QueueId;
            if (rule.TerritoryId != null) lead.TerritoryId = rule.TerritoryId;
            await _ops.SaveAssignmentRuleAsync(rule, ct);
            return;
        }
    }

    private static bool RuleMatches(LeadAssignmentRule rule, Lead lead, IReadOnlyList<LeadTerritory> territories)
    {
        var kind = rule.Kind.Trim().ToLowerInvariant();
        if (kind is "roundrobin" or "round-robin") return true;
        if (kind == "source")
            return !string.IsNullOrWhiteSpace(rule.SourceMatch)
                && (lead.Source ?? "").Contains(rule.SourceMatch.Trim(), StringComparison.OrdinalIgnoreCase);
        if (kind != "territory" || rule.TerritoryId == null) return false;
        var territory = territories.FirstOrDefault(t => t.Id == rule.TerritoryId);
        if (territory == null) return false;
        var countries = Split(territory.Countries);
        var industries = Split(territory.Industries);
        var countryHit = countries.Count > 0 && countries.Contains(lead.Country?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);
        var industryHit = industries.Count > 0 && industries.Contains(lead.Industry?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);
        return countryHit || industryHit;
    }

    /// <summary>
    /// Who this rule may assign to: its queue, else its own list, else everyone —
    /// always narrowed to people who can still work a lead (current members who are
    /// not Viewers). Queue and rule lists are saved ids and go stale: someone who
    /// left the workspace stayed in the rotation, kept getting every second lead,
    /// and was sent each lead's name in the response-deadline alert.
    /// </summary>
    internal static List<Guid> Pool(LeadAssignmentRule rule, IReadOnlyList<LeadQueue> queues, IReadOnlyList<Guid> members)
    {
        var eligible = members.ToHashSet();
        if (rule.QueueId != null)
        {
            var queue = queues.FirstOrDefault(q => q.Id == rule.QueueId);
            var fromQueue = ParseIds(queue?.MemberUserIds).Where(eligible.Contains).Distinct().ToList();
            if (fromQueue.Count > 0) return fromQueue;
        }
        var listed = ParseIds(rule.MemberUserIds).Where(eligible.Contains).Distinct().ToList();
        if (listed.Count > 0) return listed;
        return members.Distinct().ToList();
    }

    private static List<string> Split(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static List<Guid> ParseIds(string? csv)
    {
        var list = new List<Guid>();
        foreach (var part in Split(csv))
            if (Guid.TryParse(part, out var id)) list.Add(id);
        return list;
    }

    public static string JoinIds(IEnumerable<Guid> ids) => string.Join(",", ids);
}
