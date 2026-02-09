using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages contacts in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the ContactsController.
    /// </summary>
    public ContactsController(IContactService contactService, ICurrentUserService currentUser)
    {
        _contactService = contactService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves contacts with pagination and optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20, max 100.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="includeArchived">Whether to include archived contacts. Default is false.</param>
    /// <param name="companyId">Optional company ID to filter contacts by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of contacts.</returns>
    /// <response code="200">Returns the paginated list of contacts.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ContactDto>>> GetContacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool includeArchived = false,
        [FromQuery] Guid? companyId = null, 
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        
        var result = await _contactService.GetContactsPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            page,
            pageSize,
            search,
            includeArchived,
            companyId, 
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all contacts for the authenticated user (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="includeArchived">Whether to include archived contacts. Default is false.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of contacts.</returns>
    /// <response code="200">Returns the list of contacts.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> GetAllContacts(
        [FromQuery] bool includeArchived = false, 
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _contactService.GetContactsAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            includeArchived, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Searches contacts by name, email, or phone (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="q">The search query.</param>
    /// <param name="includeArchived">Whether to include archived contacts. Default is false.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching contacts.</returns>
    /// <response code="200">Returns the list of matching contacts.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> Search(
        [FromQuery] string q, 
        [FromQuery] bool includeArchived = false, 
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _contactService.SearchAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            q ?? "", 
            includeArchived, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific contact by ID.
    /// </summary>
    /// <param name="id">The contact ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The contact if found.</returns>
    /// <response code="200">Returns the contact.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Contact not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.GetByIdAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new contact.
    /// </summary>
    /// <param name="request">The contact creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created contact.</returns>
    /// <response code="200">Contact created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="409">A contact with this email already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactDto>> Create(
        [FromBody] CreateContactRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing contact.
    /// </summary>
    /// <param name="id">The contact ID to update.</param>
    /// <param name="request">The contact update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated contact.</returns>
    /// <response code="200">Contact updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Contact not found.</response>
    /// <response code="409">A contact with this email already exists.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactDto>> Update(
        Guid id, 
        [FromBody] UpdateContactRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a contact.
    /// </summary>
    /// <param name="id">The contact ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Contact deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Contact not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.DeleteAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Archives a contact (soft delete).
    /// </summary>
    /// <param name="id">The contact ID to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Contact archived successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Contact not found.</response>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.ArchiveAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Unarchives a contact.
    /// </summary>
    /// <param name="id">The contact ID to unarchive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Contact unarchived successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Contact not found.</response>
    [HttpPost("{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Unarchive(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _contactService.UnarchiveAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Checks if an email address is already used by another contact.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="excludeId">Optional contact ID to exclude from the check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the email exists.</returns>
    /// <response code="200">Returns whether the email exists.</response>
    /// <response code="400">Email is required.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("check-email")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> CheckEmail(
        [FromQuery] string email, 
        [FromQuery] Guid? excludeId = null, 
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        if (string.IsNullOrWhiteSpace(email))
        {
            return Problem(
                detail: "Email is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
        
        var exists = await _contactService.EmailExistsAsync(
            email.Trim(), 
            _currentUser.CurrentOrganizationId, 
            excludeId, 
            ct);
        
        return Ok(new { exists });
    }
}
