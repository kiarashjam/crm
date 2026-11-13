using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Corporate memberships: memberships a company holds, with seats its nominees fill, its dues
/// on the company's own house account, and its renewal on the company.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Seats.</b> Nominees are club members. Adding, swapping and removing them is held to
/// the seat count: every change takes the membership's row first, so two additions for the last
/// seat are decided one after the other and the second is refused (409). A member holds one
/// corporate seat at a time.</item>
/// <item><b>Dues.</b> A subscription on the company's account (IClubLedger), billed by the daily
/// billing run like a member's dues. Setting them takes money.manage.</item>
/// <item><b>Nominees' charges.</b> When the membership says the company pays them, what a nominee
/// runs up (charges, closed tabs, sales at the till and from the POS) is posted to the company's
/// account at posting, each line saying who ran it up; their own dues are not, the company pays a
/// seat, not each member's subscription. Otherwise each nominee pays their own.</item>
/// <item><b>Statement.</b> The company's statement lists its own charges and its nominees', with
/// what each ran up in the period (IClubBilling.ClientStatementPdfAsync).</item>
/// <item><b>Renewal.</b> The membership renews as a whole: a renewal moves its date on a term,
/// and its nominees renew with it, so they leave the members' renewals board.</item>
/// </list>
/// </remarks>
public interface ICorporateMemberships
{
    Task<IReadOnlyList<CorporateMembershipDto>> ListAsync(Guid organizationId, CorporateMembershipQuery query, CancellationToken ct = default);
    Task<Result<CorporateMembershipDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <param name="mayHandleMoney">Whether the caller may set dues (money.manage); dues asked without it are refused.</param>
    Task<Result<CorporateMembershipDto>> CreateAsync(Guid organizationId, CorporateMembershipWriteRequest request, bool mayHandleMoney, CancellationToken ct = default);

