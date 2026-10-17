using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ILeadOpsRepository
{
    Task<Lead?> FindOpenByEmailAsync(Guid? organizationId, string email, Guid? exceptId, CancellationToken ct = default);
    Task<Company?> FindCompanyByDomainAsync(Guid? organizationId, string domain, CancellationToken ct = default);
    Task<IReadOnlyList<LeadScoringRule>> GetScoringRulesAsync(Guid? organizationId, CancellationToken ct = default);
    Task ReplaceScoringRulesAsync(Guid? organizationId, IReadOnlyList<LeadScoringRule> rules, CancellationToken ct = default);
    Task<IReadOnlyList<LeadAssignmentRule>> GetAssignmentRulesAsync(Guid? organizationId, CancellationToken ct = default);
    Task SaveAssignmentRuleAsync(LeadAssignmentRule rule, CancellationToken ct = default);
    Task DeleteAssignmentRuleAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<LeadTerritory>> GetTerritoriesAsync(Guid? organizationId, CancellationToken ct = default);
    Task SaveTerritoryAsync(LeadTerritory territory, CancellationToken ct = default);
    Task DeleteTerritoryAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<LeadQueue>> GetQueuesAsync(Guid? organizationId, CancellationToken ct = default);
    Task SaveQueueAsync(LeadQueue queue, CancellationToken ct = default);
    Task DeleteQueueAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> MemberUserIdsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<int> GetSlaHoursAsync(Guid? organizationId, CancellationToken ct = default);
    Task SetSlaHoursAsync(Guid? organizationId, int hours, CancellationToken ct = default);
    Task AddStatusEventAsync(LeadStatusEvent statusEvent, CancellationToken ct = default);
    Task<IReadOnlyList<LeadStatusEvent>> GetStatusEventsAsync(Guid leadId, CancellationToken ct = default);
    Task<IReadOnlyList<LeadCaptureForm>> GetFormsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<LeadCaptureForm?> GetFormByTokenAsync(string token, CancellationToken ct = default);
    Task SaveFormAsync(LeadCaptureForm form, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> LeadsForReportAsync(Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> OverdueFirstResponseAsync(DateTime utcNow, int take, CancellationToken ct = default);
    Task SaveLeadAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> GetLeadAsync(Guid id, CancellationToken ct = default);
    Task<int> IncrementEmailOpensAsync(Guid leadId, CancellationToken ct = default);
}
