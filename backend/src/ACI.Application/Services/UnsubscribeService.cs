using ACI.Application.Common.I18n;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>What the unsubscribe page shows: whose email stops, to which address, and how much of it.</summary>
/// <param name="Scope">
/// "all": every email the workspace sends to the address (the link in sales outreach).
/// "marketing": a club member's news and offers; their statements and booking confirmations still come.
/// </param>
/// <param name="Language">
/// For a club member's link, the language the club writes to them in (en, de, fr), which the
/// page reads in unless the visitor picks another. Null otherwise (no member, or none set on
/// them): the page then follows the visitor's browser.
/// </param>
public sealed record UnsubscribeDto(string WorkspaceName, string Email, string Scope = UnsubscribeScopes.All, string? Language = null);

public static class UnsubscribeScopes
{
    public const string All = "all";
    public const string Marketing = "marketing";
}

public interface IUnsubscribeService
{
    /// <summary>What a link would stop, without stopping it. Null for a link that is not one of ours.</summary>
    Task<UnsubscribeDto?> PreviewAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Marks the address do not contact in its workspace and stops the outreach
    /// still scheduled to it. Null for a link that is not one of ours.
    /// </summary>
    Task<UnsubscribeDto?> UnsubscribeAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// The link at the foot of every email a workspace sends to a lead or contact.
/// There was none: the only way to stop a sequence was to reply and hope someone
/// read it, and the do-not-contact flag could only be set by staff.
/// </summary>
/// <remarks>
/// Opening the link only shows what it will do; the change is a separate POST. Mail
/// scanners open every link in a message, and a link that unsubscribed on GET would
/// unsubscribe everyone whose company runs one.
/// </remarks>
public sealed class UnsubscribeService : IUnsubscribeService
{
    private readonly IUnsubscribeLinks _links;
    private readonly IOrganizationRepository _organizations;
    private readonly IContactRepository _contacts;
    private readonly ISequenceRepository _sequences;
    private readonly ILogger<UnsubscribeService> _logger;
    private readonly ITenantContext? _tenant;
    private readonly IConsentService? _consent;

    public UnsubscribeService(
        IUnsubscribeLinks links,
        IOrganizationRepository organizations,
        IContactRepository contacts,
        ISequenceRepository sequences,
        ILogger<UnsubscribeService> logger,
        ITenantContext? tenant = null,
        IConsentService? consent = null,
        IMemberMailRules? members = null)
    {
        _links = links;
        _organizations = organizations;
        _contacts = contacts;
        _sequences = sequences;
        _logger = logger;
        _tenant = tenant;
        _consent = consent;
        _members = members;
    }

    private readonly IMemberMailRules? _members;

    /// <summary>
    /// The member's own language, for the page to open in; null when none is set, and the page then
    /// follows the visitor's browser.
    /// </summary>
    private async Task<string?> MemberLanguageAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        AppLanguages.Normalise(_members == null ? null : await _members.PreferredLanguageAsync(organizationId, memberId, ct));

    public async Task<UnsubscribeDto?> PreviewAsync(string token, CancellationToken ct = default)
    {
        if (_links.TryReadMember(token, out var memberOrganizationId, out var previewMemberId, out var memberEmail))
        {
            var club = await _organizations.GetByIdAsync(memberOrganizationId, ct);
            if (club == null) return null;
            using var reading = _tenant?.ActFor(memberOrganizationId, club.OwnerUserId);
            return new UnsubscribeDto(club.Name, memberEmail, UnsubscribeScopes.Marketing,
                await MemberLanguageAsync(memberOrganizationId, previewMemberId, ct));
        }
        if (!_links.TryRead(token, out var organizationId, out var email)) return null;
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        return workspace == null ? null : new UnsubscribeDto(workspace.Name, email);
    }

    public async Task<UnsubscribeDto?> UnsubscribeAsync(string token, CancellationToken ct = default)
    {
        if (_links.TryReadMember(token, out var memberOrganizationId, out var memberId, out var memberEmail))
            return await WithdrawMarketingAsync(memberOrganizationId, memberId, memberEmail, ct);
        if (!_links.TryRead(token, out var organizationId, out var email)) return null;
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        if (workspace == null) return null;
        // The person unsubscribing signs in to nothing; the signed link names the workspace.
        using var acting = _tenant?.ActFor(organizationId, workspace.OwnerUserId);

        var marked = await _contacts.MarkDoNotContactAsync(organizationId, email, workspace.OwnerUserId, ct);
        // The runner would stop these at their next step anyway; stopping them now
        // means the enrollments list says so straight away.
        foreach (var contactId in marked.ContactIds)
            await _sequences.StopOpenForTargetAsync(organizationId, "contact", contactId, ct);
        foreach (var leadId in marked.LeadIds)
            await _sequences.StopOpenForTargetAsync(organizationId, "lead", leadId, ct);

        _logger.LogInformation("An address unsubscribed from organization {OrganizationId}'s email ({Contacts} contact(s), {Leads} lead(s))",
            organizationId, marked.ContactIds.Count, marked.LeadIds.Count);
        return new UnsubscribeDto(workspace.Name, email);
    }

    /// <summary>
    /// The link in a club member's marketing email: their consent to marketing by email is
    /// withdrawn, recorded as given by the unsubscribe link at this moment. Their statements,
    /// booking confirmations and receipts still come, so nobody is marked do not contact.
    /// Marketing already queued to them is stopped by the outbox, which asks again as it sends.
    /// </summary>
    private async Task<UnsubscribeDto?> WithdrawMarketingAsync(Guid organizationId, Guid memberId, string email, CancellationToken ct)
    {
        var club = await _organizations.GetByIdAsync(organizationId, ct);
        if (club == null || _consent == null) return null;
        // The person unsubscribing signs in to nothing; the signed link names the workspace.
        using var acting = _tenant?.ActFor(organizationId, club.OwnerUserId);
        var changed = await _consent.WithdrawMarketingEmailAsync(organizationId, memberId, email, ConsentSources.Unsubscribe,
            "Unsubscribed with the link in a marketing email", ct);
        _logger.LogInformation("A member unsubscribed from organization {OrganizationId}'s marketing email ({Members} member(s) changed)",
            organizationId, changed);
        return new UnsubscribeDto(club.Name, email, UnsubscribeScopes.Marketing, await MemberLanguageAsync(organizationId, memberId, ct));
    }
}
