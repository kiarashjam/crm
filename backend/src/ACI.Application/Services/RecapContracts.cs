using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The workspace's recap emails: a daily, weekly and monthly email of the sales funnel (leads by
/// source and referral, invitations to book a meeting, meetings, contracts, deposits, conversions,
/// the pipeline by phase) and of the club, each figure beside the period before's, for the people
/// who see every deal.
/// </summary>
/// <remarks>
/// <para>
/// <b>Who</b>: the workspace's people who see all deals (deals.all), read-only ones included.
/// Those who manage the settings choose who gets which frequencies; each person switches their own
/// on and off. Someone who loses deals.all or leaves the workspace receives none.
/// </para>
/// <para>
/// <b>When</b>, in the workspace's time zone: daily from 07:00 for the day before, weekly on Monday
/// from 07:00 for Monday to Sunday just ended, monthly on the 1st from 07:00 for the month just
/// ended (<see cref="RecapPeriods"/>). Once per person, workspace, frequency and period, taken by
/// inserting a row the database holds unique; a period missed while the app was down goes at the
/// next pass, and only the latest of each frequency.
/// </para>
/// <para>
/// <b>How</b>: through the outbox, from the workspace's sender, in the recipient's language, with
/// an HTML part and a plain-text part, read as the recipient. A workspace with Sales off leaves the
/// sales parts out; the club's part is there when the Club module is on and the club has activity;
/// a period with nothing to report sends nothing.
/// </para>
/// </remarks>
public interface IRecapEmails
{
    /// <summary>The workspace's people who may receive recaps, each with their frequencies: the settings section.</summary>
    Task<RecapRecipientsDto> RecipientsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Sets one person's frequencies, a field left out kept as it is. Switching one on is refused
    /// for someone who does not see every deal (Recap.NotEligible) or is not in the workspace.
    /// </summary>
    Task<Result<RecapRecipientDto>> SetRecipientAsync(Guid organizationId, Guid changedByUserId, Guid userId, RecapFrequenciesRequest request, CancellationToken ct = default);

    /// <summary>The caller's own frequencies in the workspace, and whether they may receive recaps.</summary>
    Task<MyRecapsDto> MineAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>Sets the caller's own frequencies, a field left out kept as it is.</summary>
    Task<Result<MyRecapsDto>> SetMineAsync(Guid organizationId, Guid userId, RecapFrequenciesRequest request, CancellationToken ct = default);

