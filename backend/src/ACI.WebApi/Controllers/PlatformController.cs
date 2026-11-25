using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The platform itself, above every workspace: what the signed-in person is on it.
/// </summary>
[ApiController]
[Route("api/platform")]
[Authorize]
[Produces("application/json")]
public sealed class PlatformController : ControllerBase
{
    private readonly IPlatformAdmins _admins;
    private readonly ICurrentUserService _currentUser;

    public PlatformController(IPlatformAdmins admins, ICurrentUserService currentUser)
    {
        _admins = admins;
        _currentUser = currentUser;
    }

    /// <summary>
    /// <c>{ isPlatformAdmin }</c>: whether the signed-in person runs the platform
    /// (Platform:AdminEmails, with their address confirmed). The app shows the site analytics
    /// to them alone; the analytics endpoints check for themselves.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<PlatformMeDto>> Me(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new PlatformMeDto(await _admins.IsPlatformAdminAsync(userId, ct)));
    }

    public sealed record PlatformMeDto(bool IsPlatformAdmin);
}
