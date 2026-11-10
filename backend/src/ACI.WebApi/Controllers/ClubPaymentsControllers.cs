using ACI.Application.Common;
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

/// <summary>A bank file to import: its name and its XML, as the browser read it.</summary>
public sealed class BankFileRequest
{
    public string? FileName { get; set; }
    public string? Content { get; set; }
}

/// <summary>
/// The bank's camt files: importing one records the QR-bill payments it brings in, once each;
/// what names nobody waits in the unmatched list to be allocated to a member or set aside.
/// </summary>
[ApiController]
[Route("api/bank")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, What = "work with the bank's files")]
public class BankFilesController : ControllerBase
{
    /// <summary>Larger than a busy club's month of camt files.</summary>
    private const long MaxRequestBytes = 20 * 1024 * 1024;

    private readonly IClubBankFiles _bank;
    private readonly ICurrentUserService _user;

    public BankFilesController(IClubBankFiles bank, ICurrentUserService user)
    {
        _bank = bank;
        _user = user;
    }

    /// <summary>Imports a camt.054 (or .053, .052) file, recording every credit that quotes one of the club's QR-bill references.</summary>
    [HttpPost("imports")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult> Import([FromBody] BankFileRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Content))
            return DomainErrors.Payments.BankFileInvalid("Choose the camt file your bank gave you.").ToProblemResult();
        using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Content));
        var imported = await _bank.ImportAsync(_user.CurrentOrganizationId!.Value, userId, request.FileName ?? "", content, ct);
        return imported.IsSuccess ? Ok(imported.Value) : imported.Error.ToProblemResult();
    }

    [HttpGet("imports")]
    public async Task<ActionResult<IReadOnlyList<BankImportDto>>> Imports([FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await _bank.ImportsAsync(_user.CurrentOrganizationId!.Value, take, ct));

    /// <summary>Credits read from bank files, newest first: unmatched ones to allocate, or by file.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<BankTransactionDto>>> Transactions([FromQuery] string? status, [FromQuery] Guid? importId, CancellationToken ct) =>
        Ok(await _bank.TransactionsAsync(_user.CurrentOrganizationId!.Value, status, importId, ct));

    /// <summary>Says whose an unmatched transfer is: it is recorded as their payment.</summary>
    [HttpPost("transactions/{id:guid}/allocate")]
    public async Task<ActionResult> Allocate(Guid id, [FromBody] AllocateBankTransactionRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        var allocated = await _bank.AllocateAsync(_user.CurrentOrganizationId!.Value, userId, id, request.MemberId, version, ct);
        return allocated.IsSuccess ? Ok(allocated.Value) : allocated.Error.ToProblemResult();
    }

    /// <summary>Sets an unmatched transfer aside as no member's payment, saying why.</summary>
    [HttpPost("transactions/{id:guid}/ignore")]
    public async Task<ActionResult> Ignore(Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        var ignored = await _bank.IgnoreAsync(_user.CurrentOrganizationId!.Value, userId, id, request.Reason, version, ct);
        return ignored.IsSuccess ? Ok(ignored.Value) : ignored.Error.ToProblemResult();
    }
}

