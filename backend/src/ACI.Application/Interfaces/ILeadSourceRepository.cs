using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ILeadSourceRepository
{
    Task<IReadOnlyList<LeadSource>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<LeadSource?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<LeadSource> AddAsync(LeadSource entity, CancellationToken ct = default);
    Task<LeadSource?> UpdateAsync(LeadSource entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
