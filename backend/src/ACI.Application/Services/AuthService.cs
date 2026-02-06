using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ISecretProtector secretProtector,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _secretProtector = secretProtector;
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
            var twoFactorToken = _tokenService.GenerateTwoFactorToken(user);
            return new LoginResponse(
                Token: null,
                User: null,
                RequiresTwoFactor: true,
                TwoFactorToken: twoFactorToken);
        }

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
}
