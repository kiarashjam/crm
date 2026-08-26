using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The counterparty's half of the signing flow.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous on purpose: the person signing is not a CRM user, has no account, and
/// should not need one to sign a contract someone sent them. The token in the URL
/// is the entire authorisation, which is why it is 32 CSPRNG bytes, stored only as
/// a SHA-256 hash, expiring, and killed when the contract is voided.
/// </para>
/// <para>
/// Everything here returns <see cref="PublicContractDto"/> — a narrow projection
/// carrying no ids, no lead, no audit trail and no organisation internals. A
/// stranger holding a link gets the contract and nothing else.
/// </para>
/// </remarks>
[ApiController]
[Route("api/public/contracts")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicContractsController : ControllerBase
{
    private readonly IContractService _contracts;

    public PublicContractsController(IContractService contracts) => _contracts = contracts;

    /// <summary>Reads the contract behind a signing link.</summary>
    /// <response code="200">The contract, and whether it can still be signed.</response>
    /// <response code="400">The link is invalid or has expired.</response>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(PublicContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicContractDto>> Get(string token, CancellationToken ct)
    {
        var result = await _contracts.GetByTokenAsync(token, ClientIp(), UserAgent(), ct);
        return result.ToActionResult();
    }

    /// <summary>Signs the contract.</summary>
    /// <remarks>
    /// The typed name and the consent tick are both required: the name is the mark,
    /// and the tick is what makes typing it an act of agreement.
    /// </remarks>
    [HttpPost("{token}/sign")]
    [ProducesResponseType(typeof(PublicContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicContractDto>> Sign(
        string token, [FromBody] SignContractRequest request, CancellationToken ct)
    {
        var result = await _contracts.SignByTokenAsync(token, request, ClientIp(), UserAgent(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// The contract as a PDF, so the signer can keep their own copy.
    /// </summary>
    /// <remarks>
    /// Authorised by the token and nothing else, like the rest of this controller. It
    /// returns only the file: no ids, no lead, no audit trail. Before signing it
    /// downloads watermarked unsigned; afterwards it carries both signatures.
    /// </remarks>
    [HttpGet("{token}/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pdf(string token, CancellationToken ct)
    {
        var result = await _contracts.GetDocumentByTokenAsync(token, ct);
        if (result.IsFailure) return result.Error.ToProblemResult();

        Response.Headers.ContentDisposition =
            $"inline; filename=\"{result.Value.FileName}\"";
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>Declines the contract, which closes it.</summary>
    [HttpPost("{token}/decline")]
    [ProducesResponseType(typeof(PublicContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicContractDto>> Decline(
        string token, [FromBody] DeclineContractRequest request, CancellationToken ct)
    {
        var result = await _contracts.DeclineByTokenAsync(token, request, ClientIp(), UserAgent(), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// The signer's address, for the audit trail.
    /// </summary>
    /// <remarks>
    /// Only the first X-Forwarded-For entry is taken; the rest are appended by
    /// intermediaries and a client can prepend anything. Recorded as evidence, and
    /// never used to decide anything — the token does that.
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

    /// <summary>Truncated to the column width, so a long header cannot fail a signature.</summary>
    private string? UserAgent()
    {
        var ua = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua)) return null;
        return ua.Length > 512 ? ua[..512] : ua;
    }
}
