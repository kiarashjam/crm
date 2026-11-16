using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

/// <summary>Who a workspace's email goes out as, and who a reply reaches.</summary>
/// <param name="WorkspaceName">Shown beside the sender address. Null when there is no workspace.</param>
/// <param name="ReplyTo">The address replies go to, or null when nobody suitable has one.</param>
/// <param name="ReplyToName">The name of the person <paramref name="ReplyTo"/> belongs to.</param>
/// <param name="FromAddress">
/// The workspace's own sender address, when its domain is one the mail service has
/// verified; null means the configured shared address.
/// </param>
public sealed record OutreachSender(string? WorkspaceName, string? ReplyTo, string? ReplyToName = null, string? FromAddress = null);

/// <summary>The two parts of a workspace's email as they go out, footer included.</summary>
/// <param name="UnsubscribeUrl">
/// For the List-Unsubscribe header when the message is for one address: the one-click
/// address when <paramref name="OneClick"/>, otherwise the unsubscribe page; null otherwise.
/// </param>
/// <param name="OneClick">Whether <paramref name="UnsubscribeUrl"/> takes the one-click POST of RFC 8058.</param>
public sealed record OutreachBody(string Text, string Html, string? UnsubscribeUrl, bool OneClick = false);

/// <summary>
/// The sending side of email that leaves on a workspace's behalf, which the outbox
/// uses for every message: who it is from, who a reply reaches, and the footer.
/// </summary>
public interface IOutreachMail
{
    /// <summary>
    /// The workspace's name to send under, and the email of the person a reply
    /// should reach: <paramref name="replyToUserId"/> while they are still in the
    /// workspace, otherwise its owner.
    /// </summary>
    Task<OutreachSender> SenderAsync(Guid? organizationId, Guid? replyToUserId, CancellationToken ct = default);

    /// <summary>
    /// The text and HTML parts with an unsubscribe link for each of
    /// <paramref name="addresses"/> at the foot, and the tracking image in the HTML
    /// part when one is given.
    /// </summary>
    OutreachBody Body(Guid? organizationId, string? workspaceName, string text, IReadOnlyList<string> addresses, string? trackingUrl = null);

    /// <summary>
    /// The two parts of an email to one club member. Marketing carries a link that withdraws
    /// the member's consent to marketing by email; a service message (a statement, a
    /// booking) carries none, since the member's choices do not stop it.
    /// </summary>
    /// <remarks>The footer of a marketing email is in <paramref name="language"/>, the member's.</remarks>
    OutreachBody MemberBody(Guid organizationId, string? workspaceName, string text, Guid memberId, string address, bool marketing,
        string language = AppLanguages.English);

    /// <summary>The email-open image for a lead, or null when this API has no public address to give.</summary>
    string? TrackingUrl(Guid leadId);
}

