using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

// Money from outside the club: the payment and bank settings, QR-bill references, bank files,
// online payments through Stripe, and the accounting journal. settings.manage sets them up;
// money.manage takes payments, imports bank files and exports the journal; money.void refunds.

/// <summary>The workspace's payment, bank and accounting settings.</summary>
[ApiController]
[Route("api/payment-settings")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class PaymentSettingsController : ControllerBase
{
    private readonly IClubPaymentSettings _settings;
    private readonly ICurrentUserService _user;

    public PaymentSettingsController(IClubPaymentSettings settings, ICurrentUserService user)
    {
        _settings = settings;
        _user = user;
    }

    /// <summary>The settings, with no secret in them: only whether each is set.</summary>
    [HttpGet]
    [Requires(MemberPermissions.ManageSettings, What = "see the payment settings")]
    public async Task<ActionResult<PaymentSettingsDto>> Get(CancellationToken ct)
    {
        var settings = await _settings.GetAsync(_user.CurrentOrganizationId!.Value, ct);
        Response.SetETag(settings.Version);
        return Ok(settings);
    }

    /// <summary>Saves the settings, each checked; a new Stripe key is tried with Stripe first.</summary>
    [HttpPut]
    [Requires(MemberPermissions.ManageSettings, What = "change the payment settings")]
    public async Task<ActionResult> Save([FromBody] PaymentSettingsRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        var saved = await _settings.SaveAsync(_user.CurrentOrganizationId!.Value, userId, request, version, ct);
        if (!saved.IsSuccess) return saved.Error.ToProblemResult();
        Response.SetETag(saved.Value.Version);
        return Ok(saved.Value);
    }

    /// <summary>Whether online payments and QR-bills are set up, for the money screens. Any member may ask.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<PaymentStatusDto>> Status(CancellationToken ct) =>
        Ok(await _settings.StatusAsync(_user.CurrentOrganizationId!.Value, ct));
}

/// <summary>What a reference printed on a QR-bill pays.</summary>
[ApiController]
[Route("api/payment-references")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class PaymentReferencesController : ControllerBase
{
    private readonly IClubQrBills _qrBills;
    private readonly ICurrentUserService _user;

    public PaymentReferencesController(IClubQrBills qrBills, ICurrentUserService user)
    {
        _qrBills = qrBills;
        _user = user;
    }

    [HttpGet("{reference}")]
    public async Task<ActionResult> Find(string reference, CancellationToken ct)
    {
        var found = await _qrBills.FindAsync(_user.CurrentOrganizationId!.Value, reference, ct);
        return found.IsSuccess ? Ok(found.Value) : found.Error.ToProblemResult();
    }
}
