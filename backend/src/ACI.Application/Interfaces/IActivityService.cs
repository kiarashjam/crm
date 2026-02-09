using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing activities in the CRM system.
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// Gets paginated activities for a user within an organization with optional search.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="activityType">Optional activity type filter (call, meeting, email, note, task, follow_up, deadline, video, demo).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of activities.</returns>
    Task<PagedResult<ActivityDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        string? activityType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all activities for a user within an organization.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities.</returns>
    Task<IReadOnlyList<ActivityDto>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets activities linked to a specific contact.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the contact.</returns>
    Task<IReadOnlyList<ActivityDto>> GetByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets activities linked to a specific deal.
    /// </summary>
    /// <param name="dealId">The deal ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the deal.</returns>
    Task<IReadOnlyList<ActivityDto>> GetByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets activities linked to a specific lead.
    /// </summary>
    /// <param name="leadId">The lead ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the lead.</returns>
    Task<IReadOnlyList<ActivityDto>> GetByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new activity.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">The activity creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created activity, or a failure result.</returns>
    Task<Result<ActivityDto>> CreateAsync(Guid userId, Guid? organizationId, CreateActivityRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing activity.
    /// </summary>
    /// <param name="id">The activity ID to update.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">The activity update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated activity, or a failure result.</returns>
    Task<Result<ActivityDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateActivityRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an activity.
    /// </summary>
    /// <param name="id">The activity ID to delete.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success or failure result.</returns>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets activity counts per user for all members in the organization.
    /// Used by team management pages to display real activity metrics (HP-3).
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary mapping user IDs to their activity counts.</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetOrgMemberActivityCountsAsync(Guid? organizationId, CancellationToken ct = default);
}
