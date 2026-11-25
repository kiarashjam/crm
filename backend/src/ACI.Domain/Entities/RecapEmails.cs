namespace ACI.Domain.Entities;

/// <summary>
/// Who receives a workspace's recap emails, and how often: a daily, weekly and monthly email
/// of the sales funnel and the club's figures, each beside the period before. One row per
/// workspace and person; a frequency is on while its "since" is set.
/// </summary>
/// <remarks>
/// <para>
/// Those who manage the workspace's settings choose who gets which; each person switches their
/// own on and off in their notification settings. Only people who see every deal (deals.all)
/// receive one: a recap is the whole team's pipeline. Someone who loses that permission, or
/// leaves the workspace, receives none, whatever this row says.
/// </para>
/// <para>
/// A frequency is kept as when it was switched on rather than as a flag, so the first recap is
/// the first period that falls due after that moment: someone set up on the 20th gets the
/// month's recap on the 1st, not last month's three weeks late.
/// </para>
/// </remarks>
public class RecapSubscription : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The staff member it is for.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the daily recap was switched on for them; null while it is off.</summary>
    public DateTime? DailySinceUtc { get; set; }

    /// <summary>When the weekly recap was switched on for them; null while it is off.</summary>
    public DateTime? WeeklySinceUtc { get; set; }

    /// <summary>When the monthly recap was switched on for them; null while it is off.</summary>
    public DateTime? MonthlySinceUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Who last changed it: the person themselves, or someone managing the settings.</summary>
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// One person's recap for one frequency and period in one workspace: taken by inserting this row
/// (one per workspace, person, frequency and period, held by a unique index), so two servers, or
/// a pass run again, never send it twice. The words are in the outbox message it became.
/// </summary>
public class RecapSend : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The staff member it is for.</summary>
    public Guid UserId { get; set; }

    /// <summary><see cref="RecapFrequencies"/>.</summary>
    public string Frequency { get; set; } = RecapFrequencies.Daily;

    /// <summary>The period's first day, on the workspace's calendar: the day, the Monday, or the 1st.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary><see cref="RecapSendStatuses"/>.</summary>
    public string Status { get; set; } = RecapSendStatuses.Preparing;

    /// <summary>The email in the outbox, once it was queued.</summary>
    public Guid? OutboundMessageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When it was queued in the outbox (or found to have nothing to say).</summary>
    public DateTime? SentAtUtc { get; set; }
}

/// <summary>How often a recap comes.</summary>
public static class RecapFrequencies
{
    /// <summary>Every day from 07:00, for the day before.</summary>
    public const string Daily = "daily";

    /// <summary>Every Monday from 07:00, for Monday to Sunday just ended.</summary>
    public const string Weekly = "weekly";

    /// <summary>Every 1st of the month from 07:00, for the month just ended.</summary>
    public const string Monthly = "monthly";

    public static readonly IReadOnlyList<string> All = [Daily, Weekly, Monthly];
}

/// <summary>Where a recap got to.</summary>
public static class RecapSendStatuses
{
    /// <summary>Taken for the period and being put together.</summary>
    public const string Preparing = "preparing";

    /// <summary>Queued in the outbox, which sends it.</summary>
    public const string Sent = "sent";

    /// <summary>Nothing happened in the period to report, so nothing was sent.</summary>
    public const string Nothing = "nothing";

    /// <summary>The outbox would not take it (the person's address is on the do-not-send list, say). Not tried again.</summary>
    public const string Refused = "refused";
}
