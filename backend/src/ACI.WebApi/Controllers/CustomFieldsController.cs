using System.Text.Json;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// <response code="403">Only the owner or a manager can change custom fields.</response>
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

    /// <summary>Changes a field's label, type, options or whether it is required.</summary>
    /// <response code="200">Returns the definition.</response>
    /// <response code="403">Only the owner or a manager can change custom fields.</response>
    /// <response code="404">Not in this workspace.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Update(
        Guid id, [FromBody] UpdateCustomFieldRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.UpdateDefinitionAsync(userId, _currentUser.CurrentOrganizationId, id, request, ct)).ToActionResult();
    }

    /// <summary>Removes a field. Values already entered for it are no longer returned.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="403">Only the owner or a manager can change custom fields.</response>
    /// <response code="404">Not in this workspace.</response>
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

    /// <summary>Saves a record's values. Each is checked against its field; unknown keys are ignored.</summary>
    /// <response code="204">Saved.</response>
    /// <response code="400">A value does not fit its field.</response>
    /// <response code="404">The record is not in this workspace.</response>
    [HttpPut("values")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SaveValues([FromBody] SaveCustomFieldValuesRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _customFields.SaveValuesAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToNoContentResult();
    }
}
