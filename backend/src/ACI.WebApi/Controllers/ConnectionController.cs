using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConnectionController : ControllerBase
{
    private readonly IConnectionService _connectionService;
    private readonly ICurrentUserService _currentUser;

    public ConnectionController(IConnectionService connectionService, ICurrentUserService currentUser)
    {
        _connectionService = connectionService;
        _currentUser = currentUser;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ConnectionStatusDto>> GetStatus(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var status = await _connectionService.GetStatusAsync(userId.Value, ct);
        return Ok(status);
    }

    [HttpPut("status")]
    public async Task<IActionResult> SetStatus([FromBody] ConnectionStatusDto dto, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        await _connectionService.SetStatusAsync(userId.Value, dto, ct);
        return NoContent();
    }
}
