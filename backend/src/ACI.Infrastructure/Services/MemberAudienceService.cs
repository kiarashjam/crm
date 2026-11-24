using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Works out who an audience of club members is (see <see cref="IMemberAudience"/>), for
/// campaigns and for an event's invitations alike.
/// </summary>
/// <remarks>
/// <para>
/// The members are narrowed by every filter given, in the database where it can: tags and
/// custom field conditions go through the same query the Members list runs (RecordFlexQuery),
/// a saved view's own filters with them, and the habits are read from the club's own records,
/// a door check-in at an event (ClubEventRegistrations) and a visit (ClubVisits).
/// </para>
/// <para>
/// Then each member chosen is checked, in this order, and left out for the first reason that
/// holds: a status not chosen, erased, no address, no consent to marketing by email (for
/// marketing), an address the mail service suppressed, an address a contact marked do not
/// contact holds (for marketing), already on the event's list or invited to it (for an
/// invitation), and last another member at the same address, of whom the one who joined first
/// gets the email. The consent and the suppression list are read as the outbox reads them; the
/// outbox checks both again as it queues each email and as it sends it.
/// </para>
/// <para>
/// Reads the workspace the request or job acts for (the query filters), and says which in
/// every query besides.
/// </para>
/// </remarks>
public sealed class MemberAudienceService : IMemberAudience
{
    private readonly AppDbContext _db;
    private readonly IRecordFlexQuery _flex;
    private readonly IConsentRepository _consent;
    private readonly ICampaignRepository _addresses;

    public MemberAudienceService(AppDbContext db, IRecordFlexQuery flex, IConsentRepository consent, ICampaignRepository addresses)
    {
        _db = db;
        _flex = flex;
        _consent = consent;
        _addresses = addresses;
    }

    public async Task<Result<AudiencePlan>> PlanAsync(Guid organizationId, AudienceAsk ask, CancellationToken ct = default)
    {
        var purpose = (ask.Purpose ?? ConsentPurposes.Marketing).Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(purpose))
            return new Error("Audience.PurposeInvalid", "An audience is for marketing or for a service message.");
        var filter = MemberAudienceRules.Clean(ask.Filter);
        if (MemberAudienceRules.Problem(filter) is { } problem) return problem;

        // The view: as the person asking may see it now, or, for the scheduler, as the campaign kept it.
        AudienceViewDto? view = null;
        if (filter.ViewId is Guid viewId)
        {
            if (ask.RequesterId is Guid requester)
            {
                var seen = await ViewAsync(organizationId, requester, viewId, ct);
                if (seen.IsFailure) return seen.Error;
                view = seen.Value;
            }
            else
            {
                view = filter.View ?? await ReadViewAsync(organizationId, viewId, requesterId: null, ct);
                if (view == null) return ViewNotFound;
            }
        }

