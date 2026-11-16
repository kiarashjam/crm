using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// JWT token generation and validation (implemented in WebApi or Infrastructure).
/// </summary>
public interface ITokenService
{
    /// <summary>A session for someone who signed in with their password.</summary>
    string GenerateToken(User user);

    /// <summary>A session for someone who signed in another way (see SignInMethods).</summary>
    string GenerateToken(User user, string signInMethod);

    /// <summary>The short-lived token between a password and the two-factor code.</summary>
    string GenerateTwoFactorToken(User user);

    /// <summary>The same step after single sign-on, which the session then carries on.</summary>
    string GenerateTwoFactorToken(User user, string signInMethod);

    Guid? ValidateTokenAndGetUserId(string token);
    Guid? ValidateTwoFactorTokenAndGetUserId(string token);

    /// <summary>How the person began signing in, for a valid two-factor step token; null for anything else.</summary>
    string? TwoFactorSignInMethod(string token);
}
