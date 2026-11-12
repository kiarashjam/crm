using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Membership applications, from the first inquiry to the member: the step that makes the
/// sales pipeline and the club one product.
/// </summary>
/// <remarks>
/// <para>
/// <b>Approval</b> (<see cref="ApproveAsync"/>) does everything in one transaction on the
/// server: the application is approved; the applicant becomes a member, or is linked to the
/// member they already are (same email and name); the member carries the applicant's lead,
/// contact and consent; their tier's dues subscription starts and its initiation fee is posted
/// through the ledger (IClubLedger); they join the household the application names, or the one
/// they were already in as a contact; and a welcome email is queued on the outbox. A retry, a
/// double click or two reviewers at once make one member and one fee: the first claims the
/// application, the others answer with the member it made.
/// </para>
/// <para>
/// <b>Money</b>: the dues and the fee are money, so they are set up when whoever approves may
/// charge accounts (money.manage). Approved by someone who may not, the application is marked
/// <c>moneyPending</c> and approving it again, by someone who may, sets them up, once.
/// </para>
/// <para>
/// <b>What else admits someone</b>. A countersigned contract for the application's lead or deal
/// approves it (ContractService calls <see cref="AdmitForContractAsync"/>); a paid deposit will,
/// through <see cref="AdmitForDepositAsync"/>, once the contract flow collects deposits (Phase 3).
/// Both go through the same approval, once.
/// </para>
/// </remarks>
public interface IClubApplications
{
    Task<IReadOnlyList<ApplicationDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Result<ApplicationDto>> GetAsync(Guid organizationId, Guid applicationId, CancellationToken ct = default);

    /// <summary>The applications of a lead, a contact or a member, newest first: their pages show them.</summary>
    Task<IReadOnlyList<ApplicationDto>> ForPersonAsync(Guid organizationId, Guid? leadId, Guid? contactId, Guid? memberId, CancellationToken ct = default);

    /// <summary>
    /// The member a lead or a contact is (linked by the lead or the contact, or by the lead's
    /// contact), and their applications: what the lead and contact pages show of the club.
    /// </summary>
    Task<PersonMembershipDto> MembershipAsync(Guid organizationId, Guid? leadId, Guid? contactId, CancellationToken ct = default);

    /// <summary>A member's lead and contact, and the applications that admitted them: what the member page links to.</summary>
    Task<Result<MemberLinksDto>> MemberLinksAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    Task<Result<ApplicationDto>> CreateAsync(Guid organizationId, Guid userId, ApplicationWriteRequest request, CancellationToken ct = default);

    /// <summary>Changes an application's details; one already approved is refused.</summary>
    Task<Result<ApplicationDto>> UpdateAsync(Guid organizationId, Guid applicationId, Guid userId, ApplicationWriteRequest request, CancellationToken ct = default);

    /// <summary>Moves an application through review: under review, waitlisted, rejected, or reopened (submitted again).</summary>
    Task<Result<ApplicationDto>> SetStatusAsync(Guid organizationId, Guid applicationId, Guid userId, ApplicationStatusRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid organizationId, Guid applicationId, CancellationToken ct = default);

    /// <summary>
    /// Starts an application for a lead or a contact, filled in from the record: the sales side's
    /// "apply for membership". The open application they already have is answered instead.
    /// </summary>
    Task<Result<ApplicationDto>> StartForPersonAsync(Guid organizationId, Guid userId, Guid? leadId, Guid? contactId, CancellationToken ct = default);

    /// <summary>Approves the application and admits the applicant (see the remarks on this interface).</summary>
    Task<Result<ApprovalResultDto>> ApproveAsync(Guid organizationId, Guid applicationId, Guid userId, ApproveApplicationRequest request, CancellationToken ct = default);