        var members = await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId).ToListAsync(ct);
        IEnumerable<ClubMember> chosen = members;
        if (filter.Tiers is { Count: > 0 } tiers)
            chosen = chosen.Where(m => tiers.Contains(m.Tier, StringComparer.OrdinalIgnoreCase));
        if (view?.Tier is { Length: > 0 } viewTier)
            chosen = chosen.Where(m => string.Equals(m.Tier, viewTier, StringComparison.OrdinalIgnoreCase));
        if (view?.Status is { Length: > 0 } viewStatus)
            chosen = chosen.Where(m => string.Equals(m.Status, viewStatus, StringComparison.OrdinalIgnoreCase));

        // Tags (any of those chosen) and field conditions, in the database, as the Members list runs them.
        var flex = new FlexFilter
        {
            Tags = filter.Tags ?? [],
            Fields = (filter.Fields ?? []).Select(FieldCondition.Parse).OfType<FieldCondition>().ToList(),
        };
        if (await _flex.MatchingAsync(RecordTypes.Member, flex, ct) is { } flexIds)
            chosen = chosen.Where(m => flexIds.Contains(m.Id));
        if (view != null)
        {
            var viewFlex = new FlexFilter
            {
                Tags = view.Tags,
                AllTags = view.AllTags,
                NotTags = view.NotTags,
                Fields = view.Fields.Select(FieldCondition.Parse).OfType<FieldCondition>().ToList(),
            };
            if (await _flex.MatchingAsync(RecordTypes.Member, viewFlex, ct) is { } viewIds)
                chosen = chosen.Where(m => viewIds.Contains(m.Id));
        }

        foreach (var habit in await HabitsAsync(organizationId, filter, DateTime.UtcNow, ct))
            chosen = chosen.Where(m => habit.Contains(m.Id));

        // The order they are written to: of two members at one address, the one who joined
        // first gets the email, greeted by name. The same answer every time, and one staff can
        // explain (CampaignService has always sent in this order).
        var list = chosen
            .OrderBy(m => m.JoinedAtUtc)
            .ThenBy(m => m.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id)
            .ToList();
        return new AudiencePlan(purpose, await ClassifyAsync(organizationId, list, filter, purpose, ask.EventId, ct), view);
    }

    public async Task<Result<AudienceViewDto>> ViewAsync(Guid organizationId, Guid userId, Guid viewId, CancellationToken ct = default)
    {
        var view = await ReadViewAsync(organizationId, viewId, userId, ct);
        return view == null ? ViewNotFound : view;
    }

    private static readonly Error ViewNotFound = new("Audience.View.NotFound",
        "That saved view of the Members list was not found, or it is a colleague's and not shared with you. Pick another one.");

    // ── who is left out, and why ───────────────────────────────────────────

    private async Task<IReadOnlyList<AudienceMember>> ClassifyAsync(
        Guid organizationId, IReadOnlyList<ClubMember> members, MemberAudienceFilter filter, string purpose, Guid? eventId, CancellationToken ct)
    {
        var marketing = purpose == ConsentPurposes.Marketing;
        var statuses = filter.Statuses ?? [];
        static string Address(ClubMember m) => (m.Email ?? "").Trim();
        bool Reachable(ClubMember m) =>
            (statuses.Count == 0 || statuses.Contains(m.Status, StringComparer.OrdinalIgnoreCase))
            && m.ErasedAtUtc == null && ValidationHelper.IsValidEmail(Address(m));

        var reachable = members.Where(Reachable).ToList();
        var agreed = marketing
            ? (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, reachable.Select(m => m.Id).ToList(), ConsentChannels.Email, ConsentPurposes.Marketing, ct))
                .Where(c => c.Granted).Select(c => c.SubjectId).ToHashSet()
            : reachable.Select(m => m.Id).ToHashSet();
        var unreachable = await _addresses.UnreachableAsync(organizationId, reachable.Where(m => agreed.Contains(m.Id)).Select(Address).ToList(), ct);

        var onList = new HashSet<Guid>();
        var invited = new HashSet<Guid>();
        if (eventId is Guid ev)
        {
            onList = (await _db.ClubEventRegistrations.AsNoTracking()
                    .Where(r => r.OrganizationId == organizationId && r.EventId == ev && r.MemberId != null
                        && (r.Status == RegistrationStatuses.Reserved || r.Status == RegistrationStatuses.CheckedIn || r.Status == RegistrationStatuses.Waitlisted))
                    .Select(r => r.MemberId!.Value)
                    .ToListAsync(ct))
                .ToHashSet();
            invited = (await _db.ClubEventInvitations.AsNoTracking()
                    .Where(i => i.OrganizationId == organizationId && i.EventId == ev)
                    .Select(i => i.MemberId)
                    .ToListAsync(ct))
                .ToHashSet();
        }

        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<AudienceMember>(members.Count);
        foreach (var member in members)
        {
            var address = Address(member);
            var key = ErasedAddresses.Normalize(address);
            var reason =
                statuses.Count > 0 && !statuses.Contains(member.Status, StringComparer.OrdinalIgnoreCase) ? AudienceReasons.Status
                : member.ErasedAtUtc != null ? AudienceReasons.Erased
                : !ValidationHelper.IsValidEmail(address) ? AudienceReasons.NoEmail
                : !agreed.Contains(member.Id) ? AudienceReasons.NoConsent
                : unreachable.Suppressed.Contains(key) ? AudienceReasons.Suppressed
                : marketing && unreachable.DoNotContact.Contains(key) ? AudienceReasons.DoNotContact
                : onList.Contains(member.Id) ? AudienceReasons.AlreadyRegistered
                : invited.Contains(member.Id) ? AudienceReasons.AlreadyInvited
                // Last, so a member who may not have it does not take their address's one email
                // away from one at the same address who may.
                : !addresses.Add(key) ? AudienceReasons.Duplicate
                : null;
            result.Add(new AudienceMember(member, address.Length == 0 ? null : address, reason));
        }
        return result;
    }

    // ── habits ─────────────────────────────────────────────────────────────

    /// <summary>For each habit the filter asks for, the members who have it.</summary>
    private async Task<List<HashSet<Guid>>> HabitsAsync(Guid organizationId, MemberAudienceFilter filter, DateTime now, CancellationToken ct)
    {
        var habits = new List<HashSet<Guid>>();
        if (filter.AttendedEventIds is { Count: > 0 } events)
        {
            // An event that is not the club's, or no longer there, was attended by nobody: it
            // narrows to nobody rather than being left out, which would widen an invitation to everyone.
            habits.Add((await _db.ClubEventRegistrations.AsNoTracking()
                    .Where(r => r.OrganizationId == organizationId && events.Contains(r.EventId) && r.MemberId != null
                        && r.Status == RegistrationStatuses.CheckedIn)
                    .Select(r => r.MemberId!.Value)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet());
        }
        if (filter.MinEvents is int least && filter.EventMonths is int months)
        {
            var since = now.AddMonths(-months);
            var came = await (from r in _db.ClubEventRegistrations.AsNoTracking()
                              join e in _db.ClubEvents.AsNoTracking() on r.EventId equals e.Id
                              where r.OrganizationId == organizationId && r.MemberId != null && r.Status == RegistrationStatuses.CheckedIn
                                  && e.StartAtUtc >= since && e.StartAtUtc <= now
                              select new { MemberId = r.MemberId!.Value, r.EventId })
                .Distinct()
                .ToListAsync(ct);
            habits.Add(came.GroupBy(c => c.MemberId).Where(g => g.Count() >= least).Select(g => g.Key).ToHashSet());
        }
        if (filter.MinVisits is int visits && filter.VisitDays is int days)
        {
            var since = now.AddDays(-days);
            habits.Add((await _db.ClubVisits.AsNoTracking()
                    .Where(v => v.OrganizationId == organizationId && v.VisitedAtUtc >= since && v.VisitedAtUtc <= now)
                    .GroupBy(v => v.MemberId)
                    .Select(g => new { MemberId = g.Key, Count = g.Count() })
                    .Where(g => g.Count >= visits)
                    .Select(g => g.MemberId)
                    .ToListAsync(ct))
                .ToHashSet());
        }
        if (filter.NotVisitedDays is int away)
        {
            var since = now.AddDays(-away);
            var visited = (await _db.ClubVisits.AsNoTracking()
                    .Where(v => v.OrganizationId == organizationId && v.VisitedAtUtc >= since)
                    .Select(v => v.MemberId)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();
            var all = await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId).Select(m => m.Id).ToListAsync(ct);
            habits.Add(all.Where(id => !visited.Contains(id)).ToHashSet());
        }
        return habits;
    }

    // ── saved views ────────────────────────────────────────────────────────

    /// <summary>
    /// A saved view of the Members list, read from the list's parameters it keeps: tier, status,
    /// tag (any of, or all with tagMatch=all), notTag and field. With a requester, only one they
    /// may open: their own, or one shared with the workspace.
    /// </summary>
    private async Task<AudienceViewDto?> ReadViewAsync(Guid organizationId, Guid viewId, Guid? requesterId, CancellationToken ct)
    {
        var view = await _db.SavedViews.AsNoTracking()
            .Where(v => v.Id == viewId && v.OrganizationId == organizationId && v.List == SavedViewLists.Members
                && (requesterId == null || v.OwnerUserId == requesterId || v.Shared))
            .Select(v => new { v.Id, v.Name, v.QueryJson })
            .FirstOrDefaultAsync(ct);
        if (view == null) return null;
        var query = Parameters(view.QueryJson);
        IReadOnlyList<string> All(string name) => query.TryGetValue(name, out var values) ? values : [];
        string? One(string name) => All(name).FirstOrDefault(v => v.Length > 0 && !string.Equals(v, "All", StringComparison.OrdinalIgnoreCase));
        static IReadOnlyList<Guid> Ids(IEnumerable<string> values) =>
            values.Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().Take(MemberAudienceRules.MaxListEntries).ToList();
        var tags = Ids(All("tag"));
        return new AudienceViewDto(
            view.Id, view.Name, One("tier"), One("status"), tags,
            tags.Count > 1 && string.Equals(One("tagMatch"), "all", StringComparison.OrdinalIgnoreCase),
            Ids(All("notTag")),
            All("field").Where(f => FieldCondition.Parse(f) != null).Take(MemberAudienceRules.MaxListEntries).ToList());
    }

    /// <summary>A view's parameters: each name with its text, or its list of texts.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Parameters(string json)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => [property.Value.GetString()!.Trim()],
                    JsonValueKind.Array => property.Value.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!.Trim()).ToList(),
                    _ => [],
                };
            }
        }
        catch (JsonException)
        {
            // A view whose filters cannot be read chooses by none of them, as the list opens it.
        }
        return result;
    }
}
