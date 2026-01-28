using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/copy")]
[Authorize]
public class CopyController : ControllerBase
{
    private readonly ICopyGeneratorApplicationService _generatorService;
    private readonly ISendToCrmService _sendToCrmService;
    private readonly ICurrentUserService _currentUser;

    public CopyController(
        ICopyGeneratorApplicationService generatorService,
        ISendToCrmService sendToCrmService,
        ICurrentUserService currentUser)
    {
        _generatorService = generatorService;
        _sendToCrmService = sendToCrmService;
        _currentUser = currentUser;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<string>> Generate([FromBody] GenerateCopyRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var copy = await _generatorService.GenerateAsync(userId.Value, request, ct);
        return Ok(new { copy });
    }

    [HttpPost("send")]
    public async Task<ActionResult<SendToCrmResult>> SendToCrm([FromBody] SendToCrmRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _sendToCrmService.SendAsync(userId.Value, request, ct);
        return Ok(result);
    }
}
