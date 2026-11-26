using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <inheritdoc cref="IPlatformAdmins"/>
/// <remarks>
/// The list is read from Platform:AdminEmails as the configuration stands at each call, so a
/// change the host reloads takes effect without a restart. An account counts only once it has
/// confirmed its address: anyone can register with any address, and only proving they read its
/// mail makes it theirs (accounts from before confirmation existed were marked confirmed).
/// </remarks>
public sealed class PlatformAdmins : IPlatformAdmins
{
    private readonly IOptionsMonitor<PlatformSettings> _settings;
    private readonly IUserRepository _users;

    public PlatformAdmins(IOptionsMonitor<PlatformSettings> settings, IUserRepository users)
    {
        _settings = settings;
        _users = users;
    }

    public IReadOnlyList<string> Emails => Parse(_settings.CurrentValue.AdminEmails);

    /// <summary>The valid addresses in a comma- or semicolon-separated list, lower case, each once.</summary>
    public static IReadOnlyList<string> Parse(string? list) =>
        (list ?? "")
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.ToLowerInvariant())
            .Where(ValidationHelper.IsValidEmail)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public bool IsAdminEmail(string? email)
    {
        var address = email?.Trim();
        return !string.IsNullOrEmpty(address) && Emails.Contains(address, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken ct = default)
    {
        if (Emails.Count == 0) return false;
        var user = await _users.GetByIdAsync(userId, ct);
        return user is { EmailVerifiedAtUtc: not null } && IsAdminEmail(user.Email);
    }
}
