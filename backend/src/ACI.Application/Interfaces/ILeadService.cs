using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing leads in the CRM system.
/// </summary>
public interface ILeadService
{
    /// <summary>
    /// Retrieves leads with pagination and optional search.
    /// </summary>
    Task<PagedResult<LeadDto>> GetLeadsPagedAsync(Guid userId, Guid? organizationId, int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all leads for a user within an organization.
    /// </summary>
    Task<IReadOnlyList<LeadDto>> GetLeadsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Searches leads by name, email, or company.
    /// </summary>
    Task<IReadOnlyList<LeadDto>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a lead by its ID.
    /// </summary>
    Task<Result<LeadDto>> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new lead.
    /// </summary>
    Task<Result<LeadDto>> CreateAsync(Guid userId, Guid? organizationId, CreateLeadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing lead.
    /// </summary>
    Task<Result<LeadDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateLeadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a lead.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Converts a lead to a contact and/or deal.
    /// </summary>
    Task<Result<ConvertLeadResult>> ConvertAsync(Guid leadId, Guid userId, Guid? organizationId, ConvertLeadRequest request, CancellationToken ct = default);
}
