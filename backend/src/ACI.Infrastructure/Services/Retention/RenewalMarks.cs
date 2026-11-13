using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;

namespace ACI.Infrastructure.Services.Retention;

/// <summary>
/// What staff decided on the renewals board, for the other places that list memberships
/// renewing soon: the morning brief and the assistant's renewals tool. A membership marked
/// "not renewing" there (the lapsed stage, set by hand on its end) is no renewal coming up, and
/// a list that said it renews on its date would contradict the board.
/// </summary>
/// <remarks>
/// A membership marked renewed needs nothing here: marking it moves the member's renewal date on.
/// </remarks>
internal static class RenewalMarks
{
    /// <summary>The members whose current membership end nobody marked "not renewing" on the renewals board.</summary>
    public static IQueryable<ClubMember> NotMarkedNotRenewing(this IQueryable<ClubMember> members, AppDbContext db) =>
        members.Where(m => !db.ClubRenewals.Any(r => r.OrganizationId == m.OrganizationId && r.MemberId == m.Id
            && r.Source == RenewalSources.Membership && r.SubscriptionId == null
            && r.EndsAtUtc == m.RenewsAtUtc && r.Stage == RenewalStages.Lapsed));
}
