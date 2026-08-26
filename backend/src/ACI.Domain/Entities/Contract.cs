namespace ACI.Domain.Entities;

/// <summary>
/// Where a contract is in its lifecycle. Stored as a string so the value in the
/// database reads the same as the value in the code and in the API.
/// </summary>
public static class ContractStatuses
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string SignedByClient = "signed_by_client";
    public const string Countersigned = "countersigned";
    public const string Declined = "declined";
    public const string Voided = "voided";
}

/// <summary>
/// A contract sent to a counterparty for signature, and the record of both
/// signatures.
/// </summary>
/// <remarks>
/// <para>
/// The BODY IS STORED, not templated at render time. Once a contract is sent,
/// the exact text the counterparty was shown has to be reproducible years later,
/// and a template rendered on demand would silently change with the template. So
/// the draft is generated from the template ONCE and the resulting text is what
/// gets frozen, signed and archived.
/// </para>
/// <para>
/// The signature itself is a typed name plus the surrounding evidence — when,
/// from what address, with what browser, against which exact body hash. That
/// combination is what a simple electronic signature IS; it is not a qualified
/// signature under ZertES or eIDAS, and the API documentation says so where the
/// distinction could mislead.
/// </para>
/// </remarks>
public class Contract
{
    public Guid Id { get; set; }

    /// <summary>Owning organisation. Every query is scoped by this.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The lead this contract belongs to, if it came from one.</summary>
    public Guid? LeadId { get; set; }

    /// <summary>The deal, once the lead has been converted.</summary>
    public Guid? DealId { get; set; }

    public string Status { get; set; } = ContractStatuses.Draft;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The contract text, as Markdown-ish plain text with merge fields already
    /// resolved. Frozen at send time — see the note on this class.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of <see cref="Body"/> at the moment of sending, in hex.
    /// </summary>
    /// <remarks>
    /// The point of tamper-evidence: if the stored body were ever altered after
    /// signature, this would no longer match it. Recomputing the hash at read
    /// time instead would defeat the entire purpose.
    /// </remarks>
    public string? BodyHashAtSend { get; set; }

    public string CounterpartyName { get; set; } = string.Empty;
    public string CounterpartyEmail { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }
    public Guid? SentByUserId { get; set; }

    /// <summary>
    /// SHA-256 of the signing token, hex. The raw token is emailed and never
    /// stored, so a database leak does not hand out the ability to sign.
    /// Same treatment the password-reset token already gets.
    /// </summary>
    public string? SigningTokenHash { get; set; }
    public DateTime? SigningTokenExpiresAtUtc { get; set; }

    /// <summary>First time the counterparty opened the link, for the audit trail.</summary>
    public DateTime? FirstViewedAtUtc { get; set; }

    // ── The counterparty's signature ─────────────────────────────────────────
    public string? ClientSignatureName { get; set; }
    public DateTime? ClientSignedAtUtc { get; set; }
    public string? ClientSignatureIp { get; set; }
    public string? ClientSignatureUserAgent { get; set; }

    // ── Ours ─────────────────────────────────────────────────────────────────
    public string? CounterSignatureName { get; set; }
    public DateTime? CounterSignedAtUtc { get; set; }
    public Guid? CounterSignedByUserId { get; set; }
    public string? CounterSignatureIp { get; set; }

    /// <summary>Why it was declined or voided, when it was.</summary>
    public string? ClosedReason { get; set; }

    /// <summary>
    /// When the executed copy was successfully emailed to both parties, or null.
    /// </summary>
    /// <remarks>
    /// Null after a countersignature means the contract is executed but nobody
    /// has been sent it — a state worth being able to see and retry, rather than
    /// assuming delivery because the signature succeeded.
    /// </remarks>
    public DateTime? ExecutedCopySentAtUtc { get; set; }

    public ICollection<ContractEvent> Events { get; set; } = new List<ContractEvent>();
}

/// <summary>
/// One entry in a contract's audit trail.
/// </summary>
/// <remarks>
/// This is what makes a typed-name signature defensible: not the mark itself but
/// the record of who did what, when, and from where. Append-only by intent —
/// nothing in the application ever updates or deletes a row.
/// </remarks>
public class ContractEvent
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract? Contract { get; set; }

    /// <summary>created, edited, sent, viewed, signed, countersigned, emailed, declined, voided.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Human-readable detail, shown in the timeline.</summary>
    public string? Detail { get; set; }

    /// <summary>Null for anything the counterparty did — they have no account.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Free-text actor label, so the counterparty is attributable too.</summary>
    public string? ActorLabel { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AtUtc { get; set; }
}
