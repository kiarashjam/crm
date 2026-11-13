using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

// The member portal: members see their own club account here (statements, balance and open
// charges, a saved card and paying online, events, bookings, consent and points), in their
// language. Off until the club turns it on. It works beside the welcome hand-over to the
// club's own portal (OnboardingService), which it does not replace.
//
// A member signs in with a link emailed to them, never a password: the link is a 256-bit secret
// kept only as its hash, used once, and short-lived. Using it opens a session of its own (a
// row, so it can be ended) and hands back a member token that names the club, the member and
// the session. That token reaches only the portal's endpoints; a staff token never reaches them.

/// <summary>Signing in to the portal, and the member sessions it opens.</summary>
public interface IMemberPortalSignIn
{
    /// <summary>The club behind a portal address, for its sign-in page. NotFound for an address nobody has.</summary>
    Task<Result<PortalClubDto>> ClubAsync(string portalKey, CancellationToken ct = default);

    /// <summary>
    /// Emails a sign-in link to each member of the club at this address (one email: a couple
    /// sharing a mailbox gets a link for each), from the club's sender, in the member's language.
    /// Answers the same whether or not the address is a member's, so it tells a stranger nothing.
    /// </summary>
    Task<Result> RequestLinkAsync(string portalKey, string? email, CancellationToken ct = default);

    /// <summary>Uses a sign-in link once: a new session for the member it names, and the member token for it.</summary>
    Task<Result<PortalSessionStartedDto>> RedeemAsync(string portalKey, string? token, CancellationToken ct = default);

    /// <summary>
    /// Whether the session a member token names still stands: not ended, not run out, for a member
    /// who is still the club's and not erased. Notes when it was last used. Null when it does not.
    /// </summary>
    Task<PortalSessionCheck?> CheckSessionAsync(PortalMember who, CancellationToken ct = default);

    /// <summary>Whether the club has its member portal turned on (the club module is checked on its own).</summary>
    Task<bool> IsOpenAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Ends this session (signing out on this device).</summary>
    Task SignOutAsync(PortalMember who, CancellationToken ct = default);

    /// <summary>Ends every session of the member, this one included. Returns how many it ended.</summary>
    Task<int> SignOutEverywhereAsync(PortalMember who, CancellationToken ct = default);

    /// <summary>
    /// The club's own portal asks for a sign-in link for the member a welcome hand-over added, so
    /// its members open their club account here without signing in again. The request is signed
    /// with the hand-over's secret (X-Cadence-Timestamp and X-Cadence-Signature-256 over the body,
    /// as the hand-over itself is), fresh, and used once; a new secret refuses it.
    /// </summary>
    Task<Result<PortalHandoffLinkDto>> HandoffLinkAsync(Guid onboardingId, string? timestamp, string? signature, string body, CancellationToken ct = default);
}

/// <summary>Staff's side of the portal: turning it on, inviting members, who has used it, ending sessions.</summary>
public interface IMemberPortalAdmin
{
    Task<MemberPortalSettingsDto> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Saves the settings (settings.manage). Turning the portal off ends every member's session:
    /// a club that closes its portal does not leave anyone signed in to it.
    /// </summary>
    Task<Result<MemberPortalSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, SaveMemberPortalSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Every member, with when they were invited and when they last used the portal.</summary>
    Task<IReadOnlyList<MemberPortalUseDto>> UsageAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>One member's use of the portal (their member page shows it).</summary>
    Task<Result<MemberPortalUseDto>> UseAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Emails the member an invitation with a sign-in link, through the outbox.</summary>
    Task<Result<MemberPortalUseDto>> InviteAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default);

    /// <summary>Invites every member with an email address who has not been invited yet (not former or erased members).</summary>
    Task<Result<PortalInviteAllDto>> InviteAllAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>Ends every session of the member; their sign-in links not yet used stop working too.</summary>
    Task<Result<MemberPortalUseDto>> RevokeAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default);
}

