using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Private events: the club's spaces hired by a client (a company or a contact), sold through a
/// deal, with the menu and services from the catalogue, a deposit and a final invoice on the
/// client's house account, a contract from the templates, and the guest list checked in at the door.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Spaces.</b> The resources it uses are held for its time as bookings of their own
/// (IClubReservations.HoldForPrivateEventAsync): a whole resource each, refused with a 409 naming
/// the booking in the way, and moved, released or kept with the event.</item>
/// <item><b>Money.</b> Through the ledger only, on the client's house account: the company's, the
/// linked contact's member account when they are a member, else the contact's own. The deposit is
/// one charge; the final invoice posts the lines, one charge per VAT rate, and sets the deposit
/// against them (its charge taken back, what was paid on it applied to the invoice), so the VAT is
/// on the invoice once. Posting money takes money.manage.</item>
/// <item><b>Contract.</b> Drafted on the event's deal from the private event template, filled with
/// its date, spaces, guests, lines and totals. Countersigning it confirms the event, wins the deal,
/// and admits nobody to the club.</item>
/// </list>
/// </remarks>
public interface IPrivateEvents
{
    Task<IReadOnlyList<PrivateEventDto>> ListAsync(Guid organizationId, PrivateEventQuery query, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> CreateAsync(Guid organizationId, Guid userId, PrivateEventWriteRequest request, CancellationToken ct = default);

    /// <summary>The event's details, time and spaces: a field left out is kept. Moving it moves its holds, or refuses (409).</summary>
    Task<Result<PrivateEventDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, PrivateEventWriteRequest request, Guid? expectedVersion, CancellationToken ct = default);

