using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecretProtector _secretProtector;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ISecretProtector secretProtector)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _secretProtector = secretProtector;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);
        if (user?.PasswordHash == null)
            return null;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        if (user.TwoFactorEnabled)
        {
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
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<LoginResponse?> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default)
    {
        var userId = _tokenService.ValidateTwoFactorTokenAndGetUserId(request.TwoFactorToken);
        if (!userId.HasValue)
            return null;

        var user = await _userRepository.GetByIdAsync(userId.Value, ct);
        if (user == null || !user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
            return null;

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
            return null;

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);
        var token = _tokenService.GenerateToken(user);
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<LoginResponse?> RegisterAsync(LoginRequest request, string name, CancellationToken ct = default)
    {
        if (await _userRepository.GetByEmailAsync(request.Email, ct) != null)
            return null;

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
        return new LoginResponse(token, new UserInfoDto(user.Id, user.Name, user.Email));
    }

    public async Task<TwoFactorSetupResponse?> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return null;

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

        return new TwoFactorSetupResponse(
            Enabled: user.TwoFactorEnabled,
            Secret: secretBase32,
            OtpauthUri: otpauthUri);
    }

    public async Task<bool> EnableTwoFactorAsync(Guid userId, TwoFactorEnableRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return false;

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
            return false;

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
            return false;

        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);
        return true;
    }

    public async Task<bool> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || user.PasswordHash == null)
            return false;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return false;

        if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
            return false;

        var secret = _secretProtector.Unprotect(user.TwoFactorSecretProtected);
        if (!Totp.VerifyCode(secret, request.Code, DateTime.UtcNow))
            return false;

        user.TwoFactorEnabled = false;
        user.TwoFactorEnabledAtUtc = null;
        user.TwoFactorSecretProtected = null;
        await _userRepository.UpdateAsync(user, ct);
        return true;
    }
}
