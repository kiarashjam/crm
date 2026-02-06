using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages organizations and their members.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentUserService _currentUser;

    public OrganizationsController(IOrganizationService organizationService, ICurrentUserService currentUser)
    {
        _organizationService = organizationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lists all organizations the current user is a member of.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organizations.</returns>
    /// <response code="200">Returns the list of organizations.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> ListMy(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _organizationService.ListMyOrganizationsAsync(userId.Value, ct);
        return Ok(list);
    }

    /// <summary>
    /// Gets a specific organization by ID.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organization details.</returns>
    /// <response code="200">Returns the organization.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    /// <response code="404">Organization not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Get(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GetOrganizationAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new organization with the current user as owner.
    /// </summary>
    /// <param name="request">Organization creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created organization.</returns>
    /// <response code="201">Organization created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.CreateOrganizationAsync(userId.Value, request, ct);
        
        if (result.IsFailure)
            return result.ToActionResult();
        
        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Gets all members of an organization.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organization members.</returns>
    /// <response code="200">Returns the list of members.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<OrgMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrgMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GetMembersAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates a member's role in the organization.
    /// </summary>
    /// <remarks>
    /// Only the organization owner can update member roles.
    /// The owner's own role cannot be changed.
    /// To transfer ownership, use a separate endpoint.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="memberUserId">The member's user ID.</param>
    /// <param name="request">The new role information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Role updated successfully.</response>
    /// <response code="400">Invalid request or cannot change owner's role.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Member not found.</response>
    [HttpPut("{id:guid}/members/{memberUserId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateMemberRole(Guid id, Guid memberUserId, [FromBody] UpdateMemberRoleRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.UpdateMemberRoleAsync(id, userId.Value, memberUserId, request.Role, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Removes a member from the organization.
    /// </summary>
    /// <remarks>
    /// Only the organization owner can remove members.
    /// The owner cannot remove themselves.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="memberUserId">The member's user ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Member removed successfully.</response>
    /// <response code="400">Cannot remove owner.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Member not found.</response>
    [HttpDelete("{id:guid}/members/{memberUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberUserId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.RemoveMemberAsync(id, userId.Value, memberUserId, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Gets webhook configuration for the organization.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook configuration information.</returns>
    /// <response code="200">Returns webhook info.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    /// <response code="404">Organization not found.</response>
    [HttpGet("{id:guid}/webhook")]
    [ProducesResponseType(typeof(WebhookInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookInfoDto>> GetWebhookInfo(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GetWebhookInfoAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Generates a new webhook API key for the organization.
    /// </summary>
    /// <remarks>
    /// Only the organization owner or manager can generate API keys.
    /// Generating a new key invalidates any previous key.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new API key.</returns>
    /// <response code="200">Returns the new API key.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not owner or manager.</response>
    /// <response code="404">Organization not found.</response>
    [HttpPost("{id:guid}/webhook/regenerate")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> GenerateWebhookApiKey(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GenerateWebhookApiKeyAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }
}
