using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing contacts in the CRM system.
/// </summary>
public interface IContactService
{
    /// <summary>
    /// Retrieves contacts with pagination and optional search.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the contacts.</param>
    /// <param name="organizationId">The optional organization ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="includeArchived">Whether to include archived contacts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of contacts.</returns>
    Task<PagedResult<ContactDto>> GetContactsPagedAsync(Guid userId, Guid? organizationId, int page = 1, int pageSize = 20, string? search = null, bool includeArchived = false, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all contacts for a user within an organization.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the contacts.</param>
    /// <param name="organizationId">The optional organization ID to filter by.</param>
    /// <param name="includeArchived">Whether to include archived contacts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of contacts.</returns>
    Task<IReadOnlyList<ContactDto>> GetContactsAsync(Guid userId, Guid? organizationId, bool includeArchived = false, CancellationToken ct = default);

    /// <summary>
    /// Searches contacts by name, email, or phone.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the search.</param>
    /// <param name="organizationId">The optional organization ID to filter by.</param>
    /// <param name="query">The search query.</param>
    /// <param name="includeArchived">Whether to include archived contacts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching contacts.</returns>
    Task<IReadOnlyList<ContactDto>> SearchAsync(Guid userId, Guid? organizationId, string query, bool includeArchived = false, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a contact by its ID.
    /// </summary>
    /// <param name="id">The contact ID.</param>
    /// <param name="userId">The ID of the user requesting the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The contact if found, or a NotFound error.</returns>
    Task<Result<ContactDto>> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new contact.
    /// </summary>
    /// <param name="userId">The ID of the user creating the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="request">The contact creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created contact or an error.</returns>
    Task<Result<ContactDto>> CreateAsync(Guid userId, Guid? organizationId, CreateContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing contact.
    /// </summary>
    /// <param name="id">The contact ID to update.</param>
    /// <param name="userId">The ID of the user updating the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="request">The contact update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated contact or an error.</returns>
    Task<Result<ContactDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a contact.
    /// </summary>
    /// <param name="id">The contact ID to delete.</param>
    /// <param name="userId">The ID of the user deleting the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or an error.</returns>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Archives a contact (soft delete).
    /// </summary>
    /// <param name="id">The contact ID to archive.</param>
    /// <param name="userId">The ID of the user archiving the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or an error.</returns>
    Task<Result> ArchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Unarchives a contact.
    /// </summary>
    /// <param name="id">The contact ID to unarchive.</param>
    /// <param name="userId">The ID of the user unarchiving the contact.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or an error.</returns>
    Task<Result> UnarchiveAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Checks if an email already exists for a contact.
    /// </summary>
    /// <param name="email">The email to check.</param>
    /// <param name="organizationId">The optional organization ID.</param>
    /// <param name="excludeContactId">Contact ID to exclude from the check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the email exists, false otherwise.</returns>
    Task<bool> EmailExistsAsync(string email, Guid? organizationId, Guid? excludeContactId = null, CancellationToken ct = default);
}
