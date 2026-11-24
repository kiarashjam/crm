using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Which club members a campaign or an event's invitations are for: their tiers and statuses,
/// their tags and custom fields (the conditions the Members list offers), a saved Members view
/// to start from, and their habits at the club. Every filter given must hold; one left out
/// chooses everyone.
/// </summary>
/// <remarks>
/// Interests, hobbies and the like are the club's own member custom fields (a multiselect
/// "Interests": wine, art, golf) and tags, so a filter on them is a field condition such as
/// "interests:any:Wine|Art", written as the Members list writes it (FlexFilter).
/// A campaign keeps its audience as this, in JSON; one saved before these filters existed holds
/// only tiers and statuses, and chooses exactly the members it always did.
/// </remarks>
public sealed record MemberAudienceFilter
{
    /// <summary>Only members on these tiers; none: every tier.</summary>
    public IReadOnlyList<string>? Tiers { get; init; }

    /// <summary>Only members in these statuses; none: every status. Members in others are counted as left out.</summary>
    public IReadOnlyList<string>? Statuses { get; init; }

    /// <summary>Only members carrying any of these tags.</summary>
    public IReadOnlyList<Guid>? Tags { get; init; }

    /// <summary>Conditions on the members' custom fields, each "key:op[:value]" as the Members list writes them, all of which must hold.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>A saved view of the Members list to start from: its tier, status, tags and fields apply as well.</summary>
    public Guid? ViewId { get; init; }

    /// <summary>
    /// What the view chose when the campaign was saved, filled in by the server: a campaign sent
    /// later, by the scheduler, keeps to what the person who saved it saw. Sent by a browser, it
    /// is not read; the view is looked up again, as the person asking may see it.
    /// </summary>
    public AudienceViewDto? View { get; init; }

    /// <summary>Only members who came to any of these events (checked in at the door).</summary>
    public IReadOnlyList<Guid>? AttendedEventIds { get; init; }

    /// <summary>With <see cref="EventMonths"/>: only members who came to at least this many events in those months.</summary>
    public int? MinEvents { get; init; }

    public int? EventMonths { get; init; }

    /// <summary>With <see cref="VisitDays"/>: only members who visited the club at least this many times in those days.</summary>
    public int? MinVisits { get; init; }

    public int? VisitDays { get; init; }

    /// <summary>Only members who have not visited the club in this many days.</summary>
    public int? NotVisitedDays { get; init; }
}

/// <summary>A saved Members view as an audience applies it: its own filters, read from the list's parameters it keeps.</summary>
/// <param name="AllTags">Whether a member needs every one of <paramref name="Tags"/> rather than any.</param>
public sealed record AudienceViewDto(
    Guid Id, string Name, string? Tier, string? Status, IReadOnlyList<Guid> Tags, bool AllTags, IReadOnlyList<Guid> NotTags, IReadOnlyList<string> Fields);

/// <summary>Why a member the filters chose does not get the email. The order is the order they are checked in.</summary>
public static class AudienceReasons
{
    /// <summary>Their status is not one of those chosen (the campaign's or the invitation's statuses).</summary>
    public const string Status = "status";

    /// <summary>Erased at their request or by the retention settings: treated as gone.</summary>
    public const string Erased = "erased";

    /// <summary>No usable email address on their record.</summary>
    public const string NoEmail = "no_email";

    /// <summary>Marketing (campaigns and invitations) only: they have not agreed to it by email, or withdrew.</summary>
    public const string NoConsent = "no_consent";

    /// <summary>The mail service reported their address: unsubscribed, bounced, complained, or erased.</summary>
    public const string Suppressed = "suppressed";

    /// <summary>Marketing only: a contact with their address is marked do not contact.</summary>
    public const string DoNotContact = "do_not_contact";

    /// <summary>An event's invitation only: already on its list (a seat, the waitlist, or checked in).</summary>
    public const string AlreadyRegistered = "already_registered";

    /// <summary>An event's invitation only: invited to it already.</summary>
    public const string AlreadyInvited = "already_invited";

    /// <summary>Another member chosen has the same address and gets the one email.</summary>
    public const string Duplicate = "duplicate";
}

/// <summary>One member the filters chose: the address they are written to, and why they are left out (null when they get it).</summary>
public sealed record AudienceMember(ClubMember Member, string? Email, string? Reason);

