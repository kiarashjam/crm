using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Contracts: draft, edit, send for signature, countersign.
/// </summary>
/// <remarks>
/// The CRM side of the flow. The counterparty's half lives on
/// <see cref="PublicContractsController"/>, which is anonymous because they have
/// no account.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contracts;
    private readonly ICurrentUserService _currentUser;

    public ContractsController(IContractService contracts, ICurrentUserService currentUser)
    {
        _contracts = contracts;
        _currentUser = currentUser;
    }

    /// <summary>Step 1 — generate a draft from the organisation's template.</summary>
    /// <response code="200">The draft, with any unfilled placeholders listed.</response>
    [HttpPost("draft")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ContractDto>> CreateDraft(
        [FromBody] CreateContractDraftRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();

        var result = await _contracts.CreateDraftAsync(userId.Value, orgId.Value, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Every contract on a lead, newest first.</summary>
    [HttpGet("for-lead/{leadId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ContractDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> ListForLead(Guid leadId, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId is null) return Unauthorized();
        return Ok(await _contracts.ListForLeadAsync(leadId, orgId.Value, ct));
    }

    /// <summary>One contract, with its audit trail.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> Get(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId is null) return Unauthorized();
        var result = await _contracts.GetAsync(id, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>Step 2 — edit the draft. Refused once it has been sent.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractDto>> Update(
        Guid id, [FromBody] UpdateContractRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.UpdateDraftAsync(id, userId.Value, orgId.Value, request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Step 3 — send it for signature.
    /// </summary>
    /// <remarks>
    /// The response carries <c>emailSent</c> and the signing URL. Check
    /// <c>emailSent</c>: when SMTP is not configured the contract is still sent and
    /// the link is still live, but nobody has been told, and the URL is returned so
    /// it can be passed on by hand.
    /// </remarks>
    [HttpPost("{id:guid}/send")]
    [ProducesResponseType(typeof(SendContractResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendContractResult>> Send(
        Guid id, [FromQuery] bool resend, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.SendAsync(id, userId.Value, orgId.Value, resend, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Step 4 — countersign, which executes the contract and emails the finished
    /// copy to both parties. Refused until the counterparty has signed.
    /// </summary>
    [HttpPost("{id:guid}/countersign")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractDto>> Countersign(
        Guid id, [FromBody] SignContractRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.CountersignAsync(
            id, userId.Value, orgId.Value, request, ClientIp(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Re-sends the executed copy. For when the contract is signed but the email
    /// did not get through — nothing is signed again.
    /// </summary>
    [HttpPost("{id:guid}/resend-copy")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> ResendCopy(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.ResendExecutedCopyAsync(id, userId.Value, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>Withdraws the contract and kills its signing link.</summary>
    [HttpPost("{id:guid}/void")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractDto>> VoidContract(
        Guid id, [FromBody] DeclineContractRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.VoidAsync(id, userId.Value, orgId.Value, request.Reason, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// The caller's address for the audit trail.
    /// </summary>
    /// <remarks>
    /// Behind Azure's front end the socket address is the load balancer, so
    /// X-Forwarded-For is checked first and only its FIRST entry is taken — the
    /// rest are appended by intermediaries and the client can prepend whatever it
    /// likes. This is evidence, not access control, so a spoofable value is
    /// recorded as what it is rather than trusted for a decision.
    /// </remarks>
    private string? ClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (first.Length > 0) return first.Length > 64 ? first[..64] : first;
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
