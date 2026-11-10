using ACI.Application.Common;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi;

/// <summary>
/// The keys duplicates are matched on (Contact.PhoneKey, NameKey) for contacts saved before
/// the columns existed.
/// </summary>
/// <remarks>
/// Every save writes them (AppDbContext), so only older rows lack them, and a row lacks them
/// exactly when its NameKey is null: a name too short to match on is written as an empty key,
/// not left null. That makes this safe to run on every start, rather than once: it reads
/// the rows still to do, which after the first start are none, or the few a server still on
/// the old code saved during a deploy. The keys are worked out here because T-SQL cannot
/// strip accents or keep only the digits the way the app does.
/// </remarks>
public static class ContactMatchKeys
{
    private const int Batch = 500;

    /// <returns>How many contacts were given their keys.</returns>
    public static async Task<int> FillMissingAsync(AppDbContext db, CancellationToken ct = default)
    {
        var filled = 0;
        while (true)
        {
            // Every workspace's contacts: this runs at start-up, acting for none.
            var rows = await db.Contacts.IgnoreQueryFilters().Where(c => c.NameKey == null).OrderBy(c => c.Id).Take(Batch).ToListAsync(ct);
            if (rows.Count == 0) return filled;
            foreach (var contact in rows)
            {
                contact.PhoneKey = MatchKeys.Phone(contact.Phone);
                contact.NameKey = MatchKeys.Name(contact.Name) ?? "";
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            filled += rows.Count;
        }
    }
}