/// <summary>
/// A member's online payments: a link to pay what they owe or to save a card, the card they
/// saved, their dues charged to it, and what Stripe said of each attempt.
/// </summary>
[ApiController]
[Route("api/members/{memberId:guid}")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class MemberPaymentsController : ControllerBase
{
    private readonly IClubPayments _payments;
    private readonly ICurrentUserService _user;

    public MemberPaymentsController(IClubPayments payments, ICurrentUserService user)
    {
        _payments = payments;
        _user = user;
    }

    /// <summary>The member's saved card (what Stripe shows of it), whether their dues are charged to it, and their latest online payments.</summary>
    [HttpGet("payment-profile")]
    public async Task<ActionResult> Profile(Guid memberId, CancellationToken ct)
    {
        var profile = await _payments.ProfileAsync(_user.CurrentOrganizationId!.Value, memberId, ct);
        if (!profile.IsSuccess) return profile.Error.ToProblemResult();
        Response.SetETag(profile.Value.Version);
        return Ok(profile.Value);
    }

    /// <summary>A link the member pays on (what they owe, or some charges) or saves a card on.</summary>
    [HttpPost("pay-link")]
    [Requires(MemberPermissions.ManageMoney, What = "send a member a payment link")]
    public async Task<ActionResult> PayLink(Guid memberId, [FromBody] PayLinkRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var link = await _payments.CreatePayLinkAsync(_user.CurrentOrganizationId!.Value, userId, memberId, request, ct);
        return link.IsSuccess ? Ok(link.Value) : link.Error.ToProblemResult();
    }

    /// <summary>The same link, emailed to the member through the outbox.</summary>
    [HttpPost("pay-link/email")]
    [Requires(MemberPermissions.ManageMoney, What = "send a member a payment link")]
    public async Task<ActionResult> EmailPayLink(Guid memberId, [FromBody] PayLinkRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var link = await _payments.EmailPayLinkAsync(_user.CurrentOrganizationId!.Value, userId, memberId, request, ct);
        return link.IsSuccess ? Ok(link.Value) : link.Error.ToProblemResult();
    }

    /// <summary>Removes the saved card, at Stripe too; dues stop being charged to it.</summary>
    [HttpDelete("card")]
    [Requires(MemberPermissions.ManageMoney, What = "remove a member's saved card")]
    public async Task<ActionResult> RemoveCard(Guid memberId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var removed = await _payments.RemoveCardAsync(_user.CurrentOrganizationId!.Value, userId, memberId, ct);
        return removed.IsSuccess ? Ok(removed.Value) : removed.Error.ToProblemResult();
    }

    /// <summary>Turns charging the member's dues to their saved card on (they agreed) or off.</summary>
    [HttpPut("autopay")]
    [Requires(MemberPermissions.ManageMoney, What = "change how a member pays their dues")]
    public async Task<ActionResult> AutoPay(Guid memberId, [FromBody] AutoPayRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        var saved = await _payments.SetAutoPayAsync(_user.CurrentOrganizationId!.Value, userId, memberId, request, ct);
        return saved.IsSuccess ? Ok(saved.Value) : saved.Error.ToProblemResult();
    }

    /// <summary>Charges the member's open charges (or those named) to their saved card now.</summary>
    [HttpPost("card/charge")]
    [Requires(MemberPermissions.ManageMoney, What = "charge a member's saved card")]
    public async Task<ActionResult> ChargeCard(Guid memberId, [FromBody] ChargeSavedCardRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var charged = await _payments.ChargeSavedCardAsync(_user.CurrentOrganizationId!.Value, userId, memberId, request, ct);
        return charged.IsSuccess ? Ok(charged.Value) : charged.Error.ToProblemResult();
    }
}

/// <summary>Online payments across the workspace, failed ones included, newest first.</summary>
[ApiController]
[Route("api/online-payments")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class OnlinePaymentsController : ControllerBase
{
    private readonly IClubPayments _payments;
    private readonly ICurrentUserService _user;

    public OnlinePaymentsController(IClubPayments payments, ICurrentUserService user)
    {
        _payments = payments;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OnlinePaymentDto>>> List([FromQuery] Guid? memberId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await _payments.OnlinePaymentsAsync(_user.CurrentOrganizationId!.Value, memberId, status, ct));
}

/// <summary>The accounting journal for a period, as bookings Bexio, Abacus or another program imports.</summary>
[ApiController]
[Route("api/accounting")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json", "text/csv")]
public class AccountingController : ControllerBase
{
    private readonly IClubAccounting _accounting;
    private readonly ICurrentUserService _user;

    public AccountingController(IClubAccounting accounting, ICurrentUserService user)
    {
        _accounting = accounting;
        _user = user;
    }

    /// <summary>
    /// The bookings for [from, to): as JSON with what each account moved by, or (format=csv) as
    /// the CSV file docs/accounting-export.md describes. layout=gross books a charge with its VAT
    /// code; layout=net books its VAT on a line of its own.
    /// </summary>
    [HttpGet("journal")]
    [Requires(MemberPermissions.ManageMoney, What = "export the accounting journal")]
    public async Task<ActionResult> Journal([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? layout, [FromQuery] string? format, CancellationToken ct)
    {
        var journal = await _accounting.JournalAsync(_user.CurrentOrganizationId!.Value, from.ToUniversalTime(), to.ToUniversalTime(), layout ?? "gross", ct);
        if (!journal.IsSuccess) return journal.Error.ToProblemResult();
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)) return Ok(journal.Value);
        var name = $"journal-{journal.Value.FromUtc:yyyy-MM-dd}-to-{journal.Value.ToUtc.AddTicks(-1):yyyy-MM-dd}.csv";
        return File(ACI.Application.Common.Payments.JournalCsv.Write(journal.Value.Rows), "text/csv; charset=utf-8", name);
    }
}
