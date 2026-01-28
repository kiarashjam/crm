using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CopyHistoryController : ControllerBase
{
    private readonly ICopyHistoryService _copyHistoryService;
    private readonly ICurrentUserService _currentUser;

    public CopyHistoryController(ICopyHistoryService copyHistoryService, ICurrentUserService currentUser)
    {
        _copyHistoryService = copyHistoryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CopyHistoryItemDto>>> GetHistory(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _copyHistoryService.GetHistoryAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<CopyHistoryStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var stats = await _copyHistoryService.GetStatsAsync(userId.Value, ct);
        return Ok(stats);
    }
}
