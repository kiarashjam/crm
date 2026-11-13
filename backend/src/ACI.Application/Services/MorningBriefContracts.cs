namespace ACI.Application.Services;

/// <summary>
/// The morning brief: each person's day in one workspace, built on the server from the records
/// as that person may see them. Their tasks due, their deals that went quiet, and as their role
/// allows the club's day: members past due on their account, today's arrivals and events, and
/// memberships about to renew.
/// </summary>
/// <remarks>
/// <para>
/// A daily job sends it from 07:00 on the workspace's calendar as a notification, and by email
/// through the outbox to those who asked for that, once per person, workspace and day. When a
/// language model is set up (and at least half the workspace's monthly AI allowance is left, the
/// rest being kept for people asking) the model writes a few sentences on top of the list; the
/// list is always there, so every fact can be checked.
/// </para>
/// <para>
/// The same brief, worked out at that moment, is what the brief page and the assistant's
/// my_brief tool show.
/// </para>
/// </remarks>
public interface IMorningBrief
{
    /// <summary>The person's day in the workspace as it stands at <paramref name="nowUtc"/>.</summary>
    Task<MorningBriefDto> BuildAsync(Guid organizationId, Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>The brief now, and the one sent this morning (its words as they went out), if any.</summary>
    Task<MorningBriefView> TodayAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>Sends every brief that is due. Returns how many went (a day with nothing to say sends none).</summary>
    Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>The brief as plain text: a first line that sums it up, then each part as a list.</summary>
    string PlainText(MorningBriefDto brief, string? summary = null);
}

/// <param name="Day">The day, on the workspace's calendar, yyyy-MM-dd.</param>
/// <param name="Headline">One line that sums it up: "2 tasks due, 3 arrivals today".</param>
/// <param name="Items">How many things it lists, all parts together.</param>
public sealed record MorningBriefDto(
    string Day,
    string TimeZone,
    string WorkspaceName,
    string Headline,
    IReadOnlyList<MorningBriefSection> Sections,
    int Items);

/// <summary>One part of the brief.</summary>
/// <param name="Key">tasks, stale_deals, owing, arrivals, events or renewals.</param>
/// <param name="Count">How many there are in all; <see cref="Lines"/> holds the first few.</param>
public sealed record MorningBriefSection(string Key, string Title, int Count, IReadOnlyList<MorningBriefLine> Lines);

/// <param name="Link">The page to open for it, e.g. "/tasks/{id}".</param>
public sealed record MorningBriefLine(string Text, string? Link);

/// <param name="Now">The brief worked out now.</param>
/// <param name="ThisMorning">The one that went out today, as it was written; null when none did.</param>
public sealed record MorningBriefView(MorningBriefDto Now, SentMorningBrief? ThisMorning);

public sealed record SentMorningBrief(DateTime SentAtUtc, string Text, bool WrittenByModel, bool Emailed);
