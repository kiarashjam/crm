using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing companies in the CRM system.
/// </summary>
public interface ICompanyService
{
    /// <summary>
    /// Gets companies with pagination and optional search.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of companies.</returns>
    Task<PagedResult<CompanyDto>> GetCompaniesPagedAsync(Guid userId, Guid? organizationId, int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all companies for a user within an organization.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of companies.</returns>
    Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Searches companies by name or domain.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching companies.</returns>
    Task<IReadOnlyList<CompanyDto>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default);

    /// <summary>
    /// Gets a company by its ID.
    /// </summary>
    /// <param name="id">The company ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The company if found, or a failure result.</returns>
    Task<Result<CompanyDto>> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new company.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">The company creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created company, or a failure result.</returns>
    Task<Result<CompanyDto>> CreateAsync(Guid userId, Guid? organizationId, CreateCompanyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing company.
    /// </summary>
    /// <param name="id">The company ID to update.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">The company update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated company, or a failure result.</returns>
    Task<Result<CompanyDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateCompanyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets per-company statistics (contact count, deal count, total deal value).
    /// HP-7: Server-side aggregation to avoid fetching all contacts/deals on the frontend.
    /// </summary>
    Task<IReadOnlyList<CompanyStatsItemDto>> GetCompanyStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a company.
    /// </summary>
    /// <param name="id">The company ID to delete.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success or failure result.</returns>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
