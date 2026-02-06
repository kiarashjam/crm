using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for authentication and two-factor authentication management.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with token or 2FA requirement.</returns>
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Completes login with two-factor authentication code.
    /// </summary>
    /// <param name="request">Two-factor login request with token and code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with token.</returns>
    Task<Result<LoginResponse>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="name">User's display name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with token for the new account.</returns>
    Task<Result<LoginResponse>> RegisterAsync(LoginRequest request, string name, CancellationToken ct = default);

    /// <summary>
    /// Gets two-factor authentication setup information.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>2FA setup info including QR code URI.</returns>
    Task<Result<TwoFactorSetupResponse>> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Enables two-factor authentication for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="request">Request containing the verification code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    Task<Result> EnableTwoFactorAsync(Guid userId, TwoFactorEnableRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Disables two-factor authentication for a user.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="request">Request containing password and verification code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    Task<Result> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken ct = default);
}
