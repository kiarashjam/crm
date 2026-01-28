namespace ACI.Application.DTOs;

public record LoginRequest(string Email, string Password);

/// <summary>
/// Auth response.
/// - When <see cref="RequiresTwoFactor"/> is false: <see cref="Token"/> and <see cref="User"/> are set.
/// - When <see cref="RequiresTwoFactor"/> is true: <see cref="TwoFactorToken"/> is set (short-lived), and Token/User are null.
/// </summary>
public record LoginResponse(
    string? Token,
    UserInfoDto? User,
    bool RequiresTwoFactor = false,
    string? TwoFactorToken = null);

public record UserInfoDto(Guid Id, string Name, string Email);

public record TwoFactorLoginRequest(string TwoFactorToken, string Code);

public record TwoFactorSetupResponse(
    bool Enabled,
    string Secret, // Base32
    string OtpauthUri);

public record TwoFactorEnableRequest(string Code);

public record TwoFactorDisableRequest(string Password, string Code);
