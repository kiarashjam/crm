using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using Microsoft.IdentityModel.Tokens;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>
/// Issues the member portal's session tokens, and says how they are checked (Program.cs).
/// </summary>
/// <remarks>
/// A member token is signed with a key of its own, derived from the API's JWT secret, and is
/// for an audience of its own. The staff scheme checks the staff key and audience, and the
/// member scheme these, so a member token is refused everywhere but the portal and a staff
/// token there: each check alone would refuse the other's token, and both are made.
/// </remarks>
public sealed class MemberPortalTokens
{
    /// <summary>How long a member stays signed in on a device, unless they or staff end it first.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private readonly string _issuer;
    private readonly SymmetricSecurityKey _key;

    public MemberPortalTokens(string issuer, string jwtSecret)
    {
        _issuer = issuer;
        _key = SigningKey(jwtSecret);
    }

    /// <summary>The member tokens' key: the API's JWT secret run through HMAC with a label, never the secret itself.</summary>
    public static SymmetricSecurityKey SigningKey(string jwtSecret) =>
        new(HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtSecret), Encoding.UTF8.GetBytes("cadence-member-portal-session-v1")));

    /// <summary>What the member scheme checks: this key and issuer, the portal's audience, the lifetime to the second.</summary>
    public static TokenValidationParameters Validation(string issuer, string jwtSecret) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(jwtSecret),
        ValidIssuer = issuer,
        ValidAudience = MemberPortalClaims.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        // The identity says it came from the member scheme, which is what MemberPortalClaims.Of trusts.
        AuthenticationType = MemberPortalClaims.Scheme,
        NameClaimType = ClaimTypes.Name,
    };

    public string Issue(PortalMember who, DateTime expiresUtc)
    {
        var claims = new[]
        {
            new Claim(MemberPortalClaims.Kind, MemberPortalClaims.KindValue),
            new Claim(MemberPortalClaims.Organization, who.OrganizationId.ToString()),
            new Claim(MemberPortalClaims.Member, who.MemberId.ToString()),
            new Claim(MemberPortalClaims.Session, who.SessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: MemberPortalClaims.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
