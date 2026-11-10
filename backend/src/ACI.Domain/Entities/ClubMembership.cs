namespace ACI.Domain.Entities;

// How a person becomes a member, and who belongs together: membership applications (and the
// public form people apply on), and households. Both used to be JSON documents whose shape the
// browser owned (ClubDocuments of kind applications, households and household-members), and
// approving an application was three requests from the browser, so a retry or two reviewers
// at once made two members. The documents stay, unread, until a later release removes them.

/// <summary>Where an application is in the committee's review.</summary>
public static class ApplicationStatuses
{
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Waitlisted = "Waitlisted";

    public static readonly IReadOnlyList<string> All = [Submitted, UnderReview, Approved, Rejected, Waitlisted];

    /// <summary>The ones still waiting for a decision, which can be approved.</summary>
    public static bool IsOpen(string status) => status is Submitted or UnderReview or Waitlisted;
}

/// <summary>Where an application came from.</summary>
public static class ApplicationSources
{
    /// <summary>Typed in by staff on the Applications page.</summary>
    public const string Staff = "staff";

    /// <summary>Filled in by the applicant on the club's public application form.</summary>
    public const string PublicForm = "form";

    /// <summary>Started from a lead or a contact in the sales pipeline.</summary>
    public const string Lead = "lead";

    /// <summary>Copied from the old application documents when they moved to this table.</summary>
    public const string Moved = "moved";
}

/// <summary>What approved an application: a person, or something the applicant did that admits them.</summary>
public static class ApprovalTriggers
{
    /// <summary>A reviewer pressed Approve.</summary>
    public const string Staff = "staff";

    /// <summary>The membership contract for the application's lead or deal was countersigned.</summary>
    public const string Contract = "contract";

    /// <summary>The applicant's deposit was recorded as paid.</summary>
    public const string Deposit = "deposit";

    /// <summary>Approved before this table existed, by the old flow in the browser.</summary>
    public const string Moved = "moved";
}

/// <summary>
/// Someone asking to join the club, and what happened to their application.
/// </summary>
/// <remarks>
/// <para>
/// Approval is one transaction on the server (IClubApplications.ApproveAsync). It claims the
/// row first: the status, the member it creates (<see cref="MemberId"/>, chosen before the
/// member is written) and a short lease (<see cref="ApprovalClaimedUntilUtc"/>) are saved
/// against the <see cref="Version"/> the request read, so of two approvals at once only one
/// goes on to create the member, the dues and the fee; the other finds the member the first
/// one made and answers with it.
/// </para>
/// <para>
/// What the approval did is kept on the row (the subscription, the initiation fee, the
/// welcome message), so an approval that a reviewer without the money permission made can
/// be finished by someone with it, once, and nothing is ever done twice.
/// </para>
/// </remarks>
public class ClubApplication : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>The tier asked for: one of the club's tiers in the price catalogue.</summary>
    public string RequestedTier { get; set; } = string.Empty;

    public string? ReferredByName { get; set; }
    public string? Occupation { get; set; }
    public string? ReasonForJoining { get; set; }

    /// <summary><see cref="ApplicationStatuses"/>.</summary>
    public string Status { get; set; } = ApplicationStatuses.Submitted;

    /// <summary><see cref="ApplicationSources"/>.</summary>
    public string Source { get; set; } = ApplicationSources.Staff;

    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewerNotes { get; set; }

    /// <summary>The lead the applicant is in the sales pipeline as, when there is one.</summary>
    public Guid? LeadId { get; set; }

    /// <summary>The applicant's CRM contact, when there is one.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>A household the applicant joins on approval (a member's spouse applying, say), and as what.</summary>
    public Guid? HouseholdId { get; set; }
    public string? HouseholdRole { get; set; }

    /// <summary>The member the approval made (or found, for someone who was a member already).</summary>
    public Guid? MemberId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    /// <summary><see cref="ApprovalTriggers"/>: what approved it.</summary>
    public string? ApprovalTrigger { get; set; }

    /// <summary>The countersigned contract that approved it, when one did.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>The dues subscription the approval started, and the initiation fee it posted.</summary>
    public Guid? DuesSubscriptionId { get; set; }
    public Guid? InitiationChargeId { get; set; }

    /// <summary>
    /// Approved by someone who may not charge accounts, so the dues and the initiation fee are
    /// still to be set up by someone who may. Cleared once they are.
    /// </summary>
    public bool MoneyPending { get; set; }

    /// <summary>
    /// The approval found the applicant had been a member before (neither new nor left Pending
    /// by the old approval), so no initiation fee is due. Kept on the row because the money of
    /// an approval can be finished later (<see cref="MoneyPending"/>), by someone who must not
    /// charge the fee the approval decided against.
    /// </summary>
    public bool ReturningMember { get; set; }

    /// <summary>The deposit the applicant paid, recorded on their new account when they were admitted for it.</summary>
    public Guid? DepositPaymentId { get; set; }

    /// <summary>The welcome email the approval queued on the outbox.</summary>
    public Guid? WelcomeMessageId { get; set; }

    /// <summary>
    /// While an approval is under way: until when the request that claimed it has it. Another
    /// request that finds it claimed waits for that one rather than doing the work again.
    /// </summary>
    public DateTime? ApprovalClaimedUntilUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>A new value on every save; a change made from an older one is refused (409).</summary>
    public Guid Version { get; set; }
}

