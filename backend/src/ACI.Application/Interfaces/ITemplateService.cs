using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing copy templates in the CRM system.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Gets paginated templates available to a user with optional search and category filter.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of templates.</returns>
    Task<PagedResult<TemplateDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        string? category = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all templates available to a user (own, shared, and system templates).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of available templates.</returns>
    Task<IReadOnlyList<TemplateDto>> GetUserTemplatesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The template ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The template if found and accessible, or a failure result.</returns>
    Task<Result<TemplateDto>> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">The template creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created template, or a failure result.</returns>
    Task<Result<TemplateDto>> CreateAsync(Guid userId, Guid? organizationId, CreateTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The template ID to update.</param>
    /// <param name="request">The template update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated template, or a failure result.</returns>
    Task<Result<TemplateDto>> UpdateAsync(Guid userId, Guid id, UpdateTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a template.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The template ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success or failure result.</returns>
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Increments the use count for a template.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task IncrementUseCountAsync(Guid id, CancellationToken ct = default);
}
