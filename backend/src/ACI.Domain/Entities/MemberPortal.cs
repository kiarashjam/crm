namespace ACI.Domain.Entities;

/// <summary>
/// Whether a workspace's members can sign in to see their own club account here (statements,
/// balance, payments, events, bookings, consent, points), and how. One row per organization;
/// none means the portal is off, which it is until someone who manages the settings turns it on.
/// </summary>
/// <remarks>
/// This is Cadence's own member portal, beside the hand-over to the club's own portal
/// (<see cref="OnboardingSettings"/>), which it does not replace: a club can use either or both.
/// </remarks>
public class MemberPortalSettings : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }

    /// <summary>Members can sign in. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The part of the portal's address that names the club (/member-portal/{key}). Random, so a
    /// club's portal is not found by trying names, and public: it is what the club puts on its site.
    /// </summary>
    public string PortalKey { get; set; } = string.Empty;

    /// <summary>
    /// Up to how many hours before an event or a booking starts a member may cancel it
    /// themselves; later, they ask the club.
    /// </summary>
    public int MemberCancelHours { get; set; } = 24;

    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Changes on every save: two people changing the settings at once are one success and one 409.</summary>
    public Guid Version { get; set; }
}

/// <summary>
/// One member's use of the portal: when they were invited and how often, and when they first
/// and last signed in. Made by the first invitation or the first sign-in, and kept when their
/// sessions and sign-in links are swept away, so staff still see who has used it.
/// </summary>
public class MemberPortalAccount : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    public DateTime? InvitedAtUtc { get; set; }
    public Guid? InvitedByUserId { get; set; }
    public int InvitationsSent { get; set; }

    public DateTime? FirstSignInAtUtc { get; set; }
    public DateTime? LastSignInAtUtc { get; set; }

    /// <summary>When a session of theirs was last used, to a few minutes.</summary>
    public DateTime? LastSeenAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A member signed in to the portal, on one device: what their session token names, so it can
/// be ended (by them, everywhere, or by staff) before it runs out.
/// </summary>
public class MemberPortalSession : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary><see cref="MemberPortalSignInWays"/>: what opened it.</summary>
    public string Via { get; set; } = MemberPortalSignInWays.Link;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When it was last used, to a few minutes.</summary>
    public DateTime LastSeenAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary><see cref="MemberPortalRevocations"/>: who ended it.</summary>
    public string? RevokedBy { get; set; }

    /// <summary>The staff member who ended it, when staff did.</summary>
    public Guid? RevokedByUserId { get; set; }
}

/// <summary>
/// A sign-in link emailed to a member (or handed to the club's own portal): a secret kept only
/// as its hash, used once, and short-lived. Deleting the row is what uses it, so of two requests
/// with the same link one signs in and the other is refused.
/// </summary>
public class MemberPortalLink : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary><see cref="MemberPortalSignInWays"/>: an invitation, a link the member asked for, or a hand-over.</summary>
    public string Purpose { get; set; } = MemberPortalSignInWays.Link;

    /// <summary>The SHA-256 of the secret, lower-case hex; the secret itself is only in the email.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>The staff member who invited them; null for a link the member asked for.</summary>
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>How a member came to sign in.</summary>
public static class MemberPortalSignInWays
{
    /// <summary>A link they asked for on the portal's sign-in page.</summary>
    public const string Link = "link";

    /// <summary>The invitation staff sent.</summary>
    public const string Invite = "invite";

    /// <summary>The club's own portal, for a member it received through the welcome hand-over.</summary>
    public const string Handoff = "handoff";

    public static readonly IReadOnlyList<string> All = [Link, Invite, Handoff];
}

/// <summary>Who ended a portal session before it ran out.</summary>
public static class MemberPortalRevocations
{
    /// <summary>The member signed out on that device.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>The member signed out everywhere.</summary>
    public const string Everywhere = "everywhere";

    /// <summary>Staff ended the member's sessions.</summary>
    public const string Staff = "staff";
}

/// <summary>Whether, and how, members book a resource themselves in the portal.</summary>
public static class MemberBookingModes
{
    /// <summary>Not offered in the portal: staff book it. What every resource starts as.</summary>
    public const string Off = "off";

    /// <summary>Members ask for it: the booking holds the time as pending until staff confirm it.</summary>
    public const string Request = "request";

    /// <summary>Members book it, confirmed at once, when it is free.</summary>
    public const string Instant = "instant";

    public static readonly IReadOnlyList<string> All = [Off, Request, Instant];
}