    /// <summary>A period's figures beside the period before's, read as the person.</summary>
    Task<RecapFigures> FiguresAsync(Guid organizationId, Guid userId, RecapPeriod period, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Sends every recap that is due. Returns how many were queued (a period with nothing to report sends none).</summary>
    Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>One person who may receive recaps, and which they get.</summary>
/// <param name="Preset">The preset their permissions start from (read_only, manager...).</param>
public sealed record RecapRecipientDto(Guid UserId, string Name, string Email, string Preset, bool Daily, bool Weekly, bool Monthly);

/// <param name="TimeZone">The workspace's time zone, which the recaps' 07:00 is in.</param>
/// <param name="People">Everyone who sees every deal, the owner first, then by name.</param>
/// <param name="NotEligible">How many others are in the workspace: they do not see every deal, so they get none.</param>
public sealed record RecapRecipientsDto(string TimeZone, IReadOnlyList<RecapRecipientDto> People, int NotEligible);

/// <param name="Eligible">Whether they see every deal here, without which they receive no recap.</param>
public sealed record MyRecapsDto(bool Eligible, bool Daily, bool Weekly, bool Monthly, string TimeZone);

/// <summary>Frequencies to switch on (true) or off (false); one left out is kept as it is.</summary>
public sealed class RecapFrequenciesRequest
{
    public bool? Daily { get; set; }
    public bool? Weekly { get; set; }
    public bool? Monthly { get; set; }
}

/// <summary>A figure for the period, beside the period before's.</summary>
public sealed record RecapCount(int Now, int Before)
{
    public static readonly RecapCount Zero = new(0, 0);
}

/// <summary>A named figure (a lead source, a referrer) for the period, beside the period before's.</summary>
public sealed record RecapNamedCount(string Name, int Now, int Before);

/// <summary>Money in one currency for the period, beside the period before's; never converted.</summary>
public sealed record RecapAmount(string Currency, decimal Now, decimal Before);

/// <summary>The sales funnel's figures for a period.</summary>
/// <param name="Sources">The top sources of the period's new leads, by name ("" for none given).</param>
/// <param name="OtherSources">The new leads from every other source.</param>
/// <param name="Referred">New leads that came through a referral: someone recorded as referring them, or the source "Referral".</param>
/// <param name="Referrers">Who referred the period's new leads, the top five, by name.</param>
/// <param name="Lost">Leads moved to a lost or not-interested status in the period.</param>
/// <param name="Invitations">"Invite to a meeting" emails that went out in the period.</param>
/// <param name="MeetingsBooked">Meetings booked on the website in the period, on the day they were booked.</param>
/// <param name="MeetingsHeld">Meetings that took place in the period and were not cancelled.</param>
/// <param name="MeetingsCancelled">Meetings cancelled in the period.</param>
/// <param name="ContractsSigned">Signed in CadenceE in the period, or whose signed copy was uploaded in it.</param>
/// <param name="DepositAmounts">What the deposits paid came to, per currency.</param>
/// <param name="Converted">Leads converted in the period.</param>
/// <param name="Won">Deals won in the period.</param>
/// <param name="WonValue">What they were worth, per currency.</param>
/// <param name="WonWithoutValue">Deals won in the period whose value could not be read: counted, not added in.</param>
/// <param name="PipelineNow">Open leads at each of the five phases now (outreach first).</param>
public sealed record RecapSalesFigures(
    RecapCount NewLeads,
    IReadOnlyList<RecapNamedCount> Sources,
    RecapCount OtherSources,
    RecapCount Referred,
    IReadOnlyList<RecapNamedCount> Referrers,
    RecapCount Lost,
    RecapCount Invitations,
    RecapCount MeetingsBooked,
    RecapCount MeetingsHeld,
    RecapCount MeetingsCancelled,
    RecapCount ContractsSent,
    RecapCount ContractsSigned,
    RecapCount Deposits,
    IReadOnlyList<RecapAmount> DepositAmounts,
    RecapCount Converted,
    RecapCount Won,
    IReadOnlyList<RecapAmount> WonValue,
    int WonWithoutValue,
    IReadOnlyList<int> PipelineNow)
{
    /// <summary>Whether anything happened in the period: the pipeline as it stands is not news on its own.</summary>
    public bool HasNews =>
        new[] { NewLeads, Referred, Lost, Invitations, MeetingsBooked, MeetingsHeld, MeetingsCancelled, ContractsSent, ContractsSigned, Deposits, Converted, Won }
            .Any(c => c.Now > 0);
}

/// <summary>The club's figures for a period.</summary>
/// <param name="Events">Events held in the period (published and not cancelled).</param>
/// <param name="Attendance">Who was checked in at them.</param>
/// <param name="AtRiskNow">Active members whose at-risk score is at risk now.</param>
/// <param name="RenewalsDue">Active members whose membership renews in the next 30 days, less those marked not renewing.</param>
public sealed record RecapClubFigures(RecapCount Visits, RecapCount Events, RecapCount Attendance, int AtRiskNow, int RenewalsDue)
{
    /// <summary>Whether the club did anything in the period.</summary>
    public bool HasNews => Visits.Now > 0 || Events.Now > 0;

    /// <summary>Whether there is club life to show at all: activity in either period, or members at risk or renewing.</summary>
    public bool HasActivity => HasNews || Visits.Before > 0 || Events.Before > 0 || AtRiskNow > 0 || RenewalsDue > 0;
}

/// <summary>Everything a recap says, for one period beside the one before.</summary>
/// <param name="Sales">Null when the workspace has the Sales module off.</param>
/// <param name="NewMembers">Club members who joined in the period; null when the Club module is off.</param>
/// <param name="Club">Null when the Club module is off.</param>
public sealed record RecapFigures(RecapPeriod Period, RecapPeriod Previous, RecapSalesFigures? Sales, RecapCount? NewMembers, RecapClubFigures? Club)
{
    /// <summary>Whether there is anything to report; nothing is sent for a period without.</summary>
    public bool HasNews => (Sales?.HasNews ?? false) || (Club?.HasNews ?? false) || (NewMembers?.Now ?? 0) > 0;
}