    /// <summary>
    /// The hook a countersigned contract calls: approves the open application of the contract's
    /// lead or deal (or the one it names), once, as the person who countersigned, held to the
    /// approve button's rules. Someone without club.members (a sales rep can countersign)
    /// admits nobody, and the application stays open for a reviewer; someone without
    /// money.manage admits the member with the dues and fee left pending. A contract with no
    /// application behind it admits nobody. Answers what it approved.
    /// </summary>
    Task<IReadOnlyList<ApprovalResultDto>> AdmitForContractAsync(Guid organizationId, Guid contractId, Guid? leadId, Guid? dealId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The hook a paid deposit calls: approves the application, once, as the person who recorded
    /// the deposit, dues and fee included. Nothing calls it yet: deposits for applications do not
    /// exist until the contract flow collects one at signing (Phase 3).
    /// </summary>
    Task<Result<ApprovalResultDto>> AdmitForDepositAsync(Guid organizationId, Guid applicationId, Guid userId, Guid? depositPaymentId, CancellationToken ct = default);

    // ── the public application form ──────────────────────────────────────────

    /// <summary>The workspace's form: its link, whether it is open, and its wording. Null before one is set up.</summary>
    Task<ApplicationFormDto?> FormAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Sets up or changes the form: open or closed, and its wording.</summary>
    Task<Result<ApplicationFormDto>> SaveFormAsync(Guid organizationId, Guid userId, ApplicationFormRequest request, CancellationToken ct = default);

    /// <summary>Gives the form a new link; the old one stops working.</summary>
    Task<Result<ApplicationFormDto>> RotateFormAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>What a visitor to the form's link sees, or null when the link is not open.</summary>
    /// <remarks><paramref name="language"/>: the language the form is read in, for the standard words it shows.</remarks>
    Task<PublicApplicationFormDto?> PublicFormAsync(string token, string? language = null, CancellationToken ct = default);

    /// <summary>
    /// A visitor's application from the public form: a Submitted application, linked to the lead
    /// or contact with their email (or a new lead, as the capture form makes them), with their
    /// consent recorded. Answers the same short receipt whatever happened, so it reveals nothing.
    /// </summary>
    Task<Result<PublicApplicationReceipt>> SubmitAsync(string token, PublicApplicationRequest request, CancellationToken ct = default);
}

/// <summary>An application to add, or the fields of one to change: a field left out is kept.</summary>
public sealed class ApplicationWriteRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? RequestedTier { get; set; }
    public string? ReferredByName { get; set; }
    public string? Occupation { get; set; }
    public string? ReasonForJoining { get; set; }
    public string? ReviewerNotes { get; set; }

    /// <summary>The applicant's lead and contact, in this workspace.</summary>
    public Guid? LeadId { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>A household the applicant joins when approved, and as what to its primary.</summary>
    public Guid? HouseholdId { get; set; }
    public string? HouseholdRole { get; set; }

    /// <summary>Accepted for screens that set the status through this route: moved as <see cref="IClubApplications.SetStatusAsync"/> moves it.</summary>
    public string? Status { get; set; }

    /// <summary>The version the change was made from (or If-Match); an older one is refused.</summary>
    public Guid? Version { get; set; }
}

public sealed class ApplicationStatusRequest
{
    /// <summary>Submitted (to reopen), UnderReview, Waitlisted or Rejected. Approval has its own route.</summary>
    public string? Status { get; set; }
    public string? ReviewerNotes { get; set; }
    public Guid? Version { get; set; }
}

/// <summary>How to approve: everything is optional, and left out takes the application's own.</summary>
public sealed class ApproveApplicationRequest
{
    public string? ReviewerNotes { get; set; }

    /// <summary>The tier to admit them at, when not the one they asked for.</summary>
    public string? Tier { get; set; }

    /// <summary>Start the tier's dues subscription (yes unless false). Money: needs money.manage.</summary>
    public bool? StartDues { get; set; }

    /// <summary>Post the tier's initiation fee (yes unless false). Money: needs money.manage.</summary>
    public bool? ChargeInitiationFee { get; set; }

    /// <summary>A household to join (else the application's), as what, and whether their bills roll up to its primary.</summary>
    public Guid? HouseholdId { get; set; }
    public string? HouseholdRole { get; set; }
    public bool? RollUpBilling { get; set; }

    /// <summary>Queue the welcome email (yes unless false).</summary>
    public bool? SendWelcome { get; set; }

