using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Guessing a password, a 2FA code, or flooding someone's inbox with reset links
/// stops after a few tries per account.
/// </summary>
public class AuthAttemptLimiterTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly Clock _clock = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ISecretProtector> _protector = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly AuthService _sut;
    private readonly User _user = new()
    {
        Id = Guid.NewGuid(), Name = "Anna", Email = "anna@example.ch", PasswordHash = "hash",
    };

    public AuthAttemptLimiterTests()
    {
        _users.Setup(r => r.GetByEmailAsync(_user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _users.Setup(r => r.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _hasher.Setup(h => h.Verify("right", "hash")).Returns(true);
        _tokens.Setup(t => t.GenerateToken(_user)).Returns("session");
        _email.Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new AuthService(
            _users.Object, _tokens.Object, _hasher.Object, _protector.Object, _email.Object,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://crm.example.ch" }),
            Mock.Of<ILogger<AuthService>>(),
            new AuthAttemptLimiter(_clock));
    }

    private Task<ACI.Application.Common.Result<LoginResponse>> Login(string password) =>
        _sut.LoginAsync(new LoginRequest { Email = _user.Email, Password = password });

    [Fact]
    public async Task FiveWrongPasswordsLockTheAccountEvenForTheRightOne()
    {
        for (var i = 0; i < AuthAttemptLimiter.MaxFailures; i++)
            (await Login("wrong")).Error.Code.Should().Be("Auth.InvalidCredentials");

        var locked = await Login("right");
        locked.IsSuccess.Should().BeFalse();
        locked.Error.Code.Should().Be("Auth.AccountLocked");
    }

    [Fact]
    public async Task TheLockEndsWhenTheWindowHasPassed()
    {
        for (var i = 0; i < AuthAttemptLimiter.MaxFailures; i++) await Login("wrong");

        _clock.Now += AuthAttemptLimiter.Window;

        (await Login("right")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ASuccessfulLoginClearsEarlierTypos()
    {
        for (var i = 0; i < AuthAttemptLimiter.MaxFailures - 1; i++) await Login("wrong");
        (await Login("right")).IsSuccess.Should().BeTrue();

        for (var i = 0; i < AuthAttemptLimiter.MaxFailures - 1; i++) await Login("wrong");
        (await Login("right")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FiveWrongTwoFactorCodesStopFurtherGuesses()
    {
        _user.TwoFactorEnabled = true;
        _user.TwoFactorSecretProtected = "protected";
        _protector.Setup(p => p.Unprotect("protected")).Returns(Totp.GenerateBase32Secret());
        _tokens.Setup(t => t.ValidateTwoFactorTokenAndGetUserId("step")).Returns(_user.Id);

        Task<ACI.Application.Common.Result<LoginResponse>> Code() =>
            _sut.LoginWithTwoFactorAsync(new TwoFactorLoginRequest { TwoFactorToken = "step", Code = "000000" });

        for (var i = 0; i < AuthAttemptLimiter.MaxFailures; i++)
            (await Code()).Error.Code.Should().NotBe("Auth.AccountLocked");

        (await Code()).Error.Code.Should().Be("Auth.AccountLocked");
    }

    [Fact]
    public async Task ResetEmailsStopAfterThreeAndTheCallerCannotTell()
    {
        for (var i = 0; i < AuthAttemptLimiter.MaxResetEmails + 2; i++)
            (await _sut.RequestPasswordResetAsync(new ForgotPasswordRequest { Email = _user.Email })).IsSuccess.Should().BeTrue();

        _email.Verify(e => e.SendPasswordResetEmailAsync(_user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(AuthAttemptLimiter.MaxResetEmails));
    }
}
