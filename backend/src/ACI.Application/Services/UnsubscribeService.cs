using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>What the unsubscribe page shows: whose email stops, to which address.</summary>
public sealed record UnsubscribeDto(string WorkspaceName, string Email);

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

    public UnsubscribeService(
        IUnsubscribeLinks links,
        IOrganizationRepository organizations,
        IContactRepository contacts,
        ISequenceRepository sequences,
        ILogger<UnsubscribeService> logger)
    {
        _links = links;
        _organizations = organizations;
        _contacts = contacts;
        _sequences = sequences;
        _logger = logger;
    }

    public async Task<UnsubscribeDto?> PreviewAsync(string token, CancellationToken ct = default)
    {
        if (!_links.TryRead(token, out var organizationId, out var email)) return null;
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        return workspace == null ? null : new UnsubscribeDto(workspace.Name, email);
    }

    public async Task<UnsubscribeDto?> UnsubscribeAsync(string token, CancellationToken ct = default)
    {
        if (!_links.TryRead(token, out var organizationId, out var email)) return null;
        var workspace = await _organizations.GetByIdAsync(organizationId, ct);
        if (workspace == null) return null;

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
}
