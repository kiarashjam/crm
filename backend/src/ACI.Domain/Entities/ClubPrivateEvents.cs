namespace ACI.Domain.Entities;

// Private events: a company's dinner, a wedding reception, a launch: the club's spaces hired for
// a time by a client (a company or a contact), sold through a deal. The spaces are held as
// bookings of the club's resources (ClubReservation with PrivateEventId), so a member cannot book
// the private dining room under a private party and the party cannot be put where a member has
// booked. The menu and services come from the price catalogue, the deposit and the final invoice
// go through the ledger onto the client's house account, the contract is drafted from the
// contract templates on the event's deal, and the guest list is checked in at the door.

/// <summary>Where a private event stands.</summary>
public static class PrivateEventStatuses
{
    /// <summary>Asked for and pencilled in: its spaces are held, nothing is agreed yet.</summary>
    public const string Enquiry = "Enquiry";

    /// <summary>Agreed: the contract countersigned, or confirmed by staff.</summary>
    public const string Confirmed = "Confirmed";

    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Enquiry, Confirmed, Completed, Cancelled];
}

/// <summary>A private hire of the club's spaces for a client.</summary>
/// <remarks>
/// Who pays is worked out from its links when money is first posted: the company's house account
/// when a company is linked, else the linked contact's member account when they are a member, else
/// the contact's own account. Once money is on an account the links that decide it cannot change.
/// </remarks>
public class ClubPrivateEvent : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>The client: a CRM company, a CRM contact, or both (the company's contact for the event).</summary>
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>The deal the hire was sold through; its contract is drafted on it.</summary>
    public Guid? DealId { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    /// <summary>How many guests are expected.</summary>
    public int GuestCount { get; set; }

    /// <summary>See <see cref="PrivateEventStatuses"/>.</summary>
    public string Status { get; set; } = PrivateEventStatuses.Enquiry;

    public string? Notes { get; set; }

    /// <summary>The workspace's currency when the event was made: what its prices are in.</summary>
    public string Currency { get; set; } = "CHF";

    /// <summary>The house account its money went on (a member's, a company's or a contact's key), once some has.</summary>
    public Guid? BillToAccountId { get; set; }

    /// <summary>The deposit asked, and its charge on the client's account.</summary>
    public decimal? DepositAmount { get; set; }
    public Guid? DepositChargeId { get; set; }
    public DateTime? DepositRequestedAtUtc { get; set; }

    /// <summary>When the deposit was set against the final invoice, or given back on cancelling.</summary>
    public DateTime? DepositSettledAtUtc { get; set; }

    /// <summary>When the final invoice was posted: the lines' charges, one per VAT rate.</summary>
    public DateTime? InvoicedAtUtc { get; set; }

    /// <summary>The contract drafted for it from the templates, on its deal.</summary>
    public Guid? ContractId { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledByName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>The version a screen edits the event from (If-Match); a guest checked in does not move it.</summary>
    public Guid EditVersion { get; set; }

    /// <summary>New on every save of the row (AppDbContext stamps it): two changes to its money at once are one after the other.</summary>
    public Guid Version { get; set; }
}

/// <summary>A line of a private event's menu or services: from the catalogue (its price and VAT copied) or typed in.</summary>
public class ClubPrivateEventLine : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid PrivateEventId { get; set; }

    /// <summary>The catalogue item it was taken from, when it was.</summary>
    public Guid? CatalogItemId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>The VAT rate in percent included in the price, when known.</summary>
    public decimal? VatRate { get; set; }

    public int SortOrder { get; set; }

    /// <summary>The final invoice's charge this line went into, once invoiced.</summary>
    public Guid? ChargeId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A guest on a private event's list, checked in at the door.</summary>
public class ClubPrivateEventGuest : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid PrivateEventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>The club member, when the guest is one.</summary>
    public Guid? MemberId { get; set; }

    public string? Notes { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public string? CheckedInByName { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public string? AddedByName { get; set; }
}