/// <summary>
/// Who an audience is now: every member its filters chose, in the order they are written to
/// (who joined first, then by name), each with the reason they are left out when they are.
/// </summary>
public sealed record AudiencePlan(string Purpose, IReadOnlyList<AudienceMember> Chosen, AudienceViewDto? View)
{
    /// <summary>Those who get the email: one per address.</summary>
    public IReadOnlyList<AudienceMember> Recipients => Chosen.Where(c => c.Reason == null).ToList();

    /// <summary>
    /// The members the filters choose with their statuses: everyone but those left out for their
    /// status. What a campaign records a message for, sent or not, as it always did.
    /// </summary>
    public IReadOnlyList<AudienceMember> Matching => Chosen.Where(c => c.Reason != AudienceReasons.Status).ToList();
}

/// <summary>What to work an audience out for.</summary>
/// <param name="Purpose">marketing (campaigns and invitations) or service (<see cref="ConsentPurposes"/>).</param>
/// <param name="RequesterId">
/// Who is asking: a saved view is looked up as they may see it, and refused when they may not.
/// Null for the campaign scheduler, which keeps to the view as the campaign saved it.
/// </param>
/// <param name="EventId">An event's invitations: members on its list, or invited to it already, are left out.</param>
public sealed record AudienceAsk(MemberAudienceFilter Filter, string Purpose, Guid? RequesterId, Guid? EventId = null);

/// <summary>A member in an audience, or left out of it with why.</summary>
/// <param name="Reason">For one left out: one of <see cref="AudienceReasons"/>.</param>
/// <param name="Language">The language they are written to in (en, de, fr).</param>
public sealed record AudiencePersonDto(Guid MemberId, string Name, string? Email, string Tier, string Status, string? Reason, string Language);

/// <summary>
/// Who an audience would reach now, counted by the server the same way for the counts, the
/// preview and the send. The left-out counts add up: <see cref="Chosen"/> less all of them is
/// <see cref="WillReceive"/>.
/// </summary>
/// <param name="Chosen">Members the filters chose, their statuses aside.</param>
/// <param name="Matching">Of those, the ones whose status is chosen too (erased ones included, as before).</param>
/// <param name="WithAddress">Of those, the ones not erased and with a usable address.</param>
/// <param name="Agreed">Of those, the ones who agreed to marketing by email; for a service message, all of them.</param>
/// <param name="WillReceive">Emails that would go: one per member, one per address.</param>
/// <param name="Recipients">The first of those who get it, for the list's preview.</param>
/// <param name="LeftOut">The first of those left out, with why.</param>
/// <param name="View">The saved view applied, when the audience starts from one.</param>
public sealed record MemberAudienceDto(
    string Purpose, int Chosen, int Matching, int WithAddress, int Agreed, int WillReceive,
    int Suppressed, int DoNotContact, int StatusExcluded, int Erased, int NoEmail, int NoConsent,
    int AlreadyRegistered, int AlreadyInvited, int Duplicates,
    IReadOnlyList<AudiencePersonDto> Recipients, IReadOnlyList<AudiencePersonDto> LeftOut,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AudienceViewDto? View)
{
    /// <summary>People shown in each of the preview's lists.</summary>
    public const int SampleSize = 8;

    /// <summary>The figures of a plan, with the first few people of each list.</summary>
    public static MemberAudienceDto Of(AudiencePlan plan)
    {
        int Count(string reason) => plan.Chosen.Count(c => c.Reason == reason);
        var matching = plan.Matching;
        var withAddress = matching.Count(c => c.Member.ErasedAtUtc == null && ValidationHelper.IsValidEmail((c.Member.Email ?? "").Trim()));
        return new MemberAudienceDto(
            plan.Purpose, plan.Chosen.Count, matching.Count, withAddress, withAddress - Count(AudienceReasons.NoConsent),
            plan.Chosen.Count(c => c.Reason == null),
            Count(AudienceReasons.Suppressed), Count(AudienceReasons.DoNotContact), Count(AudienceReasons.Status), Count(AudienceReasons.Erased),
            Count(AudienceReasons.NoEmail), Count(AudienceReasons.NoConsent), Count(AudienceReasons.AlreadyRegistered),
            Count(AudienceReasons.AlreadyInvited), Count(AudienceReasons.Duplicate),
            plan.Chosen.Where(c => c.Reason == null).Take(SampleSize).Select(Person).ToList(),
            plan.Chosen.Where(c => c.Reason != null).Take(SampleSize).Select(Person).ToList(),
            plan.View);
    }

    private static AudiencePersonDto Person(AudienceMember m) =>
        new(m.Member.Id, $"{m.Member.FirstName} {m.Member.LastName}".Trim(), m.Email, m.Member.Tier, m.Member.Status, m.Reason,
            Common.I18n.AppLanguages.ForMember(m.Member.PreferredLanguage));
}

