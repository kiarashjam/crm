using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>Who a workspace's email goes out from, as Settings shows it.</summary>
/// <param name="SendingAddress">The workspace's own sender address, as saved; null when none is.</param>
/// <param name="Verified">
/// Whether the mail service has verified its domain, so email really goes out from it.
/// Until then it goes out from <paramref name="SharedAddress"/>.
/// </param>
/// <param name="SharedAddress">The configured address every workspace without a verified one sends from.</param>
/// <param name="RepliesCaptured">Whether replies come back into the CRM as well as reaching the sender.</param>
public sealed record EmailSendingDto(string? SendingAddress, bool Verified, string? SharedAddress, bool RepliesCaptured);

/// <param name="SendingAddress">The address to send from; empty clears it.</param>
public sealed record UpdateEmailSendingRequest(string? SendingAddress);

public interface IEmailSendingService
{
    Task<Result<EmailSendingDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<Result<EmailSendingDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateEmailSendingRequest request, CancellationToken ct = default);
}

/// <summary>
/// The workspace's own sending address. Every workspace sends from the one configured
/// address, the only one the relay has verified; a workspace may name its own, which is
/// used as From only once its domain is verified with the mail service.
/// </summary>
/// <remarks>
/// Verification is the mail service's (SendGrid domain authentication, DNS records on
/// the workspace's domain). Whoever runs Cadence lists each verified domain in
/// Email:VerifiedSendingDomains; the address saved here does nothing until then, and
/// Settings says so rather than claiming email goes out from it.
/// </remarks>
public sealed class EmailSendingService : IEmailSendingService
{
    private readonly IOrganizationRepository _organizations;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSendingService> _logger;

    public EmailSendingService(IOrganizationRepository organizations, IOptions<EmailSettings> settings, ILogger<EmailSendingService> logger)
    {
        _organizations = organizations;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<EmailSendingDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (await _organizations.GetMemberRoleAsync(userId, organizationId, ct) == null) return DomainErrors.Organization.NotMember;
        var org = await _organizations.GetByIdAsync(organizationId, ct);
        return org == null ? DomainErrors.Organization.NotFound : Dto(org.SendingAddress);
    }

    public async Task<Result<EmailSendingDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateEmailSendingRequest request, CancellationToken ct = default)
    {
        // Who every customer's email appears to come from: a manager's decision.
        var role = await _organizations.GetMemberRoleAsync(userId, organizationId, ct);
        if (role == null) return DomainErrors.Organization.NotMember;
        if (role is not (OrgMemberRole.Owner or OrgMemberRole.Manager)) return DomainErrors.Organization.NotOwnerOrManager;
        var org = await _organizations.GetByIdAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;

        var address = (request.SendingAddress ?? "").Trim();
        if (address.Length > 256 || (address.Length > 0 && !ValidationHelper.IsValidEmail(address)))
            return new Error("Organization.SendingAddress", $"\"{address}\" is not an email address.");
        org.SendingAddress = address.Length == 0 ? null : address;
        await _organizations.UpdateAsync(org, ct);
        _logger.LogInformation("Organization {OrganizationId}'s sending address was {Change} by {UserId}",
            organizationId, org.SendingAddress == null ? "cleared" : "set", userId);
        return Dto(org.SendingAddress);
    }

    private EmailSendingDto Dto(string? sendingAddress)
    {
        var domain = sendingAddress is { } a && a.LastIndexOf('@') is var at and > 0 ? a[(at + 1)..] : null;
        return new EmailSendingDto(
            sendingAddress,
            domain != null && _settings.IsVerifiedSendingDomain(domain),
            string.IsNullOrWhiteSpace(_settings.FromAddress) ? null : _settings.FromAddress.Trim(),
            _settings.CapturesReplies);
    }
}
