using ACI.Application.DTOs;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACI.Domain.Common;
using ACI.WebApi.Filters;

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
[Requires(MemberPermissions.EditRecords, WritesOnly = true)]
[InModule(WorkspaceModules.Sales)]
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

    /// <summary>
    /// Records a contract signed outside CadenceE (on paper, or with another signing service)
    /// by uploading the signed copy: a PDF, or a JPEG or PNG photo, up to 10 MB. Multipart form:
    /// file, and dealId or leadId, or contractId for one drafted or sent here; signedBy and
    /// signedOn (yyyy-MM-dd) optionally.
    /// </summary>
    /// <remarks>
    /// The contract is executed by it, as by a countersignature: its deal is marked won and the
    /// membership behind it approved. The copy is kept as a file on the deal and is the
    /// contract's PDF. Nothing is emailed.
    /// </remarks>
    /// <response code="200">The executed contract.</response>
    /// <response code="400">No file, one too large, not a PDF or photo, or a day in the future.</response>
    /// <response code="404">The deal, lead or contract is not in this workspace.</response>
    [HttpPost("signed-copy")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> SignedCopy(
        IFormFile? file,
        [FromForm] Guid? dealId,
        [FromForm] Guid? leadId,
        [FromForm] Guid? contractId,
        [FromForm] string? signedBy,
        [FromForm] DateTime? signedOn,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        if (file == null || file.Length == 0) return DomainErrors.Attachment.Empty.ToProblemResult();
        // Refused before it is read into memory.
        if (file.Length > AttachmentService.MaxBytes) return DomainErrors.Attachment.TooLarge.ToProblemResult();

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var result = await _contracts.RecordSignedCopyAsync(userId.Value, _currentUser.UserName, orgId.Value,
            new RecordSignedCopyRequest(dealId, leadId, contractId, signedBy, signedOn, file.FileName, file.ContentType, buffer.ToArray()), ct);
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

    /// <summary>
    /// Every contract on a deal, newest first: those drafted on it, and those of the lead it
    /// was converted from.
    /// </summary>
    /// <response code="404">The deal is not one this person may see.</response>
    [HttpGet("for-deal/{dealId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ContractDto>>> ListForDeal(Guid dealId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.CurrentOrganizationId;
        if (userId is null || orgId is null) return Unauthorized();
        var result = await _contracts.ListForDealAsync(dealId, userId.Value, orgId.Value, ct);
        return result.ToActionResult();
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

    /// <summary>
    /// The contract as a PDF, for downloading.
    /// </summary>
    /// <remarks>
    /// Available in every state. A draft downloads watermarked, so what a CRM user
    /// reviews on paper is exactly the sheet their counterparty will receive, and an
    /// unsigned copy cannot be passed off as an executed one.
    /// </remarks>
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId is null) return Unauthorized();

        var result = await _contracts.GetDocumentAsync(id, orgId.Value, ct);
        if (result.IsFailure) return result.Error.ToProblemResult();
        return DocumentFile(result.Value);
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
    /// Returns a rendered document as a file.
    /// </summary>
    /// <remarks>
    /// The filename is built by the service and already ASCII-folded, because a
    /// header carrying anything else makes Kestrel throw.
    /// </remarks>
    private FileContentResult DocumentFile(ContractDocument document)
    {
        Response.Headers.ContentDisposition = document.InlineContentDisposition;
        return File(document.Content, document.ContentType);
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
