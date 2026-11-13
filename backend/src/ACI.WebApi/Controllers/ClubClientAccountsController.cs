using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The house account of a client who is not a member: a company ("companies") or a private
/// client ("contacts"). Charges and payments on it go through the usual money routes
/// (/api/charges and /api/payments, naming the company's or the contact's id as the member);
/// this answers what is particular to it: whether it is open, opening it, and its statement.
/// </summary>
[ApiController]
[Route("api/club-accounts/{kind}/{id:guid}")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class ClubClientAccountsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly IClubBilling _billing;
    private readonly ICorporateMemberships _corporate;
    private readonly ICurrentUserService _user;

    public ClubClientAccountsController(IClubLedger ledger, IClubBilling billing, ICorporateMemberships corporate, ICurrentUserService user)
    {
        _ledger = ledger;
        _billing = billing;
        _corporate = corporate;
        _user = user;
    }

    /// <summary>The account, open or not, with its balance (negative while the client owes the club).</summary>
    [HttpGet]
    public async Task<ActionResult> Get(string kind, Guid id, CancellationToken ct)
    {
        var account = await _ledger.ClientAccountAsync(_user.CurrentOrganizationId, kind, id, ct);
        return account.IsSuccess ? Ok(account.Value) : account.Error.ToProblemResult();
    }

    /// <summary>Opens the account, once; opening it again answers it as it is.</summary>
    [HttpPost]
    public async Task<ActionResult> Open(string kind, Guid id, CancellationToken ct)
    {
        var account = await _ledger.OpenClientAccountAsync(_user.CurrentOrganizationId, kind, id, ct);
        return account.IsSuccess ? Ok(account.Value) : account.Error.ToProblemResult();
    }

    /// <summary>The opening balance, each entry from <paramref name="from"/> to <paramref name="to"/> with the balance after it, and the closing balance.</summary>
    [HttpGet("statement")]
    public async Task<ActionResult> Statement(string kind, Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (await OpenAccountAsync(kind, id, ct) is { } refused) return refused;
        var statement = await _ledger.StatementAsync(_user.CurrentOrganizationId, id, from.ToUniversalTime(), to.ToUniversalTime(), ct);
        return statement.IsSuccess ? Ok(statement.Value) : statement.Error.ToProblemResult();
    }

    /// <summary>The statement as a PDF, with the QR-bill for what is due; <paramref name="language"/> en, de or fr.</summary>
    [HttpGet("statement.pdf")]
    [Produces("application/pdf", "application/json")]
    public async Task<ActionResult> StatementPdf(string kind, Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? language,
        [FromQuery] string? email, CancellationToken ct)
    {
        if (await OpenAccountAsync(kind, id, ct) is { } refused) return refused;
        // A company with a corporate membership has its statements go where, and in the language, the membership says.
        var corporate = (await _corporate.ListAsync(_user.CurrentOrganizationId!.Value,
            new CorporateMembershipQuery { CompanyId = id, Status = Domain.Entities.CorporateMembershipStatuses.Active }, ct)).FirstOrDefault();
        var file = await _billing.ClientStatementPdfAsync(_user.CurrentOrganizationId!.Value, id, from.ToUniversalTime(), to.ToUniversalTime(),
            language ?? corporate?.Language, email ?? corporate?.BillingEmail, ct);
        if (!file.IsSuccess) return file.Error.ToProblemResult();
        return File(file.Value.Content, "application/pdf", file.Value.FileName);
    }

    /// <summary>
    /// The account must be this workspace's client's, and open: a statement read by an id alone
    /// would answer any member's account under the company's route.
    /// </summary>
    private async Task<ActionResult?> OpenAccountAsync(string kind, Guid id, CancellationToken ct)
    {
        var account = await _ledger.ClientAccountAsync(_user.CurrentOrganizationId, kind, id, ct);
        if (!account.IsSuccess) return account.Error.ToProblemResult();
        return account.Value.Open ? null : DomainErrors.ClientAccount.NotOpen.ToProblemResult();
    }
}