/// <summary>
/// Works out who an audience is: the one place campaigns and event invitations ask, so the
/// counts staff see, the preview and the send never disagree.
/// </summary>
public interface IMemberAudience
{
    /// <summary>
    /// Every member the filters choose, each with why they are left out when they are. Refused
    /// when a filter cannot be read, or names a saved view the requester may not see.
    /// </summary>
    Task<Result<AudiencePlan>> PlanAsync(Guid organizationId, AudienceAsk ask, CancellationToken ct = default);

    /// <summary>
    /// A saved Members view as an audience applies it, when the person may open it (their own,
    /// or one shared with the workspace); refused otherwise, as if there were none.
    /// </summary>
    Task<Result<AudienceViewDto>> ViewAsync(Guid organizationId, Guid userId, Guid viewId, CancellationToken ct = default);
}

/// <summary>The audience's own rules: what a filter may hold, and how one is kept tidy.</summary>
public static class MemberAudienceRules
{
    public const int MaxListEntries = 20;
    public const int MaxEvents = 50;

    /// <summary>The filter as kept: lists trimmed, without blanks or repeats, within their limits; habit figures in range.</summary>
    public static MemberAudienceFilter Clean(MemberAudienceFilter? filter)
    {
        static IReadOnlyList<string> Words(IReadOnlyList<string>? values) => (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().Length > 32 ? v.Trim()[..32] : v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxListEntries)
            .ToList();
        static IReadOnlyList<Guid>? Ids(IReadOnlyList<Guid>? values, int max)
        {
            var ids = (values ?? []).Where(v => v != Guid.Empty).Distinct().Take(max).ToList();
            return ids.Count == 0 ? null : ids;
        }
        var fields = (filter?.Fields ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f) && f.Length <= 500 && FieldCondition.Parse(f) != null)
            .Select(f => f.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxListEntries)
            .ToList();
        var minEvents = filter?.MinEvents is > 0 ? filter.MinEvents : null;
        var minVisits = filter?.MinVisits is > 0 ? filter.MinVisits : null;
        return new MemberAudienceFilter
        {
            Tiers = Words(filter?.Tiers),
            Statuses = Words(filter?.Statuses),
            Tags = Ids(filter?.Tags, MaxListEntries),
            Fields = fields.Count == 0 ? null : fields,
            ViewId = filter?.ViewId is Guid v && v != Guid.Empty ? v : null,
            View = filter?.ViewId is Guid w && w != Guid.Empty && filter.View?.Id == w ? filter.View : null,
            AttendedEventIds = Ids(filter?.AttendedEventIds, MaxEvents),
            MinEvents = minEvents,
            EventMonths = minEvents == null ? null : filter!.EventMonths,
            MinVisits = minVisits,
            VisitDays = minVisits == null ? null : filter!.VisitDays,
            NotVisitedDays = filter?.NotVisitedDays is > 0 ? filter.NotVisitedDays : null,
        };
    }

    /// <summary>What is wrong with a filter's habits, in a sentence; null when they read.</summary>
    public static Error? Problem(MemberAudienceFilter filter)
    {
        if (filter.MinEvents is int n && (n > 100 || filter.EventMonths is not (>= 1 and <= 60)))
            return new Error("Audience.Filters", "\"Came to at least … events\" takes a number of events up to 100, in the last 1 to 60 months.");
        if (filter.MinVisits is int v && (v > 1000 || filter.VisitDays is not (>= 1 and <= 730)))
            return new Error("Audience.Filters", "\"Visited at least … times\" takes a number of visits up to 1,000, in the last 1 to 730 days.");
        if (filter.NotVisitedDays is > 730)
            return new Error("Audience.Filters", "\"Has not visited in the last … days\" takes 1 to 730 days.");
        return null;
    }

    /// <summary>Whether a filter narrows anything beyond tiers and statuses.</summary>
    public static bool UsesMore(MemberAudienceFilter filter) =>
        filter.Tags is { Count: > 0 } || filter.Fields is { Count: > 0 } || filter.ViewId != null
        || filter.AttendedEventIds is { Count: > 0 } || filter.MinEvents != null || filter.MinVisits != null || filter.NotVisitedDays != null;
}
