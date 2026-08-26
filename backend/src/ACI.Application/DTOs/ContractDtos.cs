namespace ACI.Application.DTOs;

/// <summary>One entry in a contract's audit trail.</summary>
public record ContractEventDto(
    Guid Id,
    string Type,
    string? Detail,
    string? ActorLabel,
    DateTime AtUtc
);

/// <summary>
/// A contract as the CRM sees it.
/// </summary>
/// <param name="AllowedActions">
/// What the server would permit right now. The client has its own copy of the
/// state machine for greying out buttons, but this is the authoritative list —
/// so a client built against an older table cannot offer something refused.
/// </param>
/// <param name="UnresolvedFields">
/// Template placeholders still unfilled. Sending is refused while this is
/// non-empty, because the alternative is posting a contract that says
/// "Dear {{lead.name}},".
/// </param>
/// <param name="SigningUrl">
/// The counterparty's link, returned ONLY to the CRM user and only for a contract
/// already sent. It is included because email delivery is not guaranteed — if
/// SMTP is unconfigured the CRM user needs to be able to pass the link on by
/// hand rather than believe a send that never happened.
/// </param>
public record ContractDto(
    Guid Id,
    Guid? LeadId,
    Guid? DealId,
    string Status,
    string Title,
    string Body,
    string CounterpartyName,
    string CounterpartyEmail,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? FirstViewedAtUtc,
    string? ClientSignatureName,
    DateTime? ClientSignedAtUtc,
    string? CounterSignatureName,
    DateTime? CounterSignedAtUtc,
    DateTime? ExecutedCopySentAtUtc,
    string? ClosedReason,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<string> UnresolvedFields,
    string? SigningUrl,
    IReadOnlyList<ContractEventDto> Events
);

/// <summary>
/// What the counterparty sees on the public signing page.
/// </summary>
/// <remarks>
/// A deliberately narrow projection. It carries no ids, no lead, no audit trail
/// and no organisation internals — everything a stranger holding a link does not
/// need, and therefore must not receive.
/// </remarks>
public record PublicContractDto(
    string Status,
    string Title,
    string Body,
    string CounterpartyName,
    string OrganizationName,
    DateTime? SentAtUtc,
    string? ClientSignatureName,
    DateTime? ClientSignedAtUtc,
    string? CounterSignatureName,
    DateTime? CounterSignedAtUtc,
    /// <summary>True while this link can still be used to sign.</summary>
    bool CanSign,
    /// <summary>Why not, when it cannot — expired, already signed, voided.</summary>
    string? Blocked
);

public class CreateContractDraftRequest
{
    public Guid? LeadId { get; set; }
    public Guid? DealId { get; set; }
    public string? Title { get; set; }
    /// <summary>Overrides the organisation's template for this one draft.</summary>
    public string? TemplateOverride { get; set; }
    /// <summary>
    /// Values for placeholders the CRM cannot know — fee, term, jurisdiction.
    /// Keyed by the field name used in the template.
    /// </summary>
    public Dictionary<string, string?>? Values { get; set; }
}

public class UpdateContractRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyEmail { get; set; }
}

public class SignContractRequest
{
    /// <summary>The typed name that IS the signature.</summary>
    public string SignatureName { get; set; } = string.Empty;
    /// <summary>
    /// Must be true. Ticking it is what turns a typed name into an act of
    /// agreement, so it is required rather than implied by submitting the form.
    /// </summary>
    public bool Agreed { get; set; }
}

public class DeclineContractRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// The outcome of sending or resending.
/// </summary>
/// <param name="EmailSent">
/// Whether the message actually left. False is a normal, reportable outcome —
/// SMTP is not configured in every environment — and the contract is still sent
/// either way, so the CRM user can pass <paramref name="SigningUrl"/> on by hand.
/// Reporting a send that did not happen is the one thing this must not do.
/// </param>
public record SendContractResult(
    ContractDto Contract,
    bool EmailSent,
    string SigningUrl
);

/// <summary>A rendered contract document, ready to be sent to a browser.</summary>
/// <param name="FileName">
/// What it saves as. Already restricted to filename-safe characters, because it
/// goes into a Content-Disposition header where a quote or a semicolon in a
/// contract title would otherwise break out.
/// </param>
/// <param name="ContentType">Always <c>application/pdf</c> today.</param>
/// <param name="Content">The bytes.</param>
public sealed record ContractDocument(string FileName, string ContentType, byte[] Content)
{
    /// <summary>
    /// A <c>Content-Disposition</c> value that is legal AND keeps the real name.
    /// </summary>
    /// <remarks>
    /// The plain <c>filename</c> parameter must be ASCII: a header carrying anything
    /// else makes Kestrel throw, which is what turned every PDF download for an
    /// accented counterparty name into a 500 — and accented names are most of the
    /// names here. Stripping the accents and stopping there would hand somebody a
    /// file called "Ana-s", so both forms go out as RFC 6266 intends:
    /// <c>filename</c> as the ASCII fallback, and <c>filename*</c> as percent-encoded
    /// UTF-8, which every browser of the last decade prefers.
    ///
    /// Inline rather than attachment, because the common case is reading the
    /// contract, not filing it — a click should open the viewer, not drop a file in
    /// Downloads.
    ///
    /// Lives here rather than in each controller so the CRM download and the
    /// signer's download cannot drift apart, and so one test covers both.
    /// </remarks>
    public string InlineContentDisposition
    {
        get
        {
            var ascii = new string(FileName
                .Where(c => c is >= ' ' and < '\u007F' and not '"' and not ';' and not '\\')
                .ToArray());
            if (ascii.Length == 0) ascii = "contract.pdf";
            return $"inline; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(FileName)}";
        }
    }
}
