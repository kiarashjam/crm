using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>
/// Service for authentication and two-factor authentication management.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecretProtector _secretProtector;
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ISecretProtector secretProtector,
        IEmailSender emailSender,
        IOptions<EmailSettings> emailSettings,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _secretProtector = secretProtector;
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Login attempt for email {Email}", request.Email);
        
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);
        if (user?.PasswordHash == null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", request.Email);
            return DomainErrors.Auth.InvalidCredentials;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: invalid password for user {UserId}", user.Id);
            return DomainErrors.Auth.InvalidCredentials;
        }

        if (user.TwoFactorEnabled)
        {
            _logger.LogDebug("2FA required for user {UserId}", user.Id);
            ClearPasswordResetFieldsIfPresent(user);
            await _userRepository.UpdateAsync(user, ct);
            var twoFactorToken = _tokenService.GenerateTwoFactorToken(user);
            return new LoginResponse(
                Token: null,
                User: null,
                RequiresTwoFactor: true,
                TwoFactorToken: twoFactorToken);
        }

        ClearPasswordResetFieldsIfPresent(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);
        var token = _tokenService.GenerateToken(user);
        
        _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<Result<LoginResponse>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("2FA login attempt");
        
        var userId = _tokenService.ValidateTwoFactorTokenAndGetUserId(request.TwoFactorToken);
        if (!userId.HasValue)
        {
            _logger.LogWarning("2FA login failed: invalid 2FA token");
            return DomainErrors.Auth.InvalidTwoFactorCode;
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, ct);
        if (user == null || !user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            _logger.LogWarning("2FA login failed: user {UserId} not found or 2FA not enabled", userId.Value);
            return DomainErrors.Auth.InvalidTwoFactorCode;
        }

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
        {
            _logger.LogWarning("2FA login failed: invalid TOTP code for user {UserId}", user.Id);
            return DomainErrors.Auth.InvalidTwoFactorCode;
        }

        ClearPasswordResetFieldsIfPresent(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);
        var token = _tokenService.GenerateToken(user);
        
        _logger.LogInformation("User {UserId} completed 2FA login successfully", user.Id);
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<Result<LoginResponse>> RegisterAsync(LoginRequest request, string name, CancellationToken ct = default)
    {
        _logger.LogInformation("Registration attempt for email {Email}", request.Email);
        
        if (await _userRepository.GetByEmailAsync(request.Email, ct) != null)
        {
            _logger.LogWarning("Registration failed: email {Email} already exists", request.Email);
            return DomainErrors.Auth.EmailAlreadyExists;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAtUtc = DateTime.UtcNow,
        };
        
        await _userRepository.AddAsync(user, ct);
        var token = _tokenService.GenerateToken(user);
        
        _logger.LogInformation("User {UserId} registered successfully with email {Email}", user.Id, request.Email);
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<Result<TwoFactorSetupResponse>> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting 2FA setup for user {UserId}", userId);
        
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            _logger.LogWarning("2FA setup failed: user {UserId} not found", userId);
            return DomainErrors.Auth.EmailNotFound;
        }

        // Generate (or reuse) secret until 2FA is enabled.
        string secretBase32;
        if (!string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            secretBase32 = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        }
        else
        {
            secretBase32 = Totp.GenerateBase32Secret();
            user.TwoFactorSecretProtected = _secretProtector.Protect(secretBase32);
            await _userRepository.UpdateAsync(user, ct);
        }

        var issuer = "ACI";
        var label = Uri.EscapeDataString($"{issuer}:{user.Email}");
        var issuerEncoded = Uri.EscapeDataString(issuer);
        var otpauthUri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuerEncoded}&digits=6&period=30";

        _logger.LogDebug("Generated 2FA setup for user {UserId}", userId);
        return new TwoFactorSetupResponse(
            Enabled: user.TwoFactorEnabled,
            Secret: secretBase32,
            OtpauthUri: otpauthUri);
    }

    public async Task<Result> EnableTwoFactorAsync(Guid userId, TwoFactorEnableRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Enabling 2FA for user {UserId}", userId);
        
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            _logger.LogWarning("Enable 2FA failed: user {UserId} not found", userId);
            return DomainErrors.Auth.EmailNotFound;
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            _logger.LogWarning("Enable 2FA failed: no 2FA secret for user {UserId}", userId);
            return Result.Failure(new Error("Auth.NoTwoFactorSecret", "Two-factor authentication has not been set up"));
        }

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
        {
            _logger.LogWarning("Enable 2FA failed: invalid code for user {UserId}", userId);
            return DomainErrors.Auth.InvalidTwoFactorCode;
        }

        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);
        
        _logger.LogInformation("2FA enabled successfully for user {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Disabling 2FA for user {UserId}", userId);
        
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || user.PasswordHash == null)
        {
            _logger.LogWarning("Disable 2FA failed: user {UserId} not found", userId);
            return DomainErrors.Auth.EmailNotFound;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Disable 2FA failed: invalid password for user {UserId}", userId);
            return DomainErrors.Auth.InvalidCredentials;
        }

        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            _logger.LogWarning("Disable 2FA failed: 2FA not enabled for user {UserId}", userId);
            return Result.Failure(new Error("Auth.TwoFactorNotEnabled", "Two-factor authentication is not enabled"));
        }

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
        {
            _logger.LogWarning("Disable 2FA failed: invalid code for user {UserId}", userId);
            return DomainErrors.Auth.InvalidTwoFactorCode;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorEnabledAtUtc = null;
        user.TwoFactorSecretProtected = null;
        await _userRepository.UpdateAsync(user, ct);
        
        _logger.LogInformation("2FA disabled successfully for user {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        _logger.LogInformation("Password reset requested for email {Email}", email);

        var user = await _userRepository.GetByEmailAsync(email, ct);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            // Same outcome as success to avoid account enumeration.
            return Result.Success();
        }

        var rawToken = PasswordResetCrypto.CreateRawToken();
        var tokenHash = PasswordResetCrypto.HashRawToken(rawToken);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        if (!TryNormalizePasswordResetBaseUrl(_emailSettings.FrontendBaseUrl, out var baseUrl))
        {
            _logger.LogError(
                "Password reset skipped for user {UserId}: Email:FrontendBaseUrl must be an absolute http(s) URL (current: {FrontendBaseUrl})",
                user.Id,
                string.IsNullOrWhiteSpace(_emailSettings.FrontendBaseUrl) ? "(empty)" : _emailSettings.FrontendBaseUrl);
            return Result.Success();
        }

        var resetUrl = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        var sent = await _emailSender.SendPasswordResetEmailAsync(user.Email, user.Name, resetUrl, ct);
        if (!sent)
        {
            _logger.LogWarning("Password reset email was not sent for user {UserId}; token not stored", user.Id);
            return Result.Success();
        }

        user.PasswordResetTokenHash = tokenHash;
        user.PasswordResetTokenExpiresAtUtc = expiresAt;
        await _userRepository.UpdateAsync(user, ct);

        _logger.LogInformation("Password reset email sent for user {UserId}", user.Id);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var rawToken = request.Token.Trim();
        if (string.IsNullOrEmpty(rawToken))
            return DomainErrors.Auth.InvalidResetToken;

        var tokenHash = PasswordResetCrypto.HashRawToken(rawToken);
        var user = await _userRepository.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        if (user == null
            || user.PasswordResetTokenExpiresAtUtc == null
            || user.PasswordResetTokenExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Password reset failed: invalid or expired token");
            return DomainErrors.Auth.InvalidResetToken;
        }

        user.PasswordHash = _passwordHasher.Hash(request.Password);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAtUtc = null;
        await _userRepository.UpdateAsync(user, ct);

        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
        return Result.Success();
    }

    /// <summary>
    /// Pending email reset links should not remain valid after the user proves password knowledge (login or 2FA step).
    /// </summary>
    private static void ClearPasswordResetFieldsIfPresent(User user)
    {
        if (user.PasswordResetTokenHash == null && user.PasswordResetTokenExpiresAtUtc == null)
            return;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAtUtc = null;
    }

    /// <summary>
    /// Reset emails must point at the SPA with an absolute URL so links are not host-relative to the API.
    /// </summary>
    private static bool TryNormalizePasswordResetBaseUrl(string? configured, out string baseUrl)
    {
        baseUrl = (configured ?? string.Empty).Trim().TrimEnd('/');
        if (baseUrl.Length == 0)
            return false;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (string.IsNullOrEmpty(uri.Host))
            return false;
        return true;
    }
}
