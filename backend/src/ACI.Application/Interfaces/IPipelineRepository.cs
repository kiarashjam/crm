using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IPipelineRepository
{
    Task<IReadOnlyList<Pipeline>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<Pipeline?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<Pipeline> AddAsync(Pipeline pipeline, CancellationToken ct = default);
    Task<Pipeline?> UpdateAsync(Pipeline pipeline, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
