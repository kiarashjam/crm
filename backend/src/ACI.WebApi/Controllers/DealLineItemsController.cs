using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// A deal's line items: what is being sold, how many, at what price. Reached only
/// through a deal in the current workspace.
/// </summary>
[ApiController]
[Route("api/deals")]
[Authorize]
[Produces("application/json")]
public class DealLineItemsController : ControllerBase
{
    private readonly IDealLineItemService _lineItems;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the DealLineItemsController.
    /// </summary>
    public DealLineItemsController(IDealLineItemService lineItems, ICurrentUserService currentUser)
    {
        _lineItems = lineItems;
        _currentUser = currentUser;
    }

    /// <summary>Lists a deal's line items in the order they were added.</summary>
    /// <response code="200">Returns the line items.</response>
    /// <response code="404">The deal is not in this workspace.</response>
    [HttpGet("{dealId:guid}/line-items")]
    [ProducesResponseType(typeof(IReadOnlyList<DealLineItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DealLineItemDto>>> List(Guid dealId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _lineItems.ListAsync(userId, _currentUser.CurrentOrganizationId, dealId, ct)).ToActionResult();
    }

    /// <summary>Adds a line item to a deal. The deal's value is not changed.</summary>
    /// <response code="200">Returns the new line item.</response>
    /// <response code="400">No name, a quantity of 0 or less, or a negative price.</response>
    /// <response code="404">The deal is not in this workspace.</response>
    [HttpPost("{dealId:guid}/line-items")]
    [ProducesResponseType(typeof(DealLineItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealLineItemDto>> Add(
        Guid dealId, [FromBody] CreateDealLineItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _lineItems.AddAsync(userId, _currentUser.CurrentOrganizationId, dealId, request, ct)).ToActionResult();
    }

    /// <summary>Changes a line item's name, quantity, price or product.</summary>
    /// <response code="200">Returns the line item.</response>
    /// <response code="400">The change would leave it without a name, quantity or valid price.</response>
    /// <response code="404">Not on a deal in this workspace.</response>
    [HttpPut("line-items/{id:guid}")]
    [ProducesResponseType(typeof(DealLineItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealLineItemDto>> Update(
        Guid id, [FromBody] UpdateDealLineItemRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _lineItems.UpdateAsync(userId, _currentUser.CurrentOrganizationId, id, request, ct)).ToActionResult();
    }

    /// <summary>Removes a line item.</summary>
    /// <response code="204">Removed.</response>
    /// <response code="404">Not on a deal in this workspace.</response>
    [HttpDelete("line-items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _lineItems.DeleteAsync(userId, _currentUser.CurrentOrganizationId, id, ct)).ToNoContentResult();
    }
}