    public Guid? Version { get; set; }
}

public sealed class ApplicationFormRequest
{
    public bool? IsActive { get; set; }
    public string? Intro { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ConsentText { get; set; }
}

/// <summary>What a stranger submits on the public application form.</summary>
/// <param name="Website">A honeypot: hidden from people, filled by bots. A filled one is thanked and dropped.</param>
/// <param name="MarketingConsent">Whether they agreed to news and offers by email: recorded either way.</param>
/// <param name="Language">The language the form was filled in (en, de, fr): the member's language once approved.</param>
public sealed record PublicApplicationRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(64)] string? Phone,
    [StringLength(32)] string? RequestedTier,
    [StringLength(200)] string? Occupation,
    [StringLength(200)] string? ReferredByName,
    [StringLength(2000)] string? ReasonForJoining,
    bool MarketingConsent = false,
    [StringLength(200)] string? Website = null,
    [StringLength(10)] string? Language = null);

/// <summary>The whole of what the public form says back: no record, no ids.</summary>
public sealed record PublicApplicationReceipt(string Message);

/// <summary>A tier the public form offers: its name and dues.</summary>
public sealed record PublicTierDto(string Name, decimal Price, string Cadence, decimal? InitiationFee, string Currency);

/// <param name="MarketingConsent">off, optional or required: whether the form shows the marketing checkbox, and must have it ticked (the workspace's privacy settings).</param>
/// <param name="Language">The club's language (from its locale), for a visitor whose browser speaks none of the app's: en, de, fr or null.</param>
public sealed record PublicApplicationFormDto(string ClubName, string? Intro, string ConsentText, IReadOnlyList<PublicTierDto> Tiers, string MarketingConsent = "optional",
    string? Language = null);

public sealed record ApplicationFormDto(Guid Id, string PublicToken, string PublicPath, bool IsActive, string? Intro, string? SuccessMessage, string ConsentText, DateTime CreatedAtUtc);

public sealed record ApplicationDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Phone { get; init; }
    public string RequestedTier { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ReferredByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Occupation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ReasonForJoining { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime SubmittedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ReviewedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ReviewerNotes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LeadId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LeadName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContactName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? HouseholdId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? HouseholdName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? HouseholdRole { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ApprovedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ApprovalTrigger { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContractId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? DuesSubscriptionId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? InitiationChargeId { get; init; }

    /// <summary>Approved without the dues and fee, by someone who may not charge accounts: someone who may finishes it.</summary>
    public bool MoneyPending { get; init; }

    /// <summary>The marketing-by-email answer the applicant gave on the public form: granted, withdrawn, or unknown.</summary>
    public string MarketingConsent { get; init; } = ConsentStates.Unknown;

    public Guid Version { get; init; }
}

/// <summary>A member, as a lead's or a contact's page names them.</summary>
public sealed record MemberSummaryDto(Guid Id, string Name, string Email, string Tier, string Status, DateTime JoinedAtUtc);

/// <summary>What a lead's or a contact's page shows of the club: who they are as a member, their applications, their household.</summary>
public sealed record PersonMembershipDto
{
    /// <summary>The person's contact (for a lead, the contact it became), when there is one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContactId { get; init; }
    public IReadOnlyList<MemberSummaryDto> Members { get; init; } = Array.Empty<MemberSummaryDto>();
    public IReadOnlyList<ApplicationDto> Applications { get; init; } = Array.Empty<ApplicationDto>();
    public HouseholdDto? Household { get; init; }
}

/// <summary>What a member's page links to on the sales side.</summary>
public sealed record MemberLinksDto
{
    public Guid MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LeadId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LeadName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LeadStatus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ContactId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContactName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContactEmail { get; init; }
    public IReadOnlyList<ApplicationDto> Applications { get; init; } = Array.Empty<ApplicationDto>();
    public HouseholdDto? Household { get; init; }
}

/// <summary>What an approval did.</summary>
public sealed record ApprovalResultDto
{
    public ApplicationDto Application { get; init; } = new();
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;

    /// <summary>A new member was made; false when the applicant already was one (or this approval had already run).</summary>
    public bool MemberCreated { get; init; }

    /// <summary>This request did the approval; false when it found it done by an earlier one (a retry, a double click).</summary>
    public bool Approved { get; init; }

    public bool DuesStarted { get; init; }
    public bool FeeCharged { get; init; }
    public bool JoinedHousehold { get; init; }
    public bool WelcomeQueued { get; init; }
    public bool MoneyPending { get; init; }

    /// <summary>What was not done and why, in sentences for the reviewer.</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