    /// <summary>The membership's own details: a field left out is kept, empty text clears it.</summary>
    Task<Result<CorporateMembershipDto>> UpdateAsync(Guid organizationId, Guid id, CorporateMembershipWriteRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Starts or changes the dues on the company's account (money.manage).</summary>
    Task<Result<CorporateMembershipDto>> SetDuesAsync(Guid organizationId, Guid id, CorporateDuesRequest request, CancellationToken ct = default);

    Task<Result<CorporateMembershipDto>> AddNomineeAsync(Guid organizationId, Guid id, NomineeRequest request, CancellationToken ct = default);

    /// <summary>Gives a nominee's seat to another member in one step: the seat count never moves.</summary>
    Task<Result<CorporateMembershipDto>> SwapNomineeAsync(Guid organizationId, Guid id, Guid nomineeId, NomineeRequest request, CancellationToken ct = default);

    Task<Result<CorporateMembershipDto>> RemoveNomineeAsync(Guid organizationId, Guid id, Guid nomineeId, string? reason, CancellationToken ct = default);

    /// <summary>Renews the membership for another term: its date moves on by the term, from the date it was due.</summary>
    Task<Result<CorporateMembershipDto>> RenewAsync(Guid organizationId, Guid id, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Marks the membership as not renewing at its date, with why; a renewal clears it.</summary>
    Task<Result<CorporateMembershipDto>> MarkNotRenewingAsync(Guid organizationId, Guid id, string? reason, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>
    /// Ends the membership: its nominees give up their seats and its dues stop. Stopping running
    /// dues takes money.manage, so an end that would leave them running is refused without it.
    /// </summary>
    Task<Result<CorporateMembershipDto>> EndAsync(Guid organizationId, Guid id, string? reason, bool mayHandleMoney, CancellationToken ct = default);

    /// <summary>The seat a member holds now, if any.</summary>
    Task<MemberCorporateSeatDto?> SeatOfMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);
}

public sealed class CorporateMembershipQuery
{
    public Guid? CompanyId { get; set; }

    /// <summary>Active or Ended; all when left out.</summary>
    public string? Status { get; set; }

    /// <summary>Only active memberships whose renewal date is at most this many days away (or past).</summary>
    public int? RenewingWithinDays { get; set; }
}

/// <summary>A corporate membership to make, or the fields of one to change: a field left out is kept.</summary>
public sealed class CorporateMembershipWriteRequest
{
    /// <summary>The company that holds it; only when it is made.</summary>
    public Guid? CompanyId { get; set; }

    public string? Name { get; set; }
    public int? Seats { get; set; }

    /// <summary>"company" (the company pays what its nominees run up) or "nominee" (each pays their own).</summary>
    public string? NomineeCharges { get; set; }

    public string? StartsAtUtc { get; set; }
    public string? RenewsAtUtc { get; set; }
    public int? TermMonths { get; set; }

    /// <summary>The contact at the company who looks after the bill; an empty id clears it.</summary>
    public Guid? BillingContactId { get; set; }

    /// <summary>Where its statements go; empty text clears it.</summary>
    public string? BillingEmail { get; set; }

    /// <summary>en, de or fr: the language of its statements. Empty text clears it (English).</summary>
    public string? Language { get; set; }

    public string? Notes { get; set; }

    /// <summary>On making it: dues to start on the company's account (money.manage).</summary>
    public CorporateDuesRequest? Dues { get; set; }

    public Guid? Version { get; set; }
}

/// <summary>The dues a corporate membership bills the company.</summary>
public sealed class CorporateDuesRequest
{
    /// <summary>A membership or dues item from the catalogue, whose price and cadence are used unless sent too.</summary>
    public Guid? CatalogItemId { get; set; }

    public decimal? Amount { get; set; }

    /// <summary>Monthly, Quarterly, SemiAnnual or Annual; Annual when neither this nor the catalogue says.</summary>
    public string? Cadence { get; set; }

    /// <summary>The first period's start; the membership's start when left out.</summary>
    public string? FirstBillingAtUtc { get; set; }
}

public sealed class NomineeRequest
{
    public Guid? MemberId { get; set; }
    public string? Reason { get; set; }
}

public sealed record CorporateNomineeDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberStatus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberEmail { get; init; }
    public DateTime AddedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? RemovedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RemovedReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ReplacedByMemberId { get; init; }

    /// <summary>
    /// Whether the nominee still has dues of their own running: the company pays the seat, so
    /// the club will usually want to pause them (it is not done for them).
    /// </summary>
    public bool OwnDuesRunning { get; init; }
}

public sealed record CorporateDuesDto(Guid SubscriptionId, string Name, decimal Amount, string Currency, string Cadence, string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? NextChargeAtUtc);

public sealed record CorporateMembershipDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Seats { get; init; }
    public int SeatsUsed { get; init; }
    public string Status { get; init; } = string.Empty;
    public string NomineeCharges { get; init; } = string.Empty;
    public DateTime StartsAtUtc { get; init; }
    public DateTime RenewsAtUtc { get; init; }
    public int TermMonths { get; init; }
    public bool NotRenewing { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NotRenewingReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastRenewedAtUtc { get; init; }

    /// <summary>current, due (within 60 days), overdue (its date has passed), not_renewing or ended.</summary>
    public string RenewalState { get; init; } = string.Empty;

    public int DaysToRenewal { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CorporateDuesDto? Dues { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? BillingContactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BillingContactName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BillingEmail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Language { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }

    /// <summary>Who holds a seat now.</summary>
    public IReadOnlyList<CorporateNomineeDto> Nominees { get; init; } = Array.Empty<CorporateNomineeDto>();

    /// <summary>Who held one before, latest first.</summary>
    public IReadOnlyList<CorporateNomineeDto> FormerNominees { get; init; } = Array.Empty<CorporateNomineeDto>();

    /// <summary>The company's house account: its balance, negative while the company owes the club.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ClientAccountDto? Account { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? EndedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EndedReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>The version to edit the membership's details from (If-Match); a nominee change does not move it.</summary>
    public Guid Version { get; init; }
}

/// <summary>The corporate seat a member holds, for their page.</summary>
public sealed record MemberCorporateSeatDto(Guid MembershipId, string Name, Guid CompanyId, string CompanyName, string NomineeCharges,
    DateTime RenewsAtUtc, Guid NomineeId);
