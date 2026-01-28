using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ICurrentUserService _currentUser;

    public TemplatesController(ITemplateService templateService, ICurrentUserService currentUser)
    {
        _templateService = templateService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> GetAll(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _templateService.GetAllAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> GetById(Guid id, CancellationToken ct)
    {
        var template = await _templateService.GetByIdAsync(id, ct);
        if (template == null) return NotFound();
        return Ok(template);
    }
}
