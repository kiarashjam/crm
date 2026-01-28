using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>
/// JWT token generation and validation (implemented in WebApi or Infrastructure).
/// </summary>
public interface ITokenService
{
    string GenerateToken(User user);
    string GenerateTwoFactorToken(User user);
    Guid? ValidateTokenAndGetUserId(string token);
    Guid? ValidateTwoFactorTokenAndGetUserId(string token);
}
