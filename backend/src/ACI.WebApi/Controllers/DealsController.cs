using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DealsController : ControllerBase
{
    private readonly IDealService _dealService;
    private readonly ICurrentUserService _currentUser;

    public DealsController(IDealService dealService, ICurrentUserService currentUser)
    {
        _dealService = dealService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DealDto>>> GetDeals(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _dealService.GetDealsAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<DealDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _dealService.SearchAsync(userId.Value, q ?? "", ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DealDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deal = await _dealService.GetByIdAsync(id, userId.Value, ct);
        if (deal == null) return NotFound();
        return Ok(deal);
    }

    [HttpPost]
    public async Task<ActionResult<DealDto>> Create([FromBody] CreateDealRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deal = await _dealService.CreateAsync(userId.Value, request, ct);
        return deal == null ? BadRequest() : Ok(deal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DealDto>> Update(Guid id, [FromBody] UpdateDealRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deal = await _dealService.UpdateAsync(id, userId.Value, request, ct);
        if (deal == null) return NotFound();
        return Ok(deal);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deleted = await _dealService.DeleteAsync(id, userId.Value, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
