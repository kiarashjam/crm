using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace ACI.Infrastructure.Services;

public sealed class JwtTokenService : ITokenService
{
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

    public string GenerateToken(User user)
    {
        return GenerateTokenInternal(
            user,
            extraClaims: null,
            expiresUtc: DateTime.UtcNow.AddMinutes(_expiryMinutes));
    }

    public string GenerateTwoFactorToken(User user)
    {
        // Short-lived token used only to complete 2FA step after password verification.
        var extraClaims = new[] { new Claim("typ", "2fa") };
        return GenerateTokenInternal(
            user,
            extraClaims: extraClaims,
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

    public Guid? ValidateTwoFactorTokenAndGetUserId(string token)
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
            if (!string.Equals(type, "2fa", StringComparison.Ordinal))
                return null;
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateTokenInternal(User user, IEnumerable<Claim>? extraClaims, DateTime expiresUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
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