/// <summary>
/// What a signed-in member sees and does: their own records only, and their household's where
/// they are its primary. An id that is not theirs is answered as one that does not exist.
/// </summary>
public interface IMemberPortal
{
    Task<Result<PortalMeDto>> MeAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<PortalMeDto>> UpdateMeAsync(PortalMember who, PortalProfileRequest request, CancellationToken ct = default);

    Task<Result<PortalAccountDto>> AccountAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<PortalStatementDto>> StatementAsync(PortalMember who, string month, CancellationToken ct = default);

    /// <summary>The month's statement as a PDF: the member's own account, or (for a household's primary) the household's.</summary>
    Task<Result<StatementFile>> StatementPdfAsync(PortalMember who, string month, bool household, CancellationToken ct = default);

    Task<Result<PortalCardDto>> CardAsync(PortalMember who, CancellationToken ct = default);

    /// <summary>Where to save a card: the club's pay page for the member, which starts Stripe's.</summary>
    Task<Result<PortalRedirectDto>> StartCardSetupAsync(PortalMember who, PortalCardSetupRequest request, CancellationToken ct = default);

    Task<Result<PortalCardDto>> RemoveCardAsync(PortalMember who, CancellationToken ct = default);

    /// <summary>Where to pay what is owed (or the charges named): the club's pay page for the member.</summary>
    Task<Result<PortalRedirectDto>> StartPaymentAsync(PortalMember who, PortalPayRequest request, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PortalEventDto>>> EventsAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<PortalEventDto>> RegisterAsync(PortalMember who, Guid eventId, CancellationToken ct = default);
    Task<Result<PortalEventDto>> CancelRegistrationAsync(PortalMember who, Guid registrationId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<PortalResourceDto>>> ResourcesAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PortalBookingDto>>> BookingsAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<PortalBookingDto>> BookAsync(PortalMember who, PortalBookingRequest request, CancellationToken ct = default);
    Task<Result<PortalBookingDto>> CancelBookingAsync(PortalMember who, Guid reservationId, CancellationToken ct = default);

    Task<Result<PortalConsentDto>> ConsentAsync(PortalMember who, CancellationToken ct = default);
    Task<Result<PortalConsentDto>> SetConsentAsync(PortalMember who, PortalConsentRequest request, CancellationToken ct = default);

    Task<Result<PortalPointsDto>> PointsAsync(PortalMember who, CancellationToken ct = default);
}

// ── sign-in ───────────────────────────────────────────────────────────────────

/// <param name="Open">Whether members can sign in now; a club with its portal (or its club) off is closed.</param>
/// <param name="Language">The club's own language, from its locale: the page's last resort, after the visitor's.</param>
public sealed record PortalClubDto(string ClubName, bool Open, string Locale, string? Language);

public sealed record PortalLinkRequest(string? Email);

public sealed record PortalRedeemRequest(string? Token);

/// <param name="Token">The member token: sent as Authorization: Bearer on every portal request.</param>
public sealed record PortalSessionStartedDto(string Token, DateTime ExpiresAtUtc, Guid MemberId, string FirstName, string ClubName, string? Language);

/// <summary>A session that stands, and the name staff see beside what the member does in it.</summary>
public sealed record PortalSessionCheck(string MemberName);

/// <param name="Url">The one-time sign-in address to send the member's browser to; it works once, for two minutes.</param>
public sealed record PortalHandoffLinkDto(string Url, DateTime ExpiresAtUtc);

// ── staff ─────────────────────────────────────────────────────────────────────

/// <param name="PortalUrl">The portal's sign-in page, for the club's website; null before it was first turned on.</param>
/// <param name="HandsOverToOwnPortal">The workspace also hands new members to its own portal (Welcome &amp; portal), so they would have two places.</param>
public sealed record MemberPortalSettingsDto(
    bool Enabled, string? PortalUrl, int MemberCancelHours, bool HandsOverToOwnPortal, DateTime? UpdatedAtUtc, Guid Version);

public sealed class SaveMemberPortalSettingsRequest
{
    public bool? Enabled { get; set; }
    public int? MemberCancelHours { get; set; }
    public Guid? Version { get; set; }
}

public sealed record MemberPortalUseDto
{
    public Guid MemberId { get; init; }
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; init; }
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? InvitedAtUtc { get; init; }
    public int InvitationsSent { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? FirstSignInAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastSeenAtUtc { get; init; }

    /// <summary>Sessions still open: signed in on that many devices.</summary>
    public int ActiveSessions { get; init; }

    /// <summary>Whether they can be invited: an email address, and not erased.</summary>
    public bool CanInvite { get; init; }

    /// <summary>Added through a welcome hand-over from a deal (DealOnboarding): they may also use the club's own portal.</summary>
    public bool FromHandoff { get; init; }
}

/// <param name="Invited">Members invited now.</param>
/// <param name="AlreadyInvited">Members left out because they were invited before.</param>
/// <param name="NoEmail">Members left out for want of an email address.</param>
/// <param name="Refused">Members whose invitation the outbox would not take (a bounced or suppressed address).</param>
/// <param name="Remaining">Members still to invite: one request invites a batch, and the next carries on.</param>
public sealed record PortalInviteAllDto(int Invited, int AlreadyInvited, int NoEmail, int Refused, int Remaining);

// ── the member's own ──────────────────────────────────────────────────────────

/// <summary>Who the member is to the club, and what the portal offers them.</summary>
public sealed record PortalMeDto
{
    public Guid MemberId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Phone { get; init; }

    /// <summary>The language the club writes to them in (en, de, fr); null: English, as the club always wrote.</summary>
    public string? PreferredLanguage { get; init; }

    public string Tier { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>Whether booking and signing up for events are open to them (an active membership).</summary>
    public bool Active { get; init; }

    public DateTime JoinedAtUtc { get; init; }
    public DateTime RenewsAtUtc { get; init; }

    /// <summary>When the membership starts, when known; with <see cref="StartUndetermined"/> it is still to be decided.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? StartsOnUtc { get; init; }
    public bool StartUndetermined { get; init; }

    public decimal DuesAmount { get; init; }
    public string DuesFrequency { get; init; } = string.Empty;

    public string ClubName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Up to how many hours before it starts the member may cancel an event place or a booking here.</summary>
    public int MemberCancelHours { get; init; }

    /// <summary>The club takes payments online (Stripe): the member can pay and save a card.</summary>
    public bool PaysOnline { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PortalHouseholdDto? Household { get; init; }
}

/// <summary>The household the member is in. The others' balances are shown to its primary only.</summary>
public sealed record PortalHouseholdDto(string Name, bool IsPrimary, string? PrimaryName, IReadOnlyList<PortalHouseholdPersonDto> People);

/// <param name="RollsUp">Their bills go on the primary's account.</param>
/// <param name="Balance">Their own account's balance (negative while they owe): shown to the primary only.</param>
public sealed record PortalHouseholdPersonDto(string Name, string Role, bool IsPrimary, bool IsYou, bool RollsUp, decimal? Balance);

/// <summary>Only what the member may change themselves: a field left out is kept, a phone sent as null is taken off.</summary>
public sealed class PortalProfileRequest : IMergePatchRequest
{
    public string? Phone { get; set; }

    /// <summary>en, de or fr: the portal's language and the one the club writes to them in.</summary>
    public string? PreferredLanguage { get; set; }

    [JsonIgnore]
    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>The member's account now: what they owe or have in credit, and what is still open.</summary>
/// <param name="Balance">Negative while they owe the club.</param>
/// <param name="FirstMonth">The first month with an entry (yyyy-MM), for the list of statements.</param>
public sealed record PortalAccountDto(
    string Currency, decimal Balance, decimal Owed, decimal Credit, IReadOnlyList<PortalChargeDto> OpenCharges,
    string FirstMonth, string CurrentMonth, bool BillsGoToPrimary, string? PrimaryName);

public sealed record PortalChargeDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal Outstanding { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime PostedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DueAtUtc { get; init; }

    /// <summary>For a household's primary: who in the household ran it up, when it was not them.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? IncurredByName { get; init; }
}

public sealed record PortalStatementDto(
    string Month, DateTime FromUtc, DateTime ToUtc, string Currency, decimal OpeningOwed, decimal ClosingOwed,
    decimal TotalCharges, decimal TotalPayments, IReadOnlyList<PortalStatementLineDto> Lines, bool HouseholdStatement);

/// <summary>One entry on the member's statement, with what they owe after it. No staff names, notes or reasons.</summary>
public sealed record PortalStatementLineDto(
    DateTime AtUtc, string EntryType, string Description, decimal Charge, decimal Payment, decimal Owed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IncurredByName);

/// <param name="PaysOnline">Whether the club takes payments online at all.</param>
public sealed record PortalCardDto(bool PaysOnline, SavedCardDto? Card, bool AutoPayDues);

public sealed class PortalCardSetupRequest
{
    /// <summary>The member agrees to their dues being charged to the card.</summary>
    public bool AutoPay { get; set; }
}

public sealed class PortalPayRequest
{
    /// <summary>Some of their open charges; everything owed when none are named.</summary>
    public List<Guid>? ChargeIds { get; set; }
}

/// <param name="Url">Where to send the member's browser.</param>
public sealed record PortalRedirectDto(string Url);

/// <summary>An event the member may sign up for, and where they stand on its list.</summary>
public sealed record PortalEventDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Location { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string CoverEmoji { get; init; } = string.Empty;
    public decimal MemberPrice { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>Seats still free; left out when there is no limit.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SeatsLeft { get; init; }

    /// <summary>The member's place: none, Reserved, Waitlisted, CheckedIn, Cancelled or NoShow.</summary>
    public string MyStatus { get; init; } = "none";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MyRegistrationId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? WaitlistPosition { get; init; }

    /// <summary>Whether they can still cancel it here (before the club's cut-off).</summary>
    public bool CanCancel { get; init; }

    /// <summary>For a household's primary: the others in the household on this event's list, by name.</summary>
    public IReadOnlyList<string> HouseholdComing { get; init; } = Array.Empty<string>();
}

/// <summary>Something the member may book in the portal.</summary>
/// <param name="ConfirmedAtOnce">Booked at once when free; otherwise a request the club confirms.</param>
public sealed record PortalResourceDto(
    Guid Id, string Name, string Kind, int Capacity, string? Description, decimal DepositAmount, string Currency, bool ConfirmedAtOnce);

public sealed record PortalBookingDto
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    public int PartySize { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }

    /// <summary>Pending (waiting for the club), Confirmed, CheckedIn, Completed, Cancelled or NoShow.</summary>
    public string Status { get; init; } = string.Empty;

    public decimal DepositAmount { get; init; }
    public string DepositState { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }

    /// <summary>Whether it is the member's own: a primary also sees the household's, and cancels only their own.</summary>
    public bool Mine { get; init; }

    /// <summary>For a household member's booking, whose it is.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ForName { get; init; }

    public bool CanCancel { get; init; }
}

public sealed class PortalBookingRequest
{
    public Guid? ResourceId { get; set; }
    public string? StartAtUtc { get; set; }
    public string? EndAtUtc { get; set; }
    public int? PartySize { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Where each channel and purpose stands for the member. Who recorded it on the club's side is not shown.</summary>
public sealed record PortalConsentDto(IReadOnlyList<PortalConsentStateDto> Current);

public sealed record PortalConsentStateDto(string Channel, string Purpose, string State, DateTime? RecordedAtUtc, string? Source);

public sealed class PortalConsentRequest
{
    /// <summary>email, sms or post.</summary>
    public string? Channel { get; set; }

    /// <summary>marketing or service.</summary>
    public string? Purpose { get; set; }

    public bool? Granted { get; set; }
}

public sealed record PortalPointsDto(int Balance, IReadOnlyList<PortalPointsEntryDto> History);

public sealed record PortalPointsEntryDto(DateTime AtUtc, string Kind, string Reason, int Points, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note);