/// <summary>
/// A workspace's public application form: the link people apply on, without signing in, as
/// the public lead capture form takes inquiries.
/// </summary>
public class ClubApplicationForm : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The secret part of the form's link. A new one replaces it when the club wants the old link to stop working.</summary>
    public string PublicToken { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>A line of welcome above the form.</summary>
    public string? Intro { get; set; }

    /// <summary>What the applicant reads once they have applied.</summary>
    public string? SuccessMessage { get; set; }

    /// <summary>The words next to the marketing tick box, kept on each consent recorded through the form.</summary>
    public string? ConsentText { get; set; }

    /// <summary>Who set the form up: the lead an application makes is theirs, as a capture form's leads are its creator's.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>A person's place in a household.</summary>
public static class HouseholdRoles
{
    public const string Primary = "Primary";
    public const string Spouse = "Spouse";
    public const string Partner = "Partner";
    public const string Child = "Child";
    public const string Dependent = "Dependent";
    public const string Parent = "Parent";
    public const string Sibling = "Sibling";
    public const string Other = "Other";

    /// <summary>The roles someone other than the primary can have.</summary>
    public static readonly IReadOnlyList<string> NonPrimary = [Spouse, Partner, Child, Dependent, Parent, Sibling, Other];

    /// <summary>The primary's spouse or partner: a household has one at most.</summary>
    public static bool IsCouple(string role) => role is Spouse or Partner;

    /// <summary>The known role for <paramref name="value"/>, ignoring case, or null.</summary>
    public static string? Normalize(string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return NonPrimary.Concat([Primary]).FirstOrDefault(r => string.Equals(r, v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The role the old primary takes when someone else becomes the primary: the new primary's
    /// relation to them, turned round (a primary whose child takes over becomes their parent).
    /// </summary>
    public static string InverseOf(string role) => role switch
    {
        Spouse => Spouse,
        Partner => Partner,
        Child or Dependent => Parent,
        Parent => Child,
        Sibling => Sibling,
        _ => Other,
    };
}

/// <summary>
/// A household: people who belong together, one of them the primary, whose bills the others'
/// can roll up to.
/// </summary>
/// <remarks>
/// <para>
/// The one model of who forms a family or household, for the club and the CRM. A household's
/// people are club members, CRM contacts, or both (a member linked to their contact); a
/// spouse or partner recorded on a contact's page is the couple of a household, and a
/// household shows on the contact pages of the people in it. Contact relationships keep the
/// ties a household cannot hold: kin who live elsewhere, friends, colleagues.
/// </para>
/// <para>
/// One primary per household and one household per person are kept by the services and,
/// on SQL Server, by unique indexes, so two edits at once cannot break either.
/// </para>
/// </remarks>
public class ClubHousehold : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>Where a household link came from.</summary>
public static class HouseholdLinkSources
{
    /// <summary>Added on the Households page, or by an approval.</summary>
    public const string Club = "club";

    /// <summary>A spouse or partner recorded on a contact or lead page.</summary>
    public const string Crm = "crm";

    /// <summary>Copied from the old household-members documents.</summary>
    public const string MovedLink = "moved";

    /// <summary>Made from a spouse or partner relationship between two contacts, when couples moved into households.</summary>
    public const string MovedCouple = "couple";
}

/// <summary>One person in a household: a member, a contact, or a member and their contact.</summary>
public class ClubHouseholdMember : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid HouseholdId { get; set; }

    /// <summary>The club member, when the person is one.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>
    /// The CRM contact, for someone who is not a member (yet). For a member it is left empty
    /// and their own contact link (ClubMember.ContactId) is read instead, so the two never disagree.
    /// </summary>
    public Guid? ContactId { get; set; }

    /// <summary><see cref="HouseholdRoles"/>: Primary for the primary, else their relation to the primary.</summary>
    public string Role { get; set; } = HouseholdRoles.Other;

    public bool IsPrimary { get; set; }

    /// <summary>
    /// Whether what this member is charged (charges, closed tabs, billed dues) goes on the
    /// primary's account, saying who incurred it. Only a member's link, never the primary's.
    /// </summary>
    public bool RollUpBilling { get; set; }

    /// <summary><see cref="HouseholdLinkSources"/>.</summary>
    public string Source { get; set; } = HouseholdLinkSources.Club;

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
