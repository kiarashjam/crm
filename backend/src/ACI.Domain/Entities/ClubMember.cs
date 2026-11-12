namespace ACI.Domain.Entities;

/// <summary>A club member. Linked to their CRM contact and the lead they came in as, when they have them.</summary>
public class ClubMember : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Tier { get; set; } = "Bronze";
    public string Status { get; set; } = "Pending";
    public DateTime JoinedAtUtc { get; set; }
    public DateTime RenewsAtUtc { get; set; }
    public decimal DuesAmount { get; set; }
    public string DuesFrequency { get; set; } = "Monthly";
    public decimal HouseAccountBalance { get; set; }

    /// <summary>
    /// The part of <see cref="HouseAccountBalance"/> no charge explains: the balance the
    /// member was brought over with, plus any correction typed in by hand.
    /// </summary>
    /// <remarks>
    /// The balance is worked out again from the member's charges whenever one is posted,
    /// paid or voided. That used to set it to what the charges alone left owing, so the
    /// first charge after an opening balance of -500 wiped the 500, and a correction typed
    /// into the member's record lasted until the next bar tab. Adding this back keeps both.
    /// </remarks>
    public decimal ManualBalance { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// When the club's billing set the member Lapsed for a charge left unpaid past the grace
    /// period. Only a lapse the billing made is undone by it, once nothing is overdue; one
    /// set by hand stays. Cleared when someone changes the status themselves.
    /// </summary>
    public DateTime? BillingLapsedAtUtc { get; set; }

    /// <summary>The status the billing's lapse replaced, put back when the member is reinstated.</summary>
    public string? StatusBeforeLapse { get; set; }

    /// <summary>
    /// The member's CRM contact, when they have one: the same person in the sales records.
    /// Members and contacts were two separate lists that never met, so the contact page could
    /// not say someone was a member and the member page could not reach their history.
    /// </summary>
    public Guid? ContactId { get; set; }

    /// <summary>The lead the member came in as, when they joined through the sales pipeline.</summary>
    public Guid? LeadId { get; set; }

    /// <summary>
    /// When the membership starts, if it is known. Null with <see cref="StartUndetermined"/>
    /// set means the start is still to be decided; null without it means nobody said.
    /// </summary>
    public DateTime? StartsOnUtc { get; set; }
    public bool StartUndetermined { get; set; }

    /// <summary>The deal the member joined through, when they came from a converted lead (see DealOnboarding).</summary>
    public Guid? DealId { get; set; }
    /// <summary>
    /// When the member was erased: at their request, or by the workspace's retention settings.
    /// The row stays so the books still add up (their charges and payments name it), but its
    /// name is a pseudonym ("Erased member 12") and nothing personal is left on it.
    /// </summary>
    public DateTime? ErasedAtUtc { get; set; }

    /// <summary>
    /// The language the club writes to the member in, "en", "de" or "fr": their statements,
    /// reminders, welcome and event emails. Null means English, as every member was written to
    /// before languages existed. Set by staff (the member form offers the workspace's language
    /// for someone new), from the language they applied in, or from the welcome handoff's.
    /// </summary>
    public string? PreferredLanguage { get; set; }
}

public class ClubVisit : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberTier { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime VisitedAtUtc { get; set; }
    public int GuestCount { get; set; }
    public int PointsAwarded { get; set; }
    public string? Notes { get; set; }
}

public class LoyaltyLedgerEntry : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Kind { get; set; } = "Earned";
    public string Reason { get; set; } = "Visit";
    public int Points { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
