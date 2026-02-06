using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ILeadStatusService
{
    Task<Result<IReadOnlyList<LeadStatusDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<LeadStatusDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default);
    Task<Result<LeadStatusDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default);
    Task<Result<LeadStatusDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);
}