    Task<Result<PrivateEventDto>> AddLineAsync(Guid organizationId, Guid id, PrivateEventLineRequest request, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> UpdateLineAsync(Guid organizationId, Guid id, Guid lineId, PrivateEventLineRequest request, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> RemoveLineAsync(Guid organizationId, Guid id, Guid lineId, CancellationToken ct = default);

    Task<Result<PrivateEventDto>> ConfirmAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> CompleteAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cancels the event and releases its spaces. A deposit is kept or given back as house credit,
    /// as <paramref name="request"/> says; an invoiced event is not cancelled (its charges are voided first).
    /// </summary>
    /// <param name="mayHandleMoney">Whether the caller may move money (money.manage): giving a deposit back takes it.</param>
    Task<Result<PrivateEventDto>> CancelAsync(Guid organizationId, Guid id, PrivateEventCancelRequest request, bool mayHandleMoney, CancellationToken ct = default);

    /// <summary>Asks the deposit: a charge on the client's account (money.manage).</summary>
    Task<Result<PrivateEventDto>> RequestDepositAsync(Guid organizationId, Guid id, PrivateEventDepositRequest request, CancellationToken ct = default);

    /// <summary>Posts the final invoice, once (money.manage).</summary>
    Task<Result<PrivateEventDto>> InvoiceAsync(Guid organizationId, Guid id, PrivateEventInvoiceRequest request, CancellationToken ct = default);

    /// <summary>Drafts the event's contract from the private event template on its deal.</summary>
    Task<Result<PrivateEventDto>> DraftContractAsync(Guid organizationId, Guid userId, Guid id, CancellationToken ct = default);

    Task<Result<PrivateEventDto>> AddGuestAsync(Guid organizationId, Guid id, PrivateEventGuestRequest request, CancellationToken ct = default);
    Task<Result<PrivateEventDto>> RemoveGuestAsync(Guid organizationId, Guid id, Guid guestId, CancellationToken ct = default);

    /// <summary>Checks a guest in at the door, or (with <paramref name="undo"/>) takes a check-in made in error back.</summary>
    Task<Result<PrivateEventDto>> CheckInGuestAsync(Guid organizationId, Guid id, Guid guestId, bool undo, CancellationToken ct = default);
}

/// <summary>Called by the contract flow when a contract is countersigned (ContractService), so the private event behind it is confirmed.</summary>
public interface IPrivateEventContracts
{
    /// <summary>Whether the contract was drafted for a private event: such a contract admits nobody to the club.</summary>
    Task<bool> IsPrivateEventContractAsync(Guid organizationId, Guid contractId, CancellationToken ct = default);

    /// <summary>Confirms the private event the contract is for, if it is still an enquiry. Nothing for any other contract.</summary>
    Task ContractCountersignedAsync(Guid organizationId, Guid contractId, CancellationToken ct = default);
}

public sealed class PrivateEventQuery
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Status { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
}

/// <summary>A private event to make, or the fields of one to change: a field left out is kept.</summary>
public sealed class PrivateEventWriteRequest
{
    public string? Title { get; set; }

    /// <summary>The client. An empty id takes the link off (before any money is posted).</summary>
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? DealId { get; set; }

    public string? StartAtUtc { get; set; }
    public string? EndAtUtc { get; set; }
    public int? GuestCount { get; set; }

    /// <summary>The spaces (club resources) it uses; the full list, replacing what it held.</summary>
    public List<Guid>? ResourceIds { get; set; }

    public string? Notes { get; set; }

    /// <summary>On making it: its first menu and services.</summary>
    public List<PrivateEventLineRequest>? Lines { get; set; }

    public Guid? Version { get; set; }
}

/// <summary>A menu or service line: a catalogue item (its price and VAT used) or one typed in.</summary>
public sealed class PrivateEventLineRequest
{
    public Guid? CatalogItemId { get; set; }
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
}

public sealed class PrivateEventDepositRequest
{
    public decimal? Amount { get; set; }
    public string? DueAtUtc { get; set; }
}

public sealed class PrivateEventInvoiceRequest
{
    public string? DueAtUtc { get; set; }
}

public sealed class PrivateEventCancelRequest
{
    public string? Reason { get; set; }

    /// <summary>When a deposit was asked: "keep" it, or "release" it as house credit.</summary>
    public string? Deposit { get; set; }
}

public sealed class PrivateEventGuestRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public Guid? MemberId { get; set; }
    public string? Notes { get; set; }
}

public sealed record PrivateEventLineDto(Guid Id, string Name, decimal Quantity, decimal UnitPrice, decimal Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? VatRate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CatalogItemId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ChargeId);

public sealed record PrivateEventGuestDto(Guid Id, string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MemberId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Notes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? CheckedInAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CheckedInByName);

public sealed record PrivateEventSpaceDto(Guid ReservationId, Guid ResourceId, string ResourceName, string Status);

/// <summary>The event's money as the ledger has it: the deposit and the invoice, and what has been paid on them.</summary>
public sealed record PrivateEventMoneyDto
{
    /// <summary>member, company or contact: whose account it goes on; left out until the event has a client.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BillToKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? BillToAccountId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BillToName { get; init; }
    public decimal LinesTotal { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? DepositAmount { get; init; }

    /// <summary>none, charged, paid (in full), set_against_invoice, released (given back as credit), or kept (on cancelling).</summary>
    public string DepositState { get; init; } = "none";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? DepositChargeId { get; init; }
    public decimal DepositPaid { get; init; }
    public bool Invoiced { get; init; }
    public IReadOnlyList<Guid> InvoiceChargeIds { get; init; } = Array.Empty<Guid>();
    public decimal InvoiceTotal { get; init; }
    public decimal InvoicePaid { get; init; }
}

public sealed record PrivateEventDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CompanyId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CompanyName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContactName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? DealId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DealName { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public int GuestCount { get; init; }
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public string Currency { get; init; } = string.Empty;
    public IReadOnlyList<PrivateEventSpaceDto> Spaces { get; init; } = Array.Empty<PrivateEventSpaceDto>();
    public IReadOnlyList<PrivateEventLineDto> Lines { get; init; } = Array.Empty<PrivateEventLineDto>();
    public IReadOnlyList<PrivateEventGuestDto> Guests { get; init; } = Array.Empty<PrivateEventGuestDto>();
    public int GuestsCheckedIn { get; init; }
    public PrivateEventMoneyDto Money { get; init; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContractId { get; init; }

    /// <summary>The contract's status (draft, sent, signed_by_client, countersigned, declined, voided).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContractStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ConfirmedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CancelledAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CancelReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>The version to edit the event from (If-Match); a guest checked in does not move it.</summary>
    public Guid Version { get; init; }
}
