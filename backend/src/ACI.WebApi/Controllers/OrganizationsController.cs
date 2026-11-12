using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages organizations and their members.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[ActsForRouteOrganization("id")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentUserService _currentUser;

    public OrganizationsController(IOrganizationService organizationService, ICurrentUserService currentUser)
    {
        _organizationService = organizationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lists all organizations the current user is a member of.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organizations.</returns>
    /// <response code="200">Returns the list of organizations.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> ListMy([FromServices] IOrganizationSso sso, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _organizationService.ListMyOrganizationsAsync(userId.Value, ct);
        // The workspaces that would refuse this session, so the app can say so and offer single sign-on.
        var refusing = await sso.RefusingAsync(list.Select(o => o.Id).ToList(), userId.Value, SignInMethods.Of(User), ct);
        return Ok(list.Select(o => o with { SsoSignInNeeded = refusing.Contains(o.Id) }).ToList());
    }

    /// <summary>
    /// Gets a specific organization by ID.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organization details.</returns>
    /// <response code="200">Returns the organization.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    /// <response code="404">Organization not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Get(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GetOrganizationAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new organization with the current user as owner.
    /// </summary>
    /// <param name="request">Organization creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created organization.</returns>
    /// <response code="201">Organization created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.CreateOrganizationAsync(userId.Value, request, ct);
        
        if (result.IsFailure)
            return result.ToActionResult();
        
        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Sets the currency, locale and time zone the workspace works in.
    /// </summary>
    /// <remarks>
    /// New deals are priced in the currency unless they name another; existing deals keep
    /// theirs, and nothing is converted. The time zone is where the workspace's days and
    /// months begin for reports and forecasts. Takes managing the workspace's settings (the
    /// owner and managers by default). A field left out keeps its value.
    /// </remarks>
    /// <response code="200">The workspace with its new settings.</response>
    /// <response code="400">Not a three-letter currency code, a known locale or an IANA time zone.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="404">Organization not found.</response>
    [HttpPut("{id:guid}/settings")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> UpdateSettings(Guid id, [FromBody] UpdateOrganizationSettingsRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.UpdateSettingsAsync(id, userId.Value, request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Turns the workspace's Sales or Club module on or off. A field left out keeps its value.
    /// </summary>
    /// <remarks>
    /// A module that is off leaves every member's menu, its pages say it is turned off, and
    /// its endpoints refuse (Module.Off); nothing is deleted, and turning it on brings it all
    /// back. One module always stays on. A module is not turned off while work of its own is
    /// still switched on (the club's automatic billing, reminders, lapsing or monthly
    /// statements, a campaign scheduled or sending, or the import of POS tickets; leads in
    /// outreach sequences), as nobody could see or stop that work once its pages were gone.
    /// Takes managing the workspace's settings.
    /// </remarks>
    /// <response code="200">Which modules are on now.</response>
    /// <response code="400">Both modules would be off.</response>
    /// <response code="403">The caller's role does not let them manage the workspace's settings.</response>
    /// <response code="409">The module still has work of its own switched on; the answer says what.</response>
    [HttpPut("{id:guid}/modules")]
    [ProducesResponseType(typeof(WorkspaceModulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceModulesDto>> UpdateModules(
        Guid id, [FromBody] SetWorkspaceModulesRequest request, [FromServices] IWorkspaceModules modules, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await modules.SetAsync(id, userId.Value, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Whether the workspace requires its team to sign in with Google or Microsoft, for whom,
    /// and who has signed in that way before.
    /// </summary>
    /// <remarks>
    /// The owner and those who manage the workspace's settings or team can see it; only the
    /// owner can change it.
    /// </remarks>
    [HttpGet("{id:guid}/sso")]
    [ProducesResponseType(typeof(OrganizationSsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationSsoDto>> GetSso(Guid id, [FromServices] IOrganizationSso sso, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await sso.GetAsync(id, userId.Value, ct)).ToActionResult();
    }

    /// <summary>
    /// Requires single sign-on of the team, or stops requiring it, and sets the workspace's
    /// email domains it applies to. A field left out keeps its value. The owner's alone.
    /// </summary>
    /// <remarks>
    /// Sessions that began another way (a password, a passkey) are refused at once by this
    /// workspace for the members it applies to, and keep working in their other workspaces.
    /// The owner is never held to it. It cannot be turned on while this server offers no
    /// single sign-on provider.
    /// </remarks>
    /// <response code="200">The requirement as it now is.</response>
    /// <response code="400">A domain that is not one, or no provider set up on the server.</response>
    /// <response code="403">The caller is not the owner.</response>
    [HttpPut("{id:guid}/sso")]
    [ProducesResponseType(typeof(OrganizationSsoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationSsoDto>> UpdateSso(Guid id, [FromBody] UpdateOrganizationSsoRequest request, [FromServices] IOrganizationSso sso, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await sso.UpdateAsync(id, userId.Value, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Who the workspace's email goes out from: its own address, whether the mail service
    /// has verified its domain (until then the shared address is used), and whether
    /// replies come back into the CRM.
    /// </summary>
    [HttpGet("{id:guid}/email")]
    public async Task<ActionResult<EmailSendingDto>> GetEmailSending(Guid id, [FromServices] IEmailSendingService email, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await email.GetAsync(id, userId.Value, ct)).ToActionResult();
    }

    /// <summary>Sets or clears the workspace's own sending address (takes managing settings).</summary>
    [HttpPut("{id:guid}/email")]
    public async Task<ActionResult<EmailSendingDto>> UpdateEmailSending(
        Guid id, [FromBody] UpdateEmailSendingRequest request, [FromServices] IEmailSendingService email, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await email.UpdateAsync(id, userId.Value, request, ct)).ToActionResult();
    }

    /// <summary>Addresses the workspace does not email because the mail service reported them: bounced, invalid, complained, unsubscribed.</summary>
    [HttpGet("{id:guid}/email/suppressions")]
    public async Task<ActionResult<IReadOnlyList<EmailSuppressionDto>>> EmailSuppressions(Guid id, [FromServices] IEmailSendingService email, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await email.SuppressionsAsync(id, userId.Value, ct)).ToActionResult();
    }

    /// <summary>Takes an address off that list (takes managing settings), so the workspace may email it again.</summary>
    [HttpDelete("{id:guid}/email/suppressions/{suppressionId:guid}")]
    public async Task<ActionResult> LiftEmailSuppression(Guid id, Guid suppressionId, [FromServices] IEmailSendingService email, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await email.LiftSuppressionAsync(id, userId.Value, suppressionId, ct)).ToNoContentResult();
    }

    /// <summary>
    /// Gets all members of an organization.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organization members.</returns>
    /// <response code="200">Returns the list of members.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<OrgMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrgMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GetMembersAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Adds an existing registered user to the organization directly, with a role.
    /// </summary>
    /// <remarks>
    /// For when waiting on an invitation email is not wanted. Takes managing the team.
    /// The person must already have an account — a membership has to point at a user —
    /// so if no account exists yet, send an invitation instead. Owner cannot be granted
    /// here; ownership is transferred separately.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="request">Email of the existing user, and the role to grant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly added member.</returns>
    /// <response code="200">Member added.</response>
    /// <response code="400">No account with that email, already a member, or Owner requested.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">The caller's role does not let them manage the team.</response>
    /// <response code="404">Organization not found.</response>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(OrgMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgMemberDto>> AddMember(Guid id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.AddMemberByEmailAsync(id, userId.Value, request.Email, request.Role, ct, request.Preset);
        return result.ToActionResult();
    }

    /// <summary>Every permission and preset a member can be given, for the Team page.</summary>
    [HttpGet("{id:guid}/permission-sets")]
    [ProducesResponseType(typeof(PermissionCatalogueDto), StatusCodes.Status200OK)]
    public ActionResult<PermissionCatalogueDto> PermissionSets(Guid id) => Ok(_organizationService.GetPermissionCatalogue());

    /// <summary>
    /// Sets what a member may do: a preset, or (the owner only) a list of permissions of
    /// their own. Whoever manages the team can pick presets up to what they can do themselves.
    /// </summary>
    /// <response code="200">The member as they now are.</response>
    /// <response code="400">An unknown preset or permission.</response>
    /// <response code="403">The caller may not manage the team, may not give that much, or the member is the owner.</response>
    [HttpPut("{id:guid}/members/{memberUserId:guid}/permissions")]
    [ProducesResponseType(typeof(OrgMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrgMemberDto>> SetMemberPermissions(Guid id, Guid memberUserId, [FromBody] SetMemberPermissionsRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return (await _organizationService.SetMemberPermissionsAsync(id, userId.Value, memberUserId, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Updates a member's role in the organization.
    /// </summary>
    /// <remarks>
    /// Only the organization owner can update member roles.
    /// The owner's own role cannot be changed.
    /// To transfer ownership, use a separate endpoint.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="memberUserId">The member's user ID.</param>
    /// <param name="request">The new role information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Role updated successfully.</response>
    /// <response code="400">Invalid request or cannot change owner's role.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Member not found.</response>
    [HttpPut("{id:guid}/members/{memberUserId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateMemberRole(Guid id, Guid memberUserId, [FromBody] UpdateMemberRoleRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.UpdateMemberRoleAsync(id, userId.Value, memberUserId, request.Role, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Removes a member from the organization.
    /// </summary>
    /// <remarks>
    /// Only the organization owner can remove members.
    /// The owner cannot remove themselves.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="memberUserId">The member's user ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <param name="handOverTo">Who takes over their open leads, deals and tasks; the owner when left out.</param>
    /// <response code="200">Member removed, and how much of their work moved to whom.</response>
    /// <response code="400">Cannot remove owner, or the one taking over cannot.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Member not found.</response>
    [HttpDelete("{id:guid}/members/{memberUserId:guid}")]
    [ProducesResponseType(typeof(MemberRemovedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberRemovedDto>> RemoveMember(Guid id, Guid memberUserId, [FromQuery] Guid? handOverTo, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.RemoveMemberAsync(id, userId.Value, memberUserId, handOverTo, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets webhook configuration for the organization.
    /// </summary>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook configuration information.</returns>
    /// <response code="200">Returns webhook info.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the organization.</response>
    /// <response code="404">Organization not found.</response>
    [HttpGet("{id:guid}/webhook")]
    [ProducesResponseType(typeof(WebhookInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookInfoDto>> GetWebhookInfo(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.GetWebhookInfoAsync(id, userId.Value, ct);
        if (result.IsFailure)
            return result.ToActionResult();

        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        var infoWithUrl = result.Value with
        {
            WebhookUrl = $"{scheme}://{host}/api/webhook/leads",
            PasswordWebhookUrl = $"{scheme}://{host}/api/webhook/organizations/{id}/leads"
        };

        return Ok(infoWithUrl);
    }

    /// <summary>
    /// Generates a new webhook API key for the organization.
    /// </summary>
    /// <remarks>
    /// Only members who manage the workspace's settings (owners and managers by default) can generate API keys.
    /// Generating a new key invalidates any previous key.
    /// </remarks>
    /// <param name="id">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new API key.</returns>
    /// <response code="200">Returns the new API key.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">The caller's role does not let them manage settings.</response>
    /// <response code="404">Organization not found.</response>
    [HttpPost("{id:guid}/webhook/regenerate")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> GenerateWebhookApiKey(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _organizationService.GenerateWebhookApiKeyAsync(id, userId.Value, ct);
        return result.ToActionResult();
    }
}
