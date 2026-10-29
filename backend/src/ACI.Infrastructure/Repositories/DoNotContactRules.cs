using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

/// <summary>
/// Whether a person has asked not to be contacted, read the same way by every place
/// that sends: sequences, automations and the composer.
/// </summary>
/// <remarks>
/// The flag is kept on contacts. A lead has none of its own, so it used to be
/// reachable whatever its person said: ticking Do Not Contact on the contact made
/// for a lead left the lead's sequence and the "new lead" email going. A lead now
/// counts as do not contact through its person contact, and any record counts
/// through any contact in the workspace with the same address, because the "stop"
/// belongs to the person, not to one of their records. Archived contacts count:
/// archiving a record does not take a "stop" back.
/// </remarks>
internal static class DoNotContactRules
{
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>A do-not-contact contact in this workspace has this address.</summary>
    public static Task<bool> AddressAsync(AppDbContext db, Guid? organizationId, string? email, CancellationToken ct)
    {
        var address = Normalize(email);
        if (address.Length == 0) return Task.FromResult(false);
        return db.Contacts.AnyAsync(c => c.OrganizationId == organizationId && c.DoNotContact && c.Email.Trim().ToLower() == address, ct);
    }

    /// <summary>The lead's person contact is do not contact, or another contact with its address is.</summary>
    public static Task<bool> LeadAsync(AppDbContext db, Guid? organizationId, Guid leadId, Guid? personContactId, string? email, CancellationToken ct)
    {
        var address = Normalize(email);
        return db.Contacts.AnyAsync(c => c.OrganizationId == organizationId && c.DoNotContact
            && (c.Id == personContactId
                || c.ConvertedFromLeadId == leadId
                || (address != "" && c.Email.Trim().ToLower() == address)), ct);
    }
}
