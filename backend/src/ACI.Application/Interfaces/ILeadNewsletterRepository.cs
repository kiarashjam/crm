using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>A lead as a newsletter needs it: who to greet, where to write, and what the filters read.</summary>
public sealed record NewsletterLead(
    Guid Id, string Name, string? Email, string Status, Guid? ConvertedToContactId, DateTime CreatedAtUtc);

/// <summary>Of a newsletter's leads, those email may not go to.</summary>
/// <param name="DoNotContact">Leads marked do not contact: through their person contact, or a contact with their address.</param>
/// <param name="Suppressed">Addresses (normalised) the mail service reported: unsubscribed, bounced, invalid, complained, or an erased person's.</param>
public sealed record NewsletterUnreachable(IReadOnlySet<Guid> DoNotContact, IReadOnlySet<string> Suppressed);

/// <summary>Newsletters to leads: who they go to, and each send's record.</summary>
public interface ILeadNewsletterRepository
{
    /// <summary>The workspace's leads the filters choose, oldest first, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<NewsletterLead>> LeadsAsync(Guid organizationId, NewsletterAudienceRequest filters, int take, CancellationToken ct = default);

    /// <summary>One of the workspace's leads, as a newsletter needs it.</summary>
    Task<NewsletterLead?> LeadAsync(Guid organizationId, Guid leadId, CancellationToken ct = default);

    /// <summary>Of these leads, those marked do not contact, and of their addresses, those suppressed.</summary>
    Task<NewsletterUnreachable> UnreachableAsync(Guid organizationId, IReadOnlyCollection<NewsletterLead> leads, CancellationToken ct = default);

    Task<LeadNewsletter?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Saves a new send. False when one with its id is there already: the same press sent
    /// twice, by whichever request got there second.
    /// </summary>
    Task<bool> TryAddAsync(LeadNewsletter newsletter, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Meetings leads booked on the workspace's website.</summary>
public interface IMeetingBookingRepository
{
    Task<MeetingBooking?> FindAsync(Guid organizationId, string bookingId, CancellationToken ct = default);
    void Add(MeetingBooking booking);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// The workspace's lead with this address: an open one first, else the latest. The
    /// person's own address, or an establishment's contact person's.
    /// </summary>
    Task<Lead?> LeadByEmailAsync(Guid organizationId, string email, CancellationToken ct = default);
}
