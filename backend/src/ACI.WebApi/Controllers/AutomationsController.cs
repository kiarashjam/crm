using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Workspace rules: when something happens, do the stored actions.
/// </summary>
/// <remarks>
/// A rule can reassign records and send mail for the whole workspace, so changing one
/// takes the owner or a manager, as lead routing does. Everyone can see them.
/// </remarks>
[ApiController]
[Route("api/automations")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class AutomationsController : ControllerBase
{
    private readonly IAutomationService _automations;
    private readonly ICurrentUserService _currentUser;

    public AutomationsController(IAutomationService automations, ICurrentUserService currentUser)
    {
        _automations = automations;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationRuleDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _automations.ListAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost]
    [ManagersOnly("change automations")]
    public async Task<ActionResult<AutomationRuleDto>> Create([FromBody] AutomationWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _automations.CreateAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}")]
    [ManagersOnly("change automations")]
    public async Task<ActionResult<AutomationRuleDto>> Update(Guid id, [FromBody] AutomationWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.UpdateAsync(id, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ManagersOnly("change automations")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.DeleteAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}
