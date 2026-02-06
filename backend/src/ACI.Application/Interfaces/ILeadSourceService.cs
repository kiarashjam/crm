using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ILeadSourceService
{
    Task<Result<IReadOnlyList<LeadSourceDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<LeadSourceDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<Result<LeadSourceDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default);
    Task<Result<LeadSourceDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
