using System.Security.Claims;

namespace ACI.Application.Common;

/// <summary>
/// How a session began: with a password, through single sign-on (Google or Microsoft), or
/// with a passkey. Each session token carries it in one claim, otherwise the token is the
/// same whichever way the person signed in.
/// </summary>
/// <remarks>
/// A workspace that requires single sign-on (Organization.SsoRequired) refuses the sessions
/// of its members that did not begin with it, while the same session keeps working in the
/// person's other workspaces. The step token of a two-factor sign-in carries the method too,
/// so finishing with the code keeps it. A token from before the claim existed reads as a
/// password sign-in, which is what every one of them was.
/// </remarks>
public static class SignInMethods
{
    public const string ClaimType = "signin";

    public const string Password = "password";
    public const string Sso = "sso";
    public const string Passkey = "passkey";

    public static bool IsKnown(string? method) => method is Password or Sso or Passkey;

    /// <summary>The method the session in <paramref name="principal"/> began with.</summary>
    public static string Of(ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimType)?.Value is { } method && IsKnown(method) ? method : Password;
}
