using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// A person's data rights: look someone up across the workspace, download everything held
/// about them, and erase them.
/// </summary>
/// <remarks>
/// Everything here takes privacy.manage, held by the owner and managers: an export hands over
/// everything about someone at once, and an erasure cannot be undone. Lookups and previews
/// are POSTs because they carry an email address, which does not belong in a URL (and so in
/// server and proxy logs); they change nothing.
/// </remarks>
[ApiController]
[Route("api/privacy")]
[Authorize]
[WorkspaceRequired]
[Requires(MemberPermissions.ManagePrivacy, What = "handle privacy requests")]
[Produces("application/json")]
public class PrivacyController : ControllerBase
{
    private readonly IPersonData _people;
    private readonly IPrivacySettings _settings;
    private readonly IRetention _retention;
    private readonly ICurrentUserService _currentUser;

    public PrivacyController(IPersonData people, IPrivacySettings settings, IRetention retention, ICurrentUserService currentUser)
    {
        _people = people;
        _settings = settings;
        _retention = retention;
        _currentUser = currentUser;
    }

    /// <summary>The workspace's privacy settings: who answers for its data, the forms' consent, the retention periods.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(PrivacySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrivacySettingsDto>> GetSettings(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var settings = await _settings.GetAsync(org, ct);
        Response.SetETag(settings.Version);
        return Ok(settings);
    }

    /// <summary>Saves the privacy settings. Send If-Match with the version read; a stale one gets 409.</summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(PrivacySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> SaveSettings([FromBody] PrivacySettingsRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var (expected, badVersion) = Request.ExpectedVersion(null);
        if (badVersion != null) return badVersion.ToProblemResult();
        var result = await _settings.SaveAsync(org, user, request, expected, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }

    /// <summary>What the retention settings would remove if they ran now, counted without removing anything.</summary>
    [HttpGet("retention/preview")]
    [ProducesResponseType(typeof(RetentionPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetentionPreviewDto>> RetentionPreview(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _retention.PreviewAsync(org, ct));
    }

    /// <summary>Runs the retention settings now, as the daily run would, and returns what the run removed.</summary>
    [HttpPost("retention/run")]
    [ProducesResponseType(typeof(RetentionRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> RunRetention(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _retention.RunNowAsync(org, user, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The log of retention runs, newest first: what each removed, in counts.</summary>
    [HttpGet("retention/runs")]
    [ProducesResponseType(typeof(IReadOnlyList<RetentionRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RetentionRunDto>>> RetentionRuns(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _retention.RunsAsync(org, ct));
    }

    /// <summary>Records that look like the person: by an email address, or from one of their records.</summary>
    /// <response code="200">The records found, each saying why, and the addresses involved.</response>
    /// <response code="404">The record started from is not in this workspace.</response>
    [HttpPost("find")]
    [ProducesResponseType(typeof(PersonFoundDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Find([FromBody] PersonLookup lookup, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _people.FindAsync(org, lookup, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Everything held about the chosen records, as a zip of JSON files with a readable summary.</summary>
    /// <response code="200">The zip. The download is written to the audit trail.</response>
    [HttpPost("export")]
    [Produces("application/zip")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Export([FromBody] PersonSelection selection, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _people.ExportAsync(org, user, selection, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, "application/zip", result.Value.FileName);
    }

    /// <summary>What erasing the chosen records would delete, anonymise, cancel and keep. Changes nothing.</summary>
    [HttpPost("erasure/preview")]
    [ProducesResponseType(typeof(ErasurePlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Preview([FromBody] PersonSelection selection, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _people.PlanErasureAsync(org, selection, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Erases the chosen records. The body must say <c>"confirm": "ERASE"</c>.</summary>
    /// <response code="200">What was done, and the pseudonym the books use for the person now.</response>
    /// <response code="400">Not confirmed, or a reference that names the person.</response>
    /// <response code="409">One of the records changed meanwhile; nothing was erased.</response>
    [HttpPost("erasure")]
    [ProducesResponseType(typeof(ErasureResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Erase([FromBody] EraseRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _people.EraseAsync(org, user, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The log of erasures: numbers, dates, who confirmed them and counts. It names nobody.</summary>
    [HttpGet("erasures")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonErasureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonErasureDto>>> Erasures(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _people.ErasuresAsync(org, ct));
    }
}
