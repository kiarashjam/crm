using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// How contacts are related to each other: who is whose spouse or partner (the couples
/// package), parent, child, sibling, and so on.
/// </summary>
/// <remarks>
/// Each relationship is recorded once and read from either side: a parent seen from the
/// child is listed as their parent, and the child seen from the parent as their child.
/// A contact has at most one spouse or partner. The lead routes work on the lead's own
/// contact, which every lead with a name and an email has.
/// </remarks>
[ApiController]
[Authorize]
[Produces("application/json")]
public class ContactRelationshipsController : ControllerBase
{
    private readonly IContactRelationshipService _service;
    private readonly ICurrentUserService _currentUser;

    public ContactRelationshipsController(IContactRelationshipService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>A contact's relationships, spouse or partner first.</summary>
    /// <response code="200">The relationships. Empty when none are recorded.</response>
    /// <response code="404">Contact not found.</response>
    [HttpGet("api/contacts/{contactId:guid}/relationships")]
    [ProducesResponseType(typeof(ContactRelationshipListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactRelationshipListDto>> List(Guid contactId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _service.ListAsync(contactId, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToActionResult();
    }

    /// <summary>Records how another contact is related to this one.</summary>
    /// <response code="200">The relationship, as seen from this contact.</response>
    /// <response code="400">Unknown type, or the other contact is this one, archived, or not in this workspace.</response>
    /// <response code="404">Contact not found.</response>
    /// <response code="409">The two are already related, or one of them already has a spouse or partner.</response>
    [HttpPost("api/contacts/{contactId:guid}/relationships")]
    [ProducesResponseType(typeof(ContactRelationshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactRelationshipDto>> Add(
        Guid contactId, [FromBody] SetContactRelationshipRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _service.AddAsync(contactId, userId, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Changes the type of a relationship, or who it is with.</summary>
    /// <response code="200">The relationship, as seen from this contact.</response>
    /// <response code="404">Contact, or relationship on it, not found.</response>
    [HttpPut("api/contacts/{contactId:guid}/relationships/{relationshipId:guid}")]
    [ProducesResponseType(typeof(ContactRelationshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactRelationshipDto>> Update(
        Guid contactId, Guid relationshipId, [FromBody] SetContactRelationshipRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _service.UpdateAsync(contactId, relationshipId, userId, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Removes a relationship, from both contacts.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="404">Contact, or relationship on it, not found.</response>
    [HttpDelete("api/contacts/{contactId:guid}/relationships/{relationshipId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(Guid contactId, Guid relationshipId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _service.RemoveAsync(contactId, relationshipId, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }

    /// <summary>The relationships of a lead's contact. <c>contactId</c> is null when the lead has none yet.</summary>
    /// <response code="200">The relationships, and the lead's contact.</response>
    /// <response code="404">Lead not found.</response>
    [HttpGet("api/leads/{leadId:guid}/relationships")]
    [ProducesResponseType(typeof(ContactRelationshipListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactRelationshipListDto>> ListForLead(Guid leadId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var contact = await _service.LeadContactIdAsync(leadId, userId, _currentUser.CurrentOrganizationId, ct);
        if (contact.IsFailure) return Result.Failure<ContactRelationshipListDto>(contact.Error).ToActionResult();
        if (contact.Value is not { } contactId) return Ok(new ContactRelationshipListDto(null, []));
        var result = await _service.ListAsync(contactId, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToActionResult();
    }

    /// <summary>Records how a contact is related to this lead.</summary>
    /// <response code="400">As for a contact, or the lead has no name and email yet.</response>
    [HttpPost("api/leads/{leadId:guid}/relationships")]
    [ProducesResponseType(typeof(ContactRelationshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactRelationshipDto>> AddForLead(
        Guid leadId, [FromBody] SetContactRelationshipRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var contact = await LeadContactAsync(leadId, userId, ct);
        if (contact.IsFailure) return Result.Failure<ContactRelationshipDto>(contact.Error).ToActionResult();
        var result = await _service.AddAsync(contact.Value, userId, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Changes one of this lead's relationships.</summary>
    [HttpPut("api/leads/{leadId:guid}/relationships/{relationshipId:guid}")]
    [ProducesResponseType(typeof(ContactRelationshipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContactRelationshipDto>> UpdateForLead(
        Guid leadId, Guid relationshipId, [FromBody] SetContactRelationshipRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var contact = await LeadContactAsync(leadId, userId, ct);
        if (contact.IsFailure) return Result.Failure<ContactRelationshipDto>(contact.Error).ToActionResult();
        var result = await _service.UpdateAsync(contact.Value, relationshipId, userId, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Removes one of this lead's relationships.</summary>
    [HttpDelete("api/leads/{leadId:guid}/relationships/{relationshipId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveForLead(Guid leadId, Guid relationshipId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var contact = await LeadContactAsync(leadId, userId, ct);
        if (contact.IsFailure) return ((Result)contact.Error).ToNoContentResult();
        var result = await _service.RemoveAsync(contact.Value, relationshipId, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }

    private async Task<Result<Guid>> LeadContactAsync(Guid leadId, Guid userId, CancellationToken ct)
    {
        var contact = await _service.LeadContactIdAsync(leadId, userId, _currentUser.CurrentOrganizationId, ct);
        if (contact.IsFailure) return Result.Failure<Guid>(contact.Error);
        return contact.Value is { } id
            ? Result.Success(id)
            : Result.Failure<Guid>(DomainErrors.ContactRelationship.LeadHasNoContact);
    }
}
