namespace ACI.Domain.Entities;

// The assistant: what a workspace's calls to the language model came to each month, the
// workspace's own settings for it, and when each person's morning brief went out.

/// <summary>
/// What one workspace's calls to the language model came to in one calendar month (UTC): the
/// count its monthly limit holds it to, and the tokens they used. One row per workspace and
/// month; for someone using the AI writer outside any organization, one per person and month.
/// </summary>
/// <remarks>
/// Every call to the model is billed on the server's key: the assistant's questions, its
/// morning briefs and the AI writer's drafts all count here. A call is taken from the month's
/// allowance before it is made, by one conditional UPDATE, so two at the same moment cannot
/// both take the last one (AiBudget).
/// </remarks>
public class AiUsage : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary>Whose personal workspace the row counts, outside any organization; null on an organization's row.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The month, as its first day.</summary>
    public DateOnly Month { get; set; }

    /// <summary>Calls made to the model this month: what the monthly limit is held against.</summary>
    public int Calls { get; set; }

    /// <summary>Calls refused because the month's limit was reached.</summary>
    public int Refused { get; set; }

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// A workspace's settings for the assistant: its own monthly limit on calls to the model, and
/// whether its people get a morning brief. One row per workspace; a workspace that saved
/// nothing has the server's default limit and morning briefs on.
/// </summary>
public class AssistantSettings : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Calls to the model a month, below the server's ceiling (Assistant:MaxMonthlyLimit); null
    /// for the server's default (Assistant:DefaultMonthlyLimit). 0 turns the model off here.
    /// </summary>
    public int? MonthlyLimit { get; set; }

    /// <summary>Whether its people get a morning brief each day (each can still turn theirs off).</summary>
    public bool MorningBriefs { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// One person's morning brief in one workspace on one day: taken by inserting this row (one per
/// workspace, person and day, held by a unique index), so two servers never brief anyone twice.
/// The brief's words are in the notification and the email it became, not here.
/// </summary>
public class AssistantBrief : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The staff member it is for.</summary>
    public Guid UserId { get; set; }

    /// <summary>The day it is for, on the workspace's calendar.</summary>
    public DateOnly Day { get; set; }

    /// <summary><see cref="AssistantBriefStatuses"/>.</summary>
    public string Status { get; set; } = AssistantBriefStatuses.Preparing;

    /// <summary>How many things it listed.</summary>
    public int Items { get; set; }

    /// <summary>Whether the language model wrote its summary; otherwise it is the plain list alone.</summary>
    public bool WrittenByModel { get; set; }

    /// <summary>Whether a copy was queued in the outbox for the person's inbox.</summary>
    public bool Emailed { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}

/// <summary>Where a morning brief got to.</summary>
public static class AssistantBriefStatuses
{
    /// <summary>Taken for the day and being put together.</summary>
    public const string Preparing = "preparing";
    /// <summary>In the person's notifications (and their inbox, when they asked for that).</summary>
    public const string Sent = "sent";
    /// <summary>Nothing to tell them today, so nothing was sent.</summary>
    public const string Nothing = "nothing";
}
