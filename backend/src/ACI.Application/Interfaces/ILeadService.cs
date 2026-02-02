using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ILeadService
{
    Task<IReadOnlyList<LeadDto>> GetLeadsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeadDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<LeadDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<LeadDto?> CreateAsync(Guid userId, CreateLeadRequest request, CancellationToken ct = default);
    Task<LeadDto?> UpdateAsync(Guid id, Guid userId, UpdateLeadRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ConvertLeadResult?> ConvertAsync(Guid leadId, Guid userId, ConvertLeadRequest request, CancellationToken ct = default);
}
