using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Login request.
/// </summary>
public record LoginRequest
{
    /// <summary>
    /// User email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }

    /// <summary>
    /// User password.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password is required")]
    public required string Password { get; init; }
}

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

/// <summary>
/// Two-factor authentication login request.
/// </summary>
public record TwoFactorLoginRequest
{
    /// <summary>
    /// Two-factor token from initial login.
    /// </summary>
    [Required(ErrorMessage = "TwoFactorToken is required")]
    [StringLength(500, ErrorMessage = "TwoFactorToken cannot exceed 500 characters")]
    public required string TwoFactorToken { get; init; }

    /// <summary>
    /// Two-factor authentication code.
    /// </summary>
    [Required(ErrorMessage = "Code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Code must be 6-10 characters")]
    public required string Code { get; init; }
}

public record TwoFactorSetupResponse(
    bool Enabled,
    string Secret, // Base32
    string OtpauthUri);

/// <summary>
/// Request to enable two-factor authentication.
/// </summary>
public record TwoFactorEnableRequest
{
    /// <summary>
    /// Two-factor authentication code.
    /// </summary>
    [Required(ErrorMessage = "Code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Code must be 6-10 characters")]
    public required string Code { get; init; }
}

/// <summary>
/// Request to disable two-factor authentication.
/// </summary>
public record TwoFactorDisableRequest
{
    /// <summary>
    /// User password for verification.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password is required")]
    public required string Password { get; init; }

    /// <summary>
    /// Two-factor authentication code.
    /// </summary>
    [Required(ErrorMessage = "Code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Code must be 6-10 characters")]
    public required string Code { get; init; }
}

/// <summary>
/// Request a password reset email.
/// </summary>
public record ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }
}

/// <summary>
/// Complete password reset using the token from the email link.
/// </summary>
public record ResetPasswordRequest
{
    [Required(ErrorMessage = "Token is required")]
    [StringLength(500, MinimumLength = 8, ErrorMessage = "Token is invalid")]
    public required string Token { get; init; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; init; }
}
