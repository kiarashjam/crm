using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse?> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default);
    Task<LoginResponse?> RegisterAsync(LoginRequest request, string name, CancellationToken ct = default);

    Task<TwoFactorSetupResponse?> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default);
    Task<bool> EnableTwoFactorAsync(Guid userId, TwoFactorEnableRequest request, CancellationToken ct = default);
    Task<bool> DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken ct = default);
}
