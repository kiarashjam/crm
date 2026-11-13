using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Tags: the workspace's list of them, each with a colour, and the tags on its leads,
/// contacts, companies, deals and members. Anyone who edits records makes tags and puts
/// them on; renaming, recolouring or deleting one takes the right to manage settings.
/// </summary>
[ApiController]
[Route("api/tags")]
[Authorize]
[Produces("application/json")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tags;
    private readonly ICurrentUserService _currentUser;

    public TagsController(ITagService tags, ICurrentUserService currentUser)
    {
        _tags = tags;
        _currentUser = currentUser;
    }

    /// <summary>The workspace's tags, by name, with how many records carry each.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> List(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _tags.ListAsync(userId, _currentUser.CurrentOrganizationId, ct));
    }

    /// <summary>Adds a tag. Its name is the workspace's alone, ignoring letter case.</summary>
    /// <response code="200">The tag.</response>
    /// <response code="400">No name, or not one of the colours.</response>
    /// <response code="409">The workspace already has a tag of that name.</response>
    [Requires(MemberPermissions.EditRecords)]
    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagDto>> Create([FromBody] CreateTagRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.CreateAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>Renames or recolours a tag, on every record that carries it.</summary>
    /// <response code="200">The tag.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="404">Not a tag of this workspace.</response>
    /// <response code="409">Another tag has that name.</response>
    [Requires(MemberPermissions.ManageSettings, What = "rename or recolour tags")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TagDto>> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.UpdateAsync(userId, _currentUser.CurrentOrganizationId, id, request, ct)).ToActionResult();
    }

    /// <summary>Deletes a tag and takes it off every record.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="404">Not a tag of this workspace.</response>
    [Requires(MemberPermissions.ManageSettings, What = "delete tags")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.DeleteAsync(userId, _currentUser.CurrentOrganizationId, id, ct)).ToNoContentResult();
    }

    /// <summary>The ids of the tags one record carries.</summary>
    /// <response code="200">The tag ids.</response>
    /// <response code="404">Not a record the caller can see.</response>
    [HttpGet("records")]
    [ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> RecordTags([FromQuery] string? entityType, [FromQuery] Guid recordId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.RecordTagsAsync(userId, _currentUser.CurrentOrganizationId, entityType, recordId, ct)).ToActionResult();
    }

    /// <summary>Sets every tag one record carries: those left out are taken off.</summary>
    /// <response code="200">The record's tag ids.</response>
    /// <response code="404">Not a record the caller can see, or a tag not of this workspace.</response>
    // A member's tags are club work, any other record's records.edit: the service asks for the one the kind needs.
    [Requires(MemberPermissions.EditRecords, MemberPermissions.ClubMembers, Any = true)]
    [HttpPut("records")]
    [ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> SetRecordTags([FromBody] SetRecordTagsRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.SetRecordTagsAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>The tag ids of many records of one kind, keyed by record, for a list. Only records the caller can see answer.</summary>
    [HttpPost("records/batch")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>> RecordTagsBatch(
        [FromBody] RecordTagsBatchRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.RecordTagsBatchAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Puts tags on, and takes tags off, many records of one kind at once: at most 1,000
    /// records and 20 tags. Records the caller cannot see are counted and left alone.
    /// </summary>
    /// <response code="200">What changed.</response>
    /// <response code="400">Too many records or tags, or not a kind of record that carries tags.</response>
    /// <response code="404">A tag not of this workspace.</response>
    [Requires(MemberPermissions.EditRecords, MemberPermissions.ClubMembers, Any = true)]
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkTagResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkTagResult>> Bulk([FromBody] BulkTagRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _tags.BulkAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }
}
