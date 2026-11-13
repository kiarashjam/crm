namespace ACI.Domain.Entities;

// Corporate memberships: a membership a company holds (the CRM company record), with a number
// of seats its nominees fill. The company is billed on its own house account (ClubHouseAccount
// with CompanyId): its dues by a subscription on that account, and, when the membership says
// so, what its nominees run up in the club, each line saying who. Renewal is the company's:
// the membership renews as a whole, on its own date.
//
// Clubs ran these by hand: a company's "seats" were members with a note, the company got no
// bill of its own, and a nominee leaving meant someone remembering to stop sending the company
// their bar bills.

/// <summary>Where a corporate membership stands.</summary>
public static class CorporateMembershipStatuses
{
    public const string Active = "Active";

    /// <summary>Ended by the club or the company: its dues stop and its nominees hold no seat.</summary>
    public const string Ended = "Ended";

    public static readonly string[] All = [Active, Ended];
}

/// <summary>Who pays what a corporate membership's nominees run up in the club.</summary>
public static class NomineeChargeRules
{
    /// <summary>The company: their charges, tabs and purchases go on the company's account, each saying who.</summary>
    public const string Company = "company";

    /// <summary>Each nominee, on their own account; the company pays its dues only.</summary>
    public const string Nominee = "nominee";

    public static readonly string[] All = [Company, Nominee];
}

/// <summary>A membership held by a company, with seats for its nominees.</summary>
/// <remarks>
/// <para>
/// Every change to its nominees takes this row first (<see cref="NomineesChangedAtUtc"/> moves,
/// and with it <see cref="Version"/>), so two additions for the last seat are decided one after
/// the other: one gets the seat, the other is refused. A screen edits the membership's own
/// details from <see cref="EditVersion"/>, which a nominee change leaves alone.
/// </para>
/// </remarks>
public class ClubCorporateMembership : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The CRM company that holds it, and whose house account is billed.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The company's name when the membership was made or last saved, so lists read after the company is renamed.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>What the club calls the membership: "Corporate Gold", "Acme AG partnership".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How many nominees it holds at once.</summary>
    public int Seats { get; set; }

    /// <summary>See <see cref="CorporateMembershipStatuses"/>.</summary>
    public string Status { get; set; } = CorporateMembershipStatuses.Active;

    /// <summary>See <see cref="NomineeChargeRules"/>.</summary>
    public string NomineeCharges { get; set; } = NomineeChargeRules.Company;

    public DateTime StartsAtUtc { get; set; }

    /// <summary>When the current term ends and the membership is up for renewal.</summary>
    public DateTime RenewsAtUtc { get; set; }

    /// <summary>How long a term runs, in months: a renewal moves the date on by this much.</summary>
    public int TermMonths { get; set; } = 12;

    /// <summary>The company said it will not renew (or the club decided not to offer it), and why.</summary>
    public bool NotRenewing { get; set; }
    public string? NotRenewingReason { get; set; }
    public DateTime? LastRenewedAtUtc { get; set; }

    /// <summary>The dues on the company's account: a subscription the daily billing bills (ClubSubscription).</summary>
    public Guid? DuesSubscriptionId { get; set; }

    /// <summary>The CRM contact who looks after the bill at the company, and where its statements go.</summary>
    public Guid? BillingContactId { get; set; }
    public string? BillingEmail { get; set; }

    /// <summary>The language the company's statements are written in: "en", "de" or "fr"; English when none.</summary>
    public string? Language { get; set; }

    public string? Notes { get; set; }

    public DateTime? EndedAtUtc { get; set; }
    public string? EndedReason { get; set; }
    public string? EndedByName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Moved by every change to the nominees, which takes this row first (see the remarks).</summary>
    public DateTime? NomineesChangedAtUtc { get; set; }

    /// <summary>The version a screen edits the membership's details from (If-Match); a nominee change does not move it.</summary>
    public Guid EditVersion { get; set; }

    /// <summary>New on every save of the row, a nominee change included (AppDbContext stamps it).</summary>
    public Guid Version { get; set; }
}

/// <summary>
/// A member holding one of a corporate membership's seats, from when to when. A nominee who
/// leaves or is swapped out keeps their row, saying when and who took the seat, so the
/// company's history and its old statements still say who its nominees were.
/// </summary>
public class ClubCorporateNominee : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid CorporateMembershipId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The member's name when they took the seat, so the history reads after a rename.</summary>
    public string MemberName { get; set; } = string.Empty;

    public DateTime AddedAtUtc { get; set; }
    public Guid? AddedByUserId { get; set; }
    public string? AddedByName { get; set; }

    /// <summary>When they gave up the seat; null while they hold it.</summary>
    public DateTime? RemovedAtUtc { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public string? RemovedByName { get; set; }
    public string? RemovedReason { get; set; }

    /// <summary>For a swap: the member who took the seat over.</summary>
    public Guid? ReplacedByMemberId { get; set; }
}
