using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

// How people join the club and who belongs together: membership applications (and the public
// form people apply on) and households. They were JSON documents the browser kept in step
// (ClubDocumentControllers.cs); the documents stay, unread, since the
// AddMembershipBridgeAndHouseholds migration copied them here.

/// <summary>Membership applications: review, approval (one transaction on the server) and the public form's settings.</summary>
[ApiController]
[Route("api/applications")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class ApplicationsController : ControllerBase
{
    private readonly IClubApplications _applications;
    private readonly ICurrentUserService _user;

    public ApplicationsController(IClubApplications applications, ICurrentUserService user)
    {
        _applications = applications;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> List(CancellationToken ct) =>
        Ok(await _applications.ListAsync(Org, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Get(Guid id, CancellationToken ct) =>
        Versioned(await _applications.GetAsync(Org, id, ct));

    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Create([FromBody] ApplicationWriteRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return Versioned(await _applications.CreateAsync(Org, userId, request, ct));
    }

    /// <summary>Changes an application's details (a field left out is kept). An approved one is refused (409).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Update(Guid id, [FromBody] ApplicationWriteRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        return Versioned(await _applications.UpdateAsync(Org, id, userId, request, ct));
    }

    /// <summary>Under review, waitlisted, rejected, or reopened (Submitted). Approval is <see cref="Approve"/>.</summary>
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<ApplicationDto>> SetStatus(Guid id, [FromBody] ApplicationStatusRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        return Versioned(await _applications.SetStatusAsync(Org, id, userId, request, ct));
    }

    /// <summary>
    /// Approves the application and admits the applicant, in one transaction: the member, their
    /// lead, contact and consent, the household, the dues and the initiation fee (with
    /// money.manage), and the welcome email. Approving again answers with the member already made.
    /// </summary>
    /// <response code="200">What the approval did, or found done.</response>
    /// <response code="409">Not waiting for a decision any more (rejected), or changed by someone else.</response>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApprovalResultDto>> Approve(Guid id, [FromBody] ApproveApplicationRequest? request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        request ??= new ApproveApplicationRequest();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        var result = await _applications.ApproveAsync(Org, id, userId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _applications.DeleteAsync(Org, id, ct)).ToNoContentResult();

    /// <summary>Starts an application for a lead, filled in from it; their open one is answered instead.</summary>
    [HttpPost("from-lead/{leadId:guid}")]
    public async Task<ActionResult<ApplicationDto>> FromLead(Guid leadId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return Versioned(await _applications.StartForPersonAsync(Org, userId, leadId, null, ct));
    }

    /// <summary>Starts an application for a contact, filled in from them; their open one is answered instead.</summary>
    [HttpPost("from-contact/{contactId:guid}")]
    public async Task<ActionResult<ApplicationDto>> FromContact(Guid contactId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return Versioned(await _applications.StartForPersonAsync(Org, userId, null, contactId, ct));
    }

    /// <summary>The public application form's link and wording; <c>form</c> is null before one is set up.</summary>
    [HttpGet("form")]
    public async Task<ActionResult> Form(CancellationToken ct) => Ok(new { form = await _applications.FormAsync(Org, ct) });

    /// <summary>Sets up the public form, opens or closes it, or changes its wording.</summary>
    [HttpPut("form")]
    [Requires(MemberPermissions.ManageSettings, What = "change the public application form")]
    public async Task<ActionResult<ApplicationFormDto>> SaveForm([FromBody] ApplicationFormRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return (await _applications.SaveFormAsync(Org, userId, request, ct)).ToActionResult();
    }

    /// <summary>Gives the public form a new link; the old one stops working.</summary>
    [HttpPost("form/rotate")]
    [Requires(MemberPermissions.ManageSettings, What = "change the public application form")]
    public async Task<ActionResult<ApplicationFormDto>> RotateForm(CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return (await _applications.RotateFormAsync(Org, userId, ct)).ToActionResult();
    }

    private ActionResult<ApplicationDto> Versioned(Result<ApplicationDto> result)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }
}

/// <summary>Households: their people, roles, primary, roll-up billing, statement and merges.</summary>
[ApiController]
[Route("api/households")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class HouseholdsController : ControllerBase
{
    private readonly IClubHouseholds _households;
    private readonly ICurrentUserService _user;

    public HouseholdsController(IClubHouseholds households, ICurrentUserService user)
    {
        _households = households;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdDto>>> List(CancellationToken ct) => Ok(await _households.ListAsync(Org, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HouseholdDto>> Get(Guid id, CancellationToken ct) => Versioned(await _households.GetAsync(Org, id, ct));

    /// <summary>The household a member is in; <c>household</c> is null when they are in none.</summary>
    [HttpGet("of-member/{memberId:guid}")]
    public async Task<ActionResult> OfMember(Guid memberId, CancellationToken ct) =>
        Ok(new { household = await _households.ForMemberAsync(Org, memberId, ct) });

    [HttpPost]
    public async Task<ActionResult<HouseholdDto>> Create([FromBody] HouseholdWriteRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return Versioned(await _households.CreateAsync(Org, userId, request, ct));
    }

    /// <summary>Renames the household or changes its notes.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HouseholdDto>> Update(Guid id, [FromBody] HouseholdWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        return Versioned(await _households.UpdateAsync(Org, id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _households.DeleteAsync(Org, id, ct)).ToNoContentResult();

    /// <summary>Folds duplicate households into this one, in one save.</summary>
    [HttpPost("{id:guid}/merge")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult<HouseholdDto>> Merge(Guid id, [FromBody] HouseholdMergeRequest request, CancellationToken ct) =>
        Versioned(await _households.MergeAsync(Org, id, request?.DuplicateIds ?? [], ct));

    /// <summary>Adds a member or a contact, with their relation to the primary.</summary>
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<HouseholdDto>> AddPerson(Guid id, [FromBody] HouseholdPersonRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        return Versioned(await _households.AddPersonAsync(Org, id, userId, request, ct));
    }

    /// <summary>Changes a person's role, or whether their bills roll up to the primary (money.manage).</summary>
    [HttpPut("{id:guid}/members/{linkId:guid}")]
    public async Task<ActionResult<HouseholdDto>> UpdatePerson(Guid id, Guid linkId, [FromBody] HouseholdPersonRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        return Versioned(await _households.UpdatePersonAsync(Org, id, linkId, request, ct));
    }

    [HttpDelete("{id:guid}/members/{linkId:guid}")]
    public async Task<ActionResult<HouseholdDto>> RemovePerson(Guid id, Guid linkId, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        return Versioned(await _households.RemovePersonAsync(Org, id, linkId, version, ct));
    }

    /// <summary>Makes someone in the household its primary.</summary>
    [HttpPost("{id:guid}/primary")]
    public async Task<ActionResult<HouseholdDto>> SetPrimary(Guid id, [FromBody] HouseholdPrimaryRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Versioned(await _households.SetPrimaryAsync(Org, id, request.LinkId, version, ct));
    }

    /// <summary>The household's statement for the primary as a PDF: each line says whose it was.</summary>
    [HttpGet("{id:guid}/statement.pdf")]
    [Produces("application/pdf", "application/json")]
    public async Task<ActionResult> StatementPdf(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var file = await _households.StatementPdfAsync(Org, id, from.ToUniversalTime(), to.ToUniversalTime(), ct);
        if (!file.IsSuccess) return file.Error.ToProblemResult();
        return File(file.Value.Content, "application/pdf", file.Value.FileName);
    }

    private ActionResult<HouseholdDto> Versioned(Result<HouseholdDto> result)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }
}

/// <summary>Who a lead, a contact or a member is on the other side: the member, the applications, the household.</summary>
[ApiController]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class MembershipLinksController : ControllerBase
{
    private readonly IClubApplications _applications;
    private readonly IClubHouseholds _households;
    private readonly ICurrentUserService _user;

    public MembershipLinksController(IClubApplications applications, IClubHouseholds households, ICurrentUserService user)
    {
        _applications = applications;
        _households = households;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>The member a lead became (or several), their applications and household.</summary>
    [HttpGet("api/leads/{leadId:guid}/membership")]
    public async Task<ActionResult<PersonMembershipDto>> ForLead(Guid leadId, CancellationToken ct)
    {
        var found = await _applications.MembershipAsync(Org, leadId, null, ct);
        return Ok(found with { Household = found.ContactId is { } contactId ? await _households.ForContactAsync(Org, contactId, ct) : null });
    }

    /// <summary>The member a contact is (or several), their applications and household.</summary>
    [HttpGet("api/contacts/{contactId:guid}/membership")]
    public async Task<ActionResult<PersonMembershipDto>> ForContact(Guid contactId, CancellationToken ct) =>
        Ok((await _applications.MembershipAsync(Org, null, contactId, ct)) with
        {
            Household = await _households.ForContactAsync(Org, contactId, ct),
        });

    /// <summary>A member's lead and contact, the applications that made them, and their household.</summary>
    [HttpGet("api/members/{memberId:guid}/links")]
    public async Task<ActionResult<MemberLinksDto>> ForMember(Guid memberId, CancellationToken ct)
    {
        var links = await _applications.MemberLinksAsync(Org, memberId, ct);
        if (!links.IsSuccess) return links.Error.ToProblemResult();
        return Ok(links.Value with { Household = await _households.ForMemberAsync(Org, memberId, ct) });
    }
}

/// <summary>
/// The public application form: no sign-in, a per-workspace link like the lead capture form,
/// rate limited by its link, with a honeypot against bots.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/apply")]
public class PublicApplicationsController : ControllerBase
{
    private readonly IClubApplications _applications;

    public PublicApplicationsController(IClubApplications applications) => _applications = applications;

    [HttpGet("{token}")]
    public async Task<ActionResult<PublicApplicationFormDto>> Form(string token, CancellationToken ct)
    {
        var form = await _applications.PublicFormAsync(token, ct);
        return form == null ? NotFound() : Ok(form);
    }

    [HttpPost("{token}")]
    [EnableRateLimiting("public-apply")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<ActionResult<PublicApplicationReceipt>> Submit(string token, [FromBody] PublicApplicationRequest request, CancellationToken ct)
    {
        var result = await _applications.SubmitAsync(token, request, ct);
        if (result.IsSuccess) return StatusCode(StatusCodes.Status201Created, result.Value);
        return result.Error.ToProblemResult();
    }
}
