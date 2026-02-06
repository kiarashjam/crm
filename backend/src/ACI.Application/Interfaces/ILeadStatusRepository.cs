using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ILeadStatusRepository
{
    Task<IReadOnlyList<LeadStatus>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<LeadStatus?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<LeadStatus> AddAsync(LeadStatus entity, CancellationToken ct = default);
    Task<LeadStatus?> UpdateAsync(LeadStatus entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
