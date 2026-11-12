using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// What a sign-in hands back once its first step has proved who someone is: the session, or,
/// while they have two-factor sign-in on, the step token for the second.
/// </summary>
/// <remarks>
/// Single sign-on and passkeys finish here, so they give the same session a password does
/// (ITokenService.GenerateToken, with the user's security stamp) and hold to the same 2FA.
/// </remarks>
public sealed class SignInSessions
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly ILogger<SignInSessions> _logger;

    public SignInSessions(IUserRepository users, ITokenService tokens, ILogger<SignInSessions> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    /// <param name="user">The person, tracked by the request's context.</param>
    /// <param name="signInMethod">How they proved it (SignInMethods).</param>
    /// <param name="secondFactorDone">
    /// Whether that proof was both factors already. A passkey that verified the person on the
    /// device (its PIN or fingerprint) is; single sign-on is not, as nothing says the provider
    /// asked for a second factor, so someone who turned 2FA on here is asked for it here.
    /// </param>
    public async Task<LoginResponse> IssueAsync(User user, string signInMethod, bool secondFactorDone, CancellationToken ct = default)
    {
        if (user.TwoFactorEnabled && !secondFactorDone)
        {
            _logger.LogDebug("2FA required for user {UserId} after {Method} sign-in", user.Id, signInMethod);
            return new LoginResponse(
                Token: null,
                User: null,
                RequiresTwoFactor: true,
                TwoFactorToken: _tokens.GenerateTwoFactorToken(user, signInMethod));
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);
        _logger.LogInformation("User {UserId} signed in with {Method}", user.Id, signInMethod);
        return new LoginResponse(_tokens.GenerateToken(user, signInMethod), Info(user));
    }

    public static UserInfoDto Info(User user) =>
        new(user.Id, user.Name, user.Email, EmailVerified: user.EmailVerifiedAtUtc != null);
}
