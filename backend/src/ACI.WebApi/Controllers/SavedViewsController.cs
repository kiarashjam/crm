using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Saved views of the lists (leads, contacts, companies, deals, members): a name for a
/// combination of filters, order and columns, kept for its owner or shared with the
/// workspace, and the one each person opens a list with. A person's own views are theirs
/// to keep whatever their role; sharing one takes a role that can change records.
/// </summary>
[ApiController]
[Route("api/saved-views")]
[Authorize]
[Produces("application/json")]
public class SavedViewsController : ControllerBase
{
    private readonly ISavedViewService _views;
    private readonly ICurrentUserService _currentUser;

    public SavedViewsController(ISavedViewService views, ICurrentUserService currentUser)
    {
        _views = views;
        _currentUser = currentUser;
    }

    /// <summary>The views of one list the caller can open: their own and those shared with the workspace.</summary>
    /// <response code="200">The views, by name, saying which one the caller opens the list with.</response>
    /// <response code="400">Not one of the lists.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavedViewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SavedViewDto>>> List([FromQuery] string? list, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _views.ListAsync(userId, _currentUser.CurrentOrganizationId, list, ct)).ToActionResult();
    }

    /// <summary>Saves a view of a list, for the caller or shared with the workspace.</summary>
    /// <response code="200">The view.</response>
    /// <response code="400">No name, not one of the lists, or filters or columns of the wrong shape.</response>
    /// <response code="403">A view-only member tried to share it.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SavedViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SavedViewDto>> Create([FromBody] CreateSavedViewRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _views.CreateAsync(userId, _currentUser.CurrentOrganizationId, request, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Changes the caller's own view. Send the version it was read at in the body or as If-Match.</summary>
    /// <response code="200">The view.</response>
    /// <response code="403">Not the caller's view, or a view-only member tried to share it.</response>
    /// <response code="404">Not a view the caller can open.</response>
    /// <response code="409">Changed since it was read.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SavedViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SavedViewDto>> Update(Guid id, [FromBody] UpdateSavedViewRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var (expected, error) = Request.ExpectedVersion(request.Version);
        if (error != null) return error.ToProblemResult();
        var result = await _views.UpdateAsync(userId, _currentUser.CurrentOrganizationId, id, request, expected, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Deletes a view: the caller's own, or a shared one when the caller manages the workspace's settings.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Not the caller's to delete.</response>
    /// <response code="404">Not a view the caller can open.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _views.DeleteAsync(userId, _currentUser.CurrentOrganizationId, id, ct)).ToNoContentResult();
    }

    /// <summary>Sets the view the caller opens a list with; none opens it as it comes.</summary>
    /// <response code="204">Set.</response>
    /// <response code="404">Not a view of that list the caller can open.</response>
    [HttpPut("default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetDefault([FromBody] SetDefaultViewRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _views.SetDefaultAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToNoContentResult();
    }
}
