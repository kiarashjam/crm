using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(ISettingsService settingsService, ICurrentUserService currentUser)
    {
        _settingsService = settingsService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<UserSettingsDto>> Get(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var settings = await _settingsService.GetAsync(userId.Value, ct);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<UserSettingsDto>> Save([FromBody] UserSettingsDto dto, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var settings = await _settingsService.SaveAsync(userId.Value, dto, ct);
        return Ok(settings);
    }
}
