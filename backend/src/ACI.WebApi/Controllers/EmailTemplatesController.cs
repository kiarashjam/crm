using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The workspace's prepared emails, in French and English, for the email composer: list
/// them (the starter set comes with the first read), add one, change one, remove one.
/// </summary>
[ApiController]
[Route("api/email-templates")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.EditRecords, WritesOnly = true)]
public class EmailTemplatesController : ControllerBase
{
    private readonly IEmailTemplateService _templates;
    private readonly ICurrentUserService _currentUser;

    public EmailTemplatesController(IEmailTemplateService templates, ICurrentUserService currentUser)
    {
        _templates = templates;
        _currentUser = currentUser;
    }

    /// <summary>The prepared emails, the languages they can be in, the one to start in, and the placeholders filled when sending.</summary>
    [HttpGet]
    public async Task<ActionResult<EmailTemplateListDto>> List(CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return Ok(await _templates.ListAsync(org, ct));
    }

    [HttpPost]
    public async Task<ActionResult<EmailTemplateDto>> Create([FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        var result = await _templates.CreateAsync(org, userId, request, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmailTemplateDto>> Update(Guid id, [FromBody] SaveEmailTemplateRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _templates.UpdateAsync(org, userId, id, request, ct)).ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _templates.DeleteAsync(org, id, ct)).ToNoContentResult();
    }
}
