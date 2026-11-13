using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace ACI.Infrastructure.Services;

public sealed class JwtTokenService : ITokenService
{
    /// <summary>The claim carrying <see cref="User.SecurityStamp"/>, checked on every request.</summary>
    public const string SecurityStampClaim = "sstamp";

    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _secretKey;
    private readonly int _expiryMinutes;

    public JwtTokenService(string issuer, string audience, string secretKey, int expiryMinutes = 1440)
    {
        _issuer = issuer;
        _audience = audience;
        _secretKey = secretKey;
        _expiryMinutes = expiryMinutes;
    }

    public string GenerateToken(User user) => GenerateToken(user, SignInMethods.Password);

    public string GenerateToken(User user, string signInMethod)
    {
        return GenerateTokenInternal(
            user,
            extraClaims: null,
            signInMethod,
            expiresUtc: DateTime.UtcNow.AddMinutes(_expiryMinutes));
    }

    public string GenerateTwoFactorToken(User user) => GenerateTwoFactorToken(user, SignInMethods.Password);

    public string GenerateTwoFactorToken(User user, string signInMethod)
    {
        // Short-lived token used only to complete 2FA step after password verification.
        var extraClaims = new[] { new Claim("typ", "2fa") };
        return GenerateTokenInternal(
            user,
            extraClaims: extraClaims,
            signInMethod,
            expiresUtc: DateTime.UtcNow.AddMinutes(5));
    }

    public Guid? ValidateTokenAndGetUserId(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
            var principal = handler.ValidateToken(token, validation, out _);
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    public Guid? ValidateTwoFactorTokenAndGetUserId(string token) =>
        ReadTwoFactorToken(token) is { } principal
        && Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public string? TwoFactorSignInMethod(string token) =>
        ReadTwoFactorToken(token) is { } principal ? SignInMethods.Of(principal) : null;

    private ClaimsPrincipal? ReadTwoFactorToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
            var principal = handler.ValidateToken(token, validation, out _);
            var type = principal.FindFirst("typ")?.Value;
            return string.Equals(type, "2fa", StringComparison.Ordinal) ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateTokenInternal(User user, IEnumerable<Claim>? extraClaims, string signInMethod, DateTime expiresUtc)
    {
        if (!SignInMethods.IsKnown(signInMethod))
            throw new ArgumentOutOfRangeException(nameof(signInMethod), signInMethod, "Not a sign-in method (SignInMethods).");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            // How this session began; a workspace that requires single sign-on reads it.
            new Claim(SignInMethods.ClaimType, signInMethod),
            // When it began: adding a passkey to an account with no password takes a recent sign-in.
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };
        if (extraClaims != null)
            claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            expires: expiresUtc,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