/// <remarks>
/// Every workspace sends from the one configured address (Email:FromAddress), the
/// only one the relay has verified. Sequence and automation mail went out as
/// "Cadence", and nothing set Reply-To, so a prospect who answered wrote to that
/// shared inbox — another company's, for any workspace but the one that owns it.
/// None of it said how to stop it either: there was no unsubscribe link anywhere.
/// </remarks>
public sealed class OutreachMail : IOutreachMail
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;
    private readonly IUnsubscribeLinks _links;
    private readonly EmailSettings _settings;
    private readonly ILogger<OutreachMail> _logger;

    public OutreachMail(
        IOrganizationRepository organizations,
        IUserRepository users,
        IUnsubscribeLinks links,
        IOptions<EmailSettings> settings,
        ILogger<OutreachMail> logger)
    {
        _organizations = organizations;
        _users = users;
        _links = links;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<OutreachSender> SenderAsync(Guid? organizationId, Guid? replyToUserId, CancellationToken ct = default)
    {
        var workspace = organizationId is Guid id ? await _organizations.GetByIdAsync(id, ct) : null;
        var from = VerifiedSendingAddress(workspace?.SendingAddress, workspace?.Id);
        foreach (var candidate in new[] { replyToUserId, workspace?.OwnerUserId }.Distinct())
        {
            if (candidate is not Guid userId) continue;
            // Someone who has left no longer answers for this workspace.
            if (organizationId is Guid org && await _organizations.GetMemberRoleAsync(userId, org, ct) == null) continue;
            var user = await _users.GetByIdAsync(userId, ct);
            var email = user?.Email?.Trim();
            if (!string.IsNullOrEmpty(email) && ValidationHelper.IsValidEmail(email))
                return new OutreachSender(workspace?.Name, email, user!.Name?.Trim(), from);
        }
        return new OutreachSender(workspace?.Name, null, null, from);
    }

    /// <summary>
    /// The workspace's own address, only while the mail service has verified its domain
    /// for this workspace. The relay refuses mail from a domain it has not verified, so
    /// until then the configured shared address is used, which it accepts. Another
    /// workspace naming the same domain gets the shared address too: any workspace may
    /// save any address, and must not send as someone else's verified domain.
    /// </summary>
    private string? VerifiedSendingAddress(string? sendingAddress, Guid? organizationId)
    {
        var address = (sendingAddress ?? "").Trim();
        if (address.Length == 0 || !ValidationHelper.IsValidEmail(address)) return null;
        return _settings.IsVerifiedSendingDomain(address[(address.LastIndexOf('@') + 1)..], organizationId) ? address : null;
    }

    public string? TrackingUrl(Guid leadId)
    {
        // The setting defaults to http://localhost:5160, and nothing in production
        // sets it. An image pointing there can never be fetched by a recipient, so it
        // is left out rather than shipped broken.
        if (!Uri.TryCreate((_settings.ApiBaseUrl ?? "").Trim(), UriKind.Absolute, out var api)) return null;
        if (api.IsLoopback || api.Scheme != Uri.UriSchemeHttps) return null;
        return $"{api.GetLeftPart(UriPartial.Authority)}{api.AbsolutePath.TrimEnd('/')}/api/public/track/{leadId}";
    }

    public OutreachBody Body(Guid? organizationId, string? workspaceName, string text, IReadOnlyList<string> addresses, string? trackingUrl = null)
    {
        var links = new List<UnsubscribeLink>();
        // A "stop" is kept per workspace; outreach always has one (its endpoints require it).
        if (organizationId is Guid org)
        {
            if (TryPageBase(out var page))
            {
                foreach (var address in addresses.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                    links.Add(new UnsubscribeLink(address, $"{page}/unsubscribe/{Uri.EscapeDataString(_links.CreateToken(org, address))}"));
            }
            else
            {
                // The link points at the app's own page, so without a public address
                // (Email:FrontendBaseUrl) there is nothing to link to. Logged rather
                // than hidden: in production this setting is required.
                _logger.LogWarning("No unsubscribe link in outreach for organization {OrganizationId}: Email:FrontendBaseUrl is not an absolute http(s) URL", org);
            }
        }

        // One person: their mail app's own Unsubscribe button can stop it in one click.
        var oneClick = links.Count == 1 && organizationId is Guid only
            ? OneClickUrl(_links.CreateToken(only, links[0].Address))
            : null;
        return new OutreachBody(
            text + PlainTextEmail.TextFooter(workspaceName, links),
            PlainTextEmail.ToHtml(text, trackingUrl, workspaceName, links),
            oneClick ?? (links.Count == 1 ? links[0].Url : null),
            oneClick != null);
    }

    public OutreachBody MemberBody(Guid organizationId, string? workspaceName, string text, Guid memberId, string address, bool marketing,
        string language = AppLanguages.English)
    {
        if (!marketing)
            return new OutreachBody(text, PlainTextEmail.ToHtml(text, null, workspaceName), null);

        var token = _links.CreateMemberToken(organizationId, memberId, address);
        var links = new List<UnsubscribeLink>();
        if (TryPageBase(out var page))
            links.Add(new UnsubscribeLink(address, $"{page}/unsubscribe/{Uri.EscapeDataString(token)}"));
        else
            _logger.LogWarning("No unsubscribe link in a marketing email for organization {OrganizationId}: Email:FrontendBaseUrl is not an absolute http(s) URL", organizationId);
        var question = PlainTextEmail.MarketingQuestion(workspaceName, language);
        var oneClick = OneClickUrl(token);
        return new OutreachBody(
            text + PlainTextEmail.TextFooter(workspaceName, links, question, language),
            PlainTextEmail.ToHtml(text, null, workspaceName, links, question, language),
            oneClick ?? links.FirstOrDefault()?.Url,
            oneClick != null);
    }

    /// <summary>
    /// The API's one-click unsubscribe address for a token (RFC 8058), or null when this API
    /// has no public https address to give (Email:ApiBaseUrl). A mail app POSTs to it from
    /// its own Unsubscribe button, so it has to be https and reachable from outside; with
    /// the localhost default the page link is given instead.
    /// </summary>
    private string? OneClickUrl(string token)
    {
        if (!Uri.TryCreate((_settings.ApiBaseUrl ?? "").Trim(), UriKind.Absolute, out var api)) return null;
        if (api.IsLoopback || api.Scheme != Uri.UriSchemeHttps) return null;
        return $"{api.GetLeftPart(UriPartial.Authority)}{api.AbsolutePath.TrimEnd('/')}/api/public/unsubscribe/{Uri.EscapeDataString(token)}/one-click";
    }

    /// <summary>The app's own address, where the unsubscribe page is — the same rule as password reset links.</summary>
    private bool TryPageBase(out string baseUrl)
    {
        baseUrl = (_settings.FrontendBaseUrl ?? "").Trim().TrimEnd('/');
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }
}
