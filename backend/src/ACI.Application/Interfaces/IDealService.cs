using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing deals in the CRM system.
/// </summary>
public interface IDealService
{
    /// <summary>
    /// Retrieves deals with pagination and optional search.
    /// </summary>
    Task<PagedResult<DealDto>> GetDealsPagedAsync(Guid userId, Guid? organizationId, int page = 1, int pageSize = 20, string? search = null, Guid? companyId = null, Guid? contactId = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all deals for a user within an organization.
    /// </summary>
    Task<IReadOnlyList<DealDto>> GetDealsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Deals still owing something (open, or lost with no reason), the caller's own (see
    /// WorkScope) or the team's: at most <paramref name="take"/>, and whether there were more.
    /// </summary>
    Task<(IReadOnlyList<DealDto> Items, bool More)> GetOpenWorkAsync(Guid userId, Guid? organizationId, bool mineOnly, int take, CancellationToken ct = default);

    /// <summary>
    /// Searches deals by name or value.
    /// </summary>
    Task<IReadOnlyList<DealDto>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null);

    /// <summary>
    /// Retrieves a deal by its ID.
    /// </summary>
    Task<Result<DealDto>> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new deal.
    /// </summary>
    Task<Result<DealDto>> CreateAsync(Guid userId, Guid? organizationId, CreateDealRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing deal.
    /// </summary>
    Task<Result<DealDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateDealRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a deal.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Keeps one deal and folds a duplicate of it into it.</summary>
    Task<Result> MergeAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// The stages the deal has been in, newest first: when it came into each, from where,
    /// who moved it, and when it left again.
    /// </summary>
    Task<Result<IReadOnlyList<DealStageChangeDto>>> GetStageHistoryAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
