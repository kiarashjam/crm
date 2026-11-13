using System.Security.Claims;

namespace ACI.Application.Common;

/// <summary>A member signed in to the member portal: the club, the member, and the session their token names.</summary>
public sealed record PortalMember(Guid OrganizationId, Guid MemberId, Guid SessionId);

/// <summary>
/// What a member's session token carries, and how it is read back. A member token is not a
/// staff token: it is signed with a key of its own and for an audience of its own, and is
/// validated only by the member portal's authentication scheme (Program.cs), so it reaches
/// the portal's endpoints and nothing else, and a staff token reaches everything but them.
/// </summary>
public static class MemberPortalClaims
{
    /// <summary>The authentication scheme, and the authentication type of the identity it makes.</summary>
    public const string Scheme = "MemberPortal";

    /// <summary>The audience member tokens are issued for; a staff token's is the API's own.</summary>
    public const string Audience = "cadence-member-portal";

    public const string Kind = "typ";
    public const string KindValue = "portal-member";
    public const string Organization = "org";
    public const string Member = "mid";
    public const string Session = "sid";

    /// <summary>
    /// The member a principal is, when the member portal's scheme signed it in; null for staff,
    /// for anyone anonymous, and for claims of the same names that came any other way.
    /// </summary>
    public static PortalMember? Of(ClaimsPrincipal? principal)
    {
        var identity = principal?.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == Scheme);
        if (identity == null || identity.FindFirst(Kind)?.Value != KindValue) return null;
        return Guid.TryParse(identity.FindFirst(Organization)?.Value, out var org)
            && Guid.TryParse(identity.FindFirst(Member)?.Value, out var member)
            && Guid.TryParse(identity.FindFirst(Session)?.Value, out var session)
                ? new PortalMember(org, member, session)
                : null;
    }
}
