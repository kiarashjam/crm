using ACI.Application.Configuration;
using ACI.Application.Services;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IMemberPayLinks"/>
/// <remarks>
/// The same signed link staff send from a member's account (<see cref="ClubPaymentsService"/>):
/// it opens Cadence's own page, which works out what is owed when the member presses Pay, so a
/// link in a statement never asks for money paid since.
/// </remarks>
public sealed class MemberPayLinks : IMemberPayLinks
{
    private readonly ClubPaymentSettingsService _settings;
    private readonly PayLinkSigner _links;
    private readonly EmailSettings _email;

    public MemberPayLinks(ClubPaymentSettingsService settings, PayLinkSigner links, IOptions<EmailSettings> email)
    {
        _settings = settings;
        _links = links;
        _email = email.Value;
    }

    public async Task<MemberPayLink?> ForEmailAsync(Guid organizationId, Guid memberId, IReadOnlyList<Guid>? chargeIds = null, CancellationToken ct = default)
    {
        var page = (_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(page, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || uri.Host.Length == 0)
            return null;
        if (!(await _settings.ConfigAsync(organizationId, ct)).OnlinePayments) return null;
        var charges = chargeIds is { Count: > 0 and <= ClubPaymentsService.MaxChargesNamed } ? chargeIds.Distinct().ToList() : [];
        var expires = DateTime.UtcNow.Add(ClubPaymentsService.PayLinkLifetime);
        var claim = new PayLinkClaim(organizationId, memberId, charges.Count > 0 ? ClubPaymentsService.Charges : ClubPaymentsService.Account, charges, false, expires);
        return new MemberPayLink($"{page}/pay/{Uri.EscapeDataString(_links.Create(claim))}", expires);
    }
}
