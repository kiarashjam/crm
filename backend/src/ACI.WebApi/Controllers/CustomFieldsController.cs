using System.Text.Json;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACI.Domain.Common;
using ACI.WebApi.Filters;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Custom fields: the workspace's definitions, and each record's values. Owners and
/// managers change definitions; anyone who can edit a record fills in its values.
/// </summary>
[ApiController]
[Route("api/custom-fields")]
[Authorize]
[Produces("application/json")]
public class CustomFieldsController : ControllerBase
{
    private readonly ICustomFieldService _customFields;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the CustomFieldsController.
    /// </summary>
    public CustomFieldsController(ICustomFieldService customFields, ICurrentUserService currentUser)
    {
        _customFields = customFields;
        _currentUser = currentUser;
    }

    /// <summary>Lists the workspace's field definitions, optionally for one kind of record.</summary>
    /// <response code="200">Returns the definitions in display order.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> List(
        [FromQuery] string? entityType, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.ListDefinitionsAsync(userId, _currentUser.CurrentOrganizationId, entityType, ct)).ToActionResult();
    }

    /// <summary>Adds a field. Its key is derived from the label and never changes.</summary>
    /// <response code="200">Returns the new definition.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    [Requires(MemberPermissions.ManageSettings, What = "change custom fields")]
    [HttpPost]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Create(
        [FromBody] CreateCustomFieldRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.CreateDefinitionAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Changes a field's label, options, default, currency or whether it is required. Its kind
    /// (and a link's kind of record) can change only while no record has a value for it.
    /// </summary>
    /// <response code="200">Returns the definition.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="404">Not in this workspace.</response>
    /// <response code="409">The kind would change, and records already have values for the field.</response>
    [Requires(MemberPermissions.ManageSettings, What = "change custom fields")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Update(
        Guid id, [FromBody] UpdateCustomFieldRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.UpdateDefinitionAsync(userId, _currentUser.CurrentOrganizationId, id, request, ct)).ToActionResult();
    }

    /// <summary>Puts one kind of record's fields in the order given: every one of them, each once.</summary>
    /// <response code="200">The fields in their new order.</response>
    /// <response code="400">A field is missing, repeated or not of that kind.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    [Requires(MemberPermissions.ManageSettings, What = "change custom fields")]
    [HttpPut("order")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> Reorder(
        [FromBody] ReorderCustomFieldsRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.ReorderAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>Removes a field and the values entered for it, so a field added later under the same label starts empty.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="404">Not in this workspace.</response>
    [Requires(MemberPermissions.ManageSettings, What = "change custom fields")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.DeleteDefinitionAsync(userId, _currentUser.CurrentOrganizationId, id, ct)).ToNoContentResult();
    }

    /// <summary>A record's values, keyed by field key; only fields that still exist.</summary>
    /// <response code="200">Returns the values.</response>
    [HttpGet("values")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, JsonElement>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyDictionary<string, JsonElement>>> GetValues(
        [FromQuery] string? entityType, [FromQuery] Guid recordId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.GetValuesAsync(userId, _currentUser.CurrentOrganizationId, entityType, recordId, ct)).ToActionResult();
    }

    /// <summary>
    /// The values of many records of one kind at once, keyed by record id then field key, for a
    /// list's columns. Only records the caller can see answer; at most 500 a call.
    /// </summary>
    /// <response code="200">The values; a record with none is left out.</response>
    /// <response code="400">Not a kind of record that carries custom fields here.</response>
    [HttpPost("values/batch")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>>> GetValuesBatch(
        [FromBody] CustomFieldValuesBatchRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.GetValuesBatchAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Saves a record's values: only the fields sent change, each checked against its field; a
    /// required field cannot be emptied, and a link must point at a record of this workspace.
    /// Unknown keys are ignored.
    /// </summary>
    /// <response code="204">Saved.</response>
    /// <response code="400">A value does not fit its field, or a required one was emptied.</response>
    /// <response code="404">The record is not in this workspace.</response>
    /// <response code="409">Another save of the same fields got there first.</response>
    // A member's values are club work, any other record's records.edit: the service asks for the one the kind needs.
    [Requires(MemberPermissions.EditRecords, MemberPermissions.ClubMembers, Any = true)]
    [HttpPut("values")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveValues([FromBody] SaveCustomFieldValuesRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.SaveValuesAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToNoContentResult();
    }
}
