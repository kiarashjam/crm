using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Common.Pdf;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>
/// The four automated steps: draft, edit, send-and-sign, countersign-and-distribute.
/// </summary>
/// <remarks>
/// <para>
/// Every transition goes through <see cref="ContractStateMachine"/>. Nothing here
/// decides for itself whether an action is legal — that table is the single
/// authority, so a new endpoint cannot accidentally grant a permission nobody
/// designed.
/// </para>
/// <para>
/// The contract flow also DRIVES the lead's five-phase pipeline rather than
/// sitting beside it: sending satisfies the Contract phase, execution satisfies
/// the Signature phase, and the status auto-sync already in the product carries
/// that through to the lead's status. Without it you would get a fully executed
/// contract next to a lead still reading "Contacted".
/// </para>
/// </remarks>
public class ContractService : IContractService
{
    private readonly IContractRepository _contracts;
    private readonly ILeadRepository _leads;
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;
    private readonly IEmailSender _email;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        IContractRepository contracts,
        ILeadRepository leads,
        IOrganizationRepository organizations,
        IUserRepository users,
        IEmailSender email,
        IOptions<EmailSettings> emailSettings,
        ILogger<ContractService> logger)
    {
        _contracts = contracts;
        _leads = leads;
        _organizations = organizations;
        _users = users;
        _email = email;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /* ------------------------------------------------------- step 1: draft */

    public async Task<Result<ContractDto>> CreateDraftAsync(
        Guid userId, Guid organizationId, CreateContractDraftRequest request, CancellationToken ct = default)
    {
        var org = await _organizations.GetByIdAsync(organizationId, ct);
        var orgName = org?.Name ?? "";

        Lead? lead = null;
        if (request.LeadId.HasValue)
        {
            lead = await _leads.GetByIdAsync(request.LeadId.Value, userId, organizationId, ct);
            if (lead is null) return Result.Failure<ContractDto>(DomainErrors.Contract.LeadNotFound);
        }

        var template = string.IsNullOrWhiteSpace(request.TemplateOverride)
            ? ContractTemplate.DefaultTemplate
            : request.TemplateOverride!;

        var values = BuildMergeValues(orgName, lead, request.Values);
        var merged = ContractTemplate.Fill(template, values);

        var now = DateTime.UtcNow;
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LeadId = request.LeadId,
            DealId = request.DealId,
            Status = ContractStatuses.Draft,
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? (lead is null ? "Contract" : $"Membership Agreement — {lead.Name}")
                : request.Title!.Trim(),
            Body = merged.Body,
            CounterpartyName = lead?.Name ?? "",
            CounterpartyEmail = lead?.Email ?? "",
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _contracts.AddAsync(contract, ct);
        await LogEventAsync(contract.Id, "created", $"Draft generated from template", userId, null, null, null, ct);

        return Result.Success(await ToDtoAsync(contract, ct));
    }

    /// <summary>
    /// The values a template can draw on.
    /// </summary>
    /// <remarks>
    /// Caller-supplied values win over derived ones, so a CRM user can correct a
    /// name for this contract without editing the lead. Anything absent is left
    /// unresolved rather than blanked — see <see cref="ContractTemplate"/>.
    /// </remarks>
    private static Dictionary<string, string?> BuildMergeValues(
        string orgName, Lead? lead, IReadOnlyDictionary<string, string?>? overrides)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["org.name"] = orgName,
            ["today"] = DateTime.UtcNow.ToString("d MMMM yyyy"),
            ["lead.name"] = lead?.Name,
            ["lead.email"] = lead?.Email,
            // A clause rather than a bare value, so a lead with no phone number
            // produces a sentence that still reads correctly instead of a dangling
            // comma. The template asks for the clause, not the number.
            ["lead.phoneClause"] = string.IsNullOrWhiteSpace(lead?.Phone) ? null : $", {lead!.Phone}",
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides) values[key] = value;
        }
        return values;
    }

    /* -------------------------------------------------------- step 2: edit */

    public async Task<Result<ContractDto>> UpdateDraftAsync(
        Guid contractId, Guid userId, Guid organizationId, UpdateContractRequest request, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<ContractDto>(DomainErrors.Contract.NotFound);

        // The gate that matters most. Editing after sending would mean the
        // counterparty signs one text while we keep another.
        if (!ContractStateMachine.Can(contract.Status, ContractActions.Edit))
        {
            return Result.Failure<ContractDto>(DomainErrors.Contract.NotAllowedInThisState);
        }

        if (request.Title is not null) contract.Title = request.Title.Trim();
        if (request.Body is not null) contract.Body = request.Body;
        if (request.CounterpartyName is not null) contract.CounterpartyName = request.CounterpartyName.Trim();
        if (request.CounterpartyEmail is not null) contract.CounterpartyEmail = request.CounterpartyEmail.Trim();
        contract.UpdatedAtUtc = DateTime.UtcNow;

        await _contracts.UpdateAsync(contract, ct);
        await LogEventAsync(contract.Id, "edited", "Draft edited", userId, null, null, null, ct);

        return Result.Success(await ToDtoAsync(contract, ct));
    }

    /* -------------------------------------------------------- step 3: send */

    public async Task<Result<SendContractResult>> SendAsync(
        Guid contractId, Guid userId, Guid organizationId, bool resend, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<SendContractResult>(DomainErrors.Contract.NotFound);

        var action = resend ? ContractActions.Resend : ContractActions.Send;
        if (!ContractStateMachine.Can(contract.Status, action))
        {
            return Result.Failure<SendContractResult>(DomainErrors.Contract.NotAllowedInThisState);
        }

        if (string.IsNullOrWhiteSpace(contract.Body))
            return Result.Failure<SendContractResult>(DomainErrors.Contract.BodyRequired);
        if (string.IsNullOrWhiteSpace(contract.CounterpartyEmail))
            return Result.Failure<SendContractResult>(DomainErrors.Contract.CounterpartyEmailRequired);
        if (!contract.CounterpartyEmail.Contains('@'))
            return Result.Failure<SendContractResult>(DomainErrors.Contract.CounterpartyEmailInvalid);

        // Refuse to post a contract that still says "Dear {{lead.name}},".
        var unresolved = UnresolvedIn(contract.Body);
        if (unresolved.Count > 0)
            return Result.Failure<SendContractResult>(DomainErrors.Contract.HasUnresolvedFields);

        var now = DateTime.UtcNow;

        // A resend necessarily ROTATES the token, and the state machine allows it
        // only from `sent`. Only the hash is stored, so the raw value in the first
        // email cannot be recovered to send again — there is no version of this
        // that reuses the old link. Rotating is the honest option: exactly one link
        // is ever live, and a resend says which. The alternative, storing the raw
        // token so it could be re-sent, would put a signable secret in the database.
        var rawToken = ContractSigningToken.CreateRawToken();
        contract.SigningTokenHash = ContractSigningToken.HashRawToken(rawToken);
        contract.SigningTokenExpiresAtUtc = now.Add(ContractSigningToken.Lifetime);
        if (resend)
        {
            _logger.LogInformation(
                "Contract {ContractId} resent; any previous signing link is now dead", contract.Id);
        }

        contract.Status = ContractStatuses.Sent;
        contract.SentAtUtc ??= now;
        contract.SentByUserId = userId;
        // Frozen here, and never recomputed. If the stored body were ever altered
        // after this point, it would no longer match.
        contract.BodyHashAtSend = ContractSigningToken.HashBody(contract.Body);
        contract.UpdatedAtUtc = now;

        await _contracts.UpdateAsync(contract, ct);

        var org = await _organizations.GetByIdAsync(organizationId, ct);
        var signingUrl = BuildSigningUrl(rawToken);

        var emailSent = await _email.SendContractForSignatureEmailAsync(
            contract.CounterpartyEmail, contract.CounterpartyName,
            org?.Name ?? "", contract.Title, signingUrl,
            // The whole agreement, so they can read it before deciding to follow a
            // link. Watermarked unsigned, so it cannot be mistaken for the executed
            // copy that arrives later.
            DocumentAttachment(contract, org?.Name ?? ""), ct);

        await LogEventAsync(
            contract.Id,
            resend ? "resent" : "sent",
            emailSent
                ? $"Sent to {contract.CounterpartyEmail}"
                // Recorded as what actually happened. The contract IS sent — the
                // link is live — but nobody has been told, and that distinction has
                // to survive into the audit trail.
                : $"Link created for {contract.CounterpartyEmail}, but the email could not be sent",
            userId, null, null, null, ct);

        await ApplyPipelineEffectAsync(contract, userId, organizationId, ct);

        var dto = await ToDtoAsync(contract, ct);
        return Result.Success(new SendContractResult(dto with { SigningUrl = signingUrl }, emailSent, signingUrl));
    }

    /* ------------------------------------ step 3b: the counterparty signs */

    public async Task<Result<PublicContractDto>> GetByTokenAsync(
        string rawToken, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var (contract, error) = await ResolveTokenAsync(rawToken, ct);
        if (contract is null) return Result.Failure<PublicContractDto>(error);

        if (contract.FirstViewedAtUtc is null)
        {
            contract.FirstViewedAtUtc = DateTime.UtcNow;
            await _contracts.UpdateAsync(contract, ct);
            await LogEventAsync(contract.Id, "viewed", "Opened by the counterparty",
                null, contract.CounterpartyName, ip, userAgent, ct);
        }

        return Result.Success(await ToPublicDtoAsync(contract, ct));
    }

    public async Task<Result<PublicContractDto>> SignByTokenAsync(
        string rawToken, SignContractRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var (contract, error) = await ResolveTokenAsync(rawToken, ct);
        if (contract is null) return Result.Failure<PublicContractDto>(error);

        if (!ContractStateMachine.Can(contract.Status, ContractActions.ClientSign))
            return Result.Failure<PublicContractDto>(DomainErrors.Contract.NotAllowedInThisState);
        if (!ContractStateMachine.IsSignatureNameValid(request.SignatureName))
            return Result.Failure<PublicContractDto>(DomainErrors.Contract.SignatureNameRequired);
        // Required rather than implied by submitting: the tick is what turns a
        // typed name into an act of agreement.
        if (!request.Agreed)
            return Result.Failure<PublicContractDto>(DomainErrors.Contract.ConsentRequired);

        var now = DateTime.UtcNow;
        contract.Status = ContractStatuses.SignedByClient;
        contract.ClientSignatureName = request.SignatureName.Trim();
        contract.ClientSignedAtUtc = now;
        contract.ClientSignatureIp = ip;
        contract.ClientSignatureUserAgent = userAgent;
        contract.UpdatedAtUtc = now;
        await _contracts.UpdateAsync(contract, ct);

        await LogEventAsync(contract.Id, "signed",
            $"Signed by {contract.ClientSignatureName}", null, contract.ClientSignatureName, ip, userAgent, ct);

        await NotifyOwnerOfSignatureAsync(contract, ct);

        return Result.Success(await ToPublicDtoAsync(contract, ct));
    }

    public async Task<Result<PublicContractDto>> DeclineByTokenAsync(
        string rawToken, DeclineContractRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var (contract, error) = await ResolveTokenAsync(rawToken, ct);
        if (contract is null) return Result.Failure<PublicContractDto>(error);

        if (!ContractStateMachine.Can(contract.Status, ContractActions.Decline))
            return Result.Failure<PublicContractDto>(DomainErrors.Contract.NotAllowedInThisState);

        var now = DateTime.UtcNow;
        contract.Status = ContractStatuses.Declined;
        contract.ClosedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason!.Trim();
        contract.UpdatedAtUtc = now;
        await _contracts.UpdateAsync(contract, ct);

        await LogEventAsync(contract.Id, "declined",
            contract.ClosedReason ?? "Declined by the counterparty",
            null, contract.CounterpartyName, ip, userAgent, ct);

        // Their decision, and the pipeline has a word for it.
        await ApplyPipelineEffectAsync(contract, contract.CreatedByUserId, contract.OrganizationId, ct);

        return Result.Success(await ToPublicDtoAsync(contract, ct));
    }

    /* ------------------------------------------------- step 4: countersign */

    public async Task<Result<ContractDto>> CountersignAsync(
        Guid contractId, Guid userId, Guid organizationId, SignContractRequest request, string? ip, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<ContractDto>(DomainErrors.Contract.NotFound);

        // Refused unless the counterparty has already signed, so an "executed"
        // contract can never carry only our own signature.
        if (!ContractStateMachine.Can(contract.Status, ContractActions.Countersign))
            return Result.Failure<ContractDto>(DomainErrors.Contract.NotAllowedInThisState);
        if (!ContractStateMachine.IsSignatureNameValid(request.SignatureName))
            return Result.Failure<ContractDto>(DomainErrors.Contract.SignatureNameRequired);
        if (!request.Agreed)
            return Result.Failure<ContractDto>(DomainErrors.Contract.ConsentRequired);

        var now = DateTime.UtcNow;
        contract.Status = ContractStatuses.Countersigned;
        contract.CounterSignatureName = request.SignatureName.Trim();
        contract.CounterSignedAtUtc = now;
        contract.CounterSignedByUserId = userId;
        contract.CounterSignatureIp = ip;
        contract.UpdatedAtUtc = now;
        await _contracts.UpdateAsync(contract, ct);

        await LogEventAsync(contract.Id, "countersigned",
            $"Countersigned by {contract.CounterSignatureName}", userId, contract.CounterSignatureName, ip, null, ct);

        await ApplyPipelineEffectAsync(contract, userId, organizationId, ct);
        await SendExecutedCopiesAsync(contract, userId, ct);

        return Result.Success(await ToDtoAsync(contract, ct));
    }

    public async Task<Result<bool>> ResendExecutedCopyAsync(
        Guid contractId, Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<bool>(DomainErrors.Contract.NotFound);
        if (contract.Status != ContractStatuses.Countersigned)
            return Result.Failure<bool>(DomainErrors.Contract.NotAllowedInThisState);

        var sent = await SendExecutedCopiesAsync(contract, userId, ct);
        return Result.Success(sent);
    }

    /// <summary>
    /// Emails the executed contract to both parties.
    /// </summary>
    /// <remarks>
    /// <see cref="Contract.ExecutedCopySentAtUtc"/> is stamped only when BOTH
    /// messages went. A half-delivered execution is not a delivered one, and
    /// leaving the stamp null is what makes the retry button meaningful.
    /// </remarks>
    private async Task<bool> SendExecutedCopiesAsync(Contract contract, Guid userId, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(contract.OrganizationId, ct);
        var orgName = org?.Name ?? "";
        var block = BuildSignatureBlock(contract, orgName);

        // Rendered once and attached to both messages, so the two parties are
        // demonstrably holding the same file.
        var document = DocumentAttachment(contract, orgName);

        var toClient = await _email.SendExecutedContractEmailAsync(
            contract.CounterpartyEmail, contract.CounterpartyName,
            orgName, contract.Title, contract.Body, block, document, ct);

        var us = await _users.GetByIdAsync(userId, ct);
        var toUs = false;
        if (us is not null && !string.IsNullOrWhiteSpace(us.Email))
        {
            toUs = await _email.SendExecutedContractEmailAsync(
                us.Email, us.Name ?? "", orgName, contract.Title, contract.Body, block, document, ct);
        }

        var both = toClient && toUs;
        if (both)
        {
            contract.ExecutedCopySentAtUtc = DateTime.UtcNow;
            await _contracts.UpdateAsync(contract, ct);
        }

        await LogEventAsync(contract.Id, "emailed",
            both
                ? $"Executed copy sent to {contract.CounterpartyEmail} and {us?.Email}"
                : $"Executed copy could NOT be sent to all parties (counterparty: {(toClient ? "sent" : "failed")}, us: {(toUs ? "sent" : "failed")})",
            userId, null, null, null, ct);

        if (!both)
        {
            _logger.LogWarning(
                "Contract {ContractId} is executed but the copy was not delivered to every party", contract.Id);
        }
        return both;
    }

    /// <summary>
    /// The signature record that travels with the executed contract.
    /// </summary>
    /// <remarks>
    /// This, not the typed names on their own, is what makes a simple electronic
    /// signature worth anything: who signed, when, and the hash of the exact text
    /// they were shown. The hash is the tamper-evidence — a recipient can be told
    /// what it was without needing access to the database.
    /// </remarks>
    private static string BuildSignatureBlock(Contract contract, string orgName)
    {
        var lines = new List<string> { "SIGNATURE RECORD", "" };

        lines.Add($"Counterparty : {contract.ClientSignatureName}");
        lines.Add($"Signed       : {Fmt(contract.ClientSignedAtUtc)}");
        if (!string.IsNullOrWhiteSpace(contract.ClientSignatureIp))
            lines.Add($"From         : {contract.ClientSignatureIp}");
        lines.Add("");
        lines.Add($"{(string.IsNullOrWhiteSpace(orgName) ? "Counterparty 2" : orgName),-13}: {contract.CounterSignatureName}");
        lines.Add($"Signed       : {Fmt(contract.CounterSignedAtUtc)}");
        lines.Add("");
        lines.Add($"Document hash: {contract.BodyHashAtSend ?? "(not recorded)"}");
        lines.Add("");
        lines.Add("This is a simple electronic signature: a typed name recorded with its");
        lines.Add("timestamp, origin and a hash of the exact text signed. It is not a");
        lines.Add("qualified electronic signature under ZertES or eIDAS.");

        return string.Join('\n', lines);

        static string Fmt(DateTime? at) =>
            at is null ? "(not signed)" : at.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    private async Task NotifyOwnerOfSignatureAsync(Contract contract, CancellationToken ct)
    {
        var owner = await _users.GetByIdAsync(contract.CreatedByUserId, ct);
        if (owner is null || string.IsNullOrWhiteSpace(owner.Email)) return;

        var url = BuildAppUrl($"/leads/{contract.LeadId}");
        var sent = await _email.SendContractSignedNotificationAsync(
            owner.Email, owner.Name ?? "", contract.CounterpartyName, contract.Title, url, ct);

        if (!sent)
        {
            // Not fatal — the CRM shows the state anyway — but it must not pass
            // silently, because the countersignature is now waiting on someone who
            // has not been told.
            _logger.LogWarning(
                "Contract {ContractId} was signed but {Email} could not be notified", contract.Id, owner.Email);
        }
    }

    public async Task<Result<ContractDocument>> GetDocumentAsync(
        Guid contractId, Guid organizationId, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<ContractDocument>(DomainErrors.Contract.NotFound);

        var org = await _organizations.GetByIdAsync(organizationId, ct);
        return Document(contract, org?.Name ?? "");
    }

    public async Task<Result<ContractDocument>> GetDocumentByTokenAsync(
        string rawToken, CancellationToken ct = default)
    {
        var (contract, error) = await ResolveTokenAsync(rawToken, ct);
        if (contract is null) return Result.Failure<ContractDocument>(error);

        var org = await _organizations.GetByIdAsync(contract.OrganizationId, ct);
        return Document(contract, org?.Name ?? "");
    }

    /// <summary>Wraps the rendered document, or reports that it could not be made.</summary>
    private Result<ContractDocument> Document(Contract contract, string orgName)
    {
        var rendered = RenderDocument(contract, orgName);
        return rendered is null
            // Reported rather than returning an empty file: a browser handed zero
            // bytes named ".pdf" shows a broken document with no explanation.
            ? Result.Failure<ContractDocument>(DomainErrors.Contract.DocumentUnavailable)
            : Result.Success(new ContractDocument(
                DocumentFileName(contract), "application/pdf", rendered.Bytes));
    }

    /* ------------------------------------------------------ the document */

    /// <summary>
    /// The typeset copy of a contract, or null if it could not be produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rendering is pure and in-memory, so the only way it fails is a defect. But a
    /// defect here must not be able to stop a contract being signed or sent — the
    /// plain text is the instrument, and the PDF is a copy of it. So this swallows
    /// and logs rather than throwing, and every caller treats null as "send it
    /// without the attachment".
    /// </para>
    /// <para>
    /// A document whose party names could not be carried by the encoding is logged
    /// as a warning and still sent: a copy with a substituted character is more use
    /// than no copy, and the plain text in the same email is exact.
    /// </para>
    /// </remarks>
    private ContractPdfResult? RenderDocument(Contract contract, string orgName)
    {
        try
        {
            var result = ContractPdf.Render(new ContractPdfRequest(
                contract.Title, contract.Body, orgName, contract.CounterpartyName,
                contract.Status,
                Reference: Reference(contract.Id),
                BodyHash: contract.BodyHashAtSend,
                GeneratedAtUtc: DateTime.UtcNow,
                ClientSignatureName: contract.ClientSignatureName,
                ClientSignedAtUtc: contract.ClientSignedAtUtc,
                ClientSignatureIp: contract.ClientSignatureIp,
                CounterSignatureName: contract.CounterSignatureName,
                CounterSignedAtUtc: contract.CounterSignedAtUtc,
                CounterSignatureIp: contract.CounterSignatureIp));

            if (result.UnrepresentableCharacters.Count > 0)
            {
                _logger.LogWarning(
                    "Contract {ContractId} PDF substituted {Count} character(s) the document "
                    + "encoding cannot carry: {Characters}. The emailed text is exact.",
                    contract.Id, result.UnrepresentableCharacters.Count,
                    string.Join(" ", result.UnrepresentableCharacters));
            }
            return result;
        }
        catch (Exception ex)
        {
            // Never fatal. Losing the attachment costs a nicety; failing the
            // countersignature because of it would cost the contract.
            _logger.LogError(ex, "Could not render the PDF for contract {ContractId}", contract.Id);
            return null;
        }
    }

    /// <summary>The document as an email attachment, or null.</summary>
    private EmailAttachment? DocumentAttachment(Contract contract, string orgName)
    {
        var rendered = RenderDocument(contract, orgName);
        return rendered is null
            ? null
            : new EmailAttachment(DocumentFileName(contract), "application/pdf", rendered.Bytes);
    }

    /// <summary>
    /// What the file is called when it lands in somebody's downloads folder.
    /// </summary>
    /// <remarks>
    /// Built from the title, the reference and the state, because "contract.pdf" in a
    /// folder of thirty contracts is worth nothing. Restricted to characters that are
    /// safe in a filename on every platform and in a Content-Disposition header —
    /// a quote or a semicolon in a contract title would otherwise let the title
    /// break out of the header.
    /// </remarks>
    internal static string DocumentFileName(Contract contract)
    {
        var stem = new string((contract.Title ?? "").Trim()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        while (stem.Contains("--", StringComparison.Ordinal)) stem = stem.Replace("--", "-");
        stem = stem.Trim('-');
        if (stem.Length > 60) stem = stem[..60].TrimEnd('-');
        if (stem.Length == 0) stem = "Contract";

        var state = contract.Status == ContractStatuses.Countersigned ? "signed" : contract.Status;
        return $"{stem}-{Reference(contract.Id)}-{state}.pdf";
    }

    /// <summary>A short, quotable reference for a contract. The first block of its id.</summary>
    internal static string Reference(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    /* ------------------------------------------------------------- voiding */

    public async Task<Result<ContractDto>> VoidAsync(
        Guid contractId, Guid userId, Guid organizationId, string? reason, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<ContractDto>(DomainErrors.Contract.NotFound);
        if (!ContractStateMachine.Can(contract.Status, ContractActions.Void))
            return Result.Failure<ContractDto>(DomainErrors.Contract.NotAllowedInThisState);

        contract.Status = ContractStatuses.Voided;
        contract.ClosedReason = string.IsNullOrWhiteSpace(reason) ? null : reason!.Trim();
        // The link dies with it, so a stale copy in an inbox cannot be signed.
        contract.SigningTokenHash = null;
        contract.SigningTokenExpiresAtUtc = null;
        contract.UpdatedAtUtc = DateTime.UtcNow;
        await _contracts.UpdateAsync(contract, ct);

        await LogEventAsync(contract.Id, "voided", contract.ClosedReason ?? "Voided", userId, null, null, null, ct);
        // Deliberately no pipeline effect: voiding is our decision, not the
        // customer's, and recording it against them would corrupt the drop-off report.
        return Result.Success(await ToDtoAsync(contract, ct));
    }

    /* ---------------------------------------------------------- read paths */

    public async Task<Result<ContractDto>> GetAsync(Guid contractId, Guid organizationId, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(contractId, organizationId, ct);
        if (contract is null) return Result.Failure<ContractDto>(DomainErrors.Contract.NotFound);
        return Result.Success(await ToDtoAsync(contract, ct));
    }

    public async Task<IReadOnlyList<ContractDto>> ListForLeadAsync(
        Guid leadId, Guid organizationId, CancellationToken ct = default)
    {
        var list = await _contracts.GetForLeadAsync(leadId, organizationId, ct);
        var dtos = new List<ContractDto>(list.Count);
        foreach (var c in list) dtos.Add(await ToDtoAsync(c, ct));
        return dtos;
    }

    /* -------------------------------------------------------------- shared */

    /// <summary>
    /// Resolves a raw signing token to its contract, or the reason it will not.
    /// </summary>
    /// <remarks>
    /// The token is hashed and looked up by hash, so the raw value is never
    /// compared in application code and a leaked row cannot be replayed. An
    /// unknown token and a malformed one are given the SAME answer, so the
    /// endpoint cannot be used to discover which tokens exist.
    /// </remarks>
    private async Task<(Contract? Contract, Error Error)> ResolveTokenAsync(string rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return (null, DomainErrors.Contract.LinkInvalid);

        var hash = ContractSigningToken.HashRawToken(rawToken.Trim());
        var contract = await _contracts.GetBySigningTokenHashAsync(hash, ct);
        if (contract is null) return (null, DomainErrors.Contract.LinkInvalid);

        if (contract.SigningTokenExpiresAtUtc is null || contract.SigningTokenExpiresAtUtc < DateTime.UtcNow)
        {
            return (null, DomainErrors.Contract.LinkExpired);
        }
        return (contract, Error.None);
    }

    private static IReadOnlyList<string> UnresolvedIn(string body)
        // Anything still in {{field}} form after a merge is by definition unfilled.
        => ContractTemplate.FieldsUsed(body);

    /// <summary>
    /// Writes the pipeline consequence of the contract's current status onto its lead.
    /// </summary>
    /// <remarks>
    /// The pipeline is a JSON blob the frontend owns, so this merges into it by key
    /// rather than replacing it — anything else would wipe the outreach and meeting
    /// phases the moment a contract was sent.
    /// </remarks>
    private async Task ApplyPipelineEffectAsync(Contract contract, Guid userId, Guid organizationId, CancellationToken ct)
    {
        if (contract.LeadId is null) return;

        var effect = PipelineEffectFor(contract.Status);
        if (effect.Count == 0) return;

        var lead = await _leads.GetByIdAsync(contract.LeadId.Value, userId, organizationId, ct);
        if (lead is null) return;

        JsonObject state;
        try
        {
            state = string.IsNullOrWhiteSpace(lead.PipelineState)
                ? new JsonObject()
                : JsonNode.Parse(lead.PipelineState!)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            // A corrupt blob must not stop the contract flow, and must not be
            // silently replaced either — start fresh but say so.
            _logger.LogWarning("Lead {LeadId} has unparseable pipelineState; starting a fresh object", lead.Id);
            state = new JsonObject();
        }

        foreach (var (key, value) in effect) state[key] = value;

        lead.PipelineState = state.ToJsonString();
        await _leads.UpdateAsync(lead, userId, organizationId, ct);
        _logger.LogInformation(
            "Contract {ContractId} advanced lead {LeadId} pipeline: {Keys}",
            contract.Id, lead.Id, string.Join(", ", effect.Keys));
    }

    /// <summary>
    /// Mirrors <c>pipelineEffect</c> in <c>contractLifecycle.ts</c>.
    /// </summary>
    private static Dictionary<string, string> PipelineEffectFor(string status)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return status switch
        {
            ContractStatuses.Sent => new() { ["contractStatus"] = "yes", ["contractSentDate"] = today },
            ContractStatuses.Countersigned => new() { ["contractSigned"] = "yes", ["signatureDate"] = today },
            ContractStatuses.Declined => new() { ["contractSigned"] = "no" },
            // Voided writes nothing: our decision, not theirs.
            _ => new(),
        };
    }

    private string BuildAppUrl(string path)
    {
        var b = (_emailSettings.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return b.Length == 0 ? path : b + path;
    }

    private string BuildSigningUrl(string rawToken) => BuildAppUrl($"/sign/{rawToken}");

    private async Task LogEventAsync(
        Guid contractId, string type, string? detail, Guid? actorUserId,
        string? actorLabel, string? ip, string? userAgent, CancellationToken ct)
    {
        await _contracts.AddEventAsync(new ContractEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            Type = type,
            Detail = detail,
            ActorUserId = actorUserId,
            ActorLabel = actorLabel,
            Ip = ip,
            UserAgent = userAgent,
            AtUtc = DateTime.UtcNow,
        }, ct);
    }

    private async Task<ContractDto> ToDtoAsync(Contract c, CancellationToken ct)
    {
        var events = await _contracts.GetEventsAsync(c.Id, ct);
        return new ContractDto(
            c.Id, c.LeadId, c.DealId, c.Status, c.Title, c.Body,
            c.CounterpartyName, c.CounterpartyEmail,
            c.CreatedAtUtc, c.UpdatedAtUtc, c.SentAtUtc, c.FirstViewedAtUtc,
            c.ClientSignatureName, c.ClientSignedAtUtc,
            c.CounterSignatureName, c.CounterSignedAtUtc,
            c.ExecutedCopySentAtUtc, c.ClosedReason,
            ContractStateMachine.AllowedActions(c.Status),
            UnresolvedIn(c.Body),
            // Always null on a read. The raw token is never stored, so a signing URL
            // can only be produced by the call that minted it — SendAsync attaches
            // it with `dto with { SigningUrl = ... }`.
            null,
            events.Select(e => new ContractEventDto(e.Id, e.Type, e.Detail, e.ActorLabel, e.AtUtc)).ToList());
    }

    private async Task<PublicContractDto> ToPublicDtoAsync(Contract c, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(c.OrganizationId, ct);
        var canSign = ContractStateMachine.Can(c.Status, ContractActions.ClientSign);
        var blocked = canSign
            ? null
            : c.Status switch
            {
                ContractStatuses.SignedByClient => "You have already signed this contract.",
                ContractStatuses.Countersigned => "This contract is fully signed by both parties.",
                ContractStatuses.Declined => "This contract was declined.",
                ContractStatuses.Voided => "This contract was withdrawn.",
                ContractStatuses.Draft => "This contract has not been sent yet.",
                _ => "This contract can no longer be signed.",
            };

        return new PublicContractDto(
            c.Status, c.Title, c.Body, c.CounterpartyName, org?.Name ?? "",
            c.SentAtUtc, c.ClientSignatureName, c.ClientSignedAtUtc,
            c.CounterSignatureName, c.CounterSignedAtUtc, canSign, blocked);
    }
}
