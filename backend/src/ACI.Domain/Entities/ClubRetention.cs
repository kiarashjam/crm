namespace ACI.Domain.Entities;

// Keeping members and growing through them: the renewals board, an at-risk score per member
// worked out from what the club's records say, the save actions the club sets off when a
// member becomes at risk, and referrals and sponsorship (who introduced, proposed and seconded
// a new member, and the bonus the club gives for it).
//
// The club knew none of this. Renewal dates sat on the member and nothing looked at them; a
// member who stopped coming, stopped spending and stopped paying looked like every other
// member until they lapsed; and a referrer was a name typed into the application, so nobody
// could see who brings members in, and the referral bonus on the settings page was never paid.

/// <summary>Where a member's at-risk score stands.</summary>
public static class RiskLevels
{
    /// <summary>Under the watch threshold: nothing in the records worries.</summary>
    public const string Low = "low";

    /// <summary>At or over the watch threshold, under the at-risk one: worth an eye.</summary>
    public const string Watch = "watch";

    /// <summary>At or over the club's at-risk threshold: the save actions run when a member gets here.</summary>
    public const string AtRisk = "at_risk";
}

/// <summary>What a member's renewal stands at on the renewals board.</summary>
public static class RenewalStages
{
    public const string Upcoming = "upcoming";
    public const string Contacted = "contacted";
    public const string Renewed = "renewed";
    public const string AtRisk = "at_risk";
    public const string Lapsed = "lapsed";

    /// <summary>In the order the board shows them.</summary>
    public static readonly IReadOnlyList<string> All = [Upcoming, Contacted, AtRisk, Renewed, Lapsed];

    /// <summary>The stages staff can set by hand; "upcoming" is what is left when nothing else is true.</summary>
    public static readonly IReadOnlyList<string> Settable = [Contacted, Renewed, AtRisk, Lapsed];
}

/// <summary>What ends when a member comes up for renewal.</summary>
public static class RenewalSources
{
    /// <summary>The membership's term (ClubMember.RenewsAtUtc).</summary>
    public const string Membership = "membership";

    /// <summary>The current period of the member's dues subscription (ClubSubscription.NextChargeAtUtc).</summary>
    public const string Subscription = "subscription";
}

/// <summary>What a referral bonus is paid in.</summary>
public static class ReferralBonusKinds
{
    /// <summary>No bonus: the club gives none.</summary>
    public const string None = "none";

    /// <summary>Loyalty points, on the referrer's points.</summary>
    public const string Points = "points";

    /// <summary>A credit on the referrer's house account, through the ledger.</summary>
    public const string Credit = "credit";

    public static bool IsKind(string? value) => value is None or Points or Credit;
}

/// <summary>What became of a referral bonus.</summary>
public static class ReferralBonusStatuses
{
    /// <summary>Given: the points or the credit are on the referrer's account.</summary>
    public const string Awarded = "awarded";

    /// <summary>
    /// Due, but not given yet: a credit is money, and whoever admitted the member may not put
    /// money on an account. Someone who may gives it from the application or the member.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>Not given, and why (the referrer had left the club, say). Someone can give it later.</summary>
    public const string Skipped = "skipped";
}

/// <summary>Whether proposers and seconders are asked for on applications.</summary>
public static class SponsorRules
{
    /// <summary>Not asked for. Any that are named are kept, without checking them.</summary>
    public const string Off = "off";

    /// <summary>May be named; each one named must be a member in good standing.</summary>
    public const string Optional = "optional";

    /// <summary>Both must be named, each a member in good standing, before an application is approved.</summary>
    public const string Required = "required";

    public static bool IsRule(string? value) => value is Off or Optional or Required;
}

/// <summary>What a concern recorded about a member is.</summary>
public static class ConcernKinds
{
    /// <summary>The member complained about something.</summary>
    public const string Complaint = "complaint";

    /// <summary>Staff noticed something that may mean the member is unhappy (a remark, a cancelled habit).</summary>
    public const string Concern = "concern";

    public static bool IsKind(string? value) => value is Complaint or Concern;
}

/// <summary>
/// A workspace's rules for keeping members and for referrals: how much each sign counts in a
/// member's at-risk score and where "at risk" starts, what happens when a member gets there,
/// the referral bonus, and whether applications need a proposer and a seconder.
/// </summary>
/// <remarks>
/// One row per workspace, edited as a whole, so it carries a version and a save made from an
/// older copy is refused. Without a row the defaults below apply; every action that reaches a
/// member or puts something on an account starts off until the club switches it on.
/// </remarks>
public class ClubRetentionSettings : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }

    // ── How much each sign counts (0 leaves it out) ─────────────────────────
    /// <summary>Visits falling against the member's own past.</summary>
    public int VisitWeight { get; set; } = 30;
    /// <summary>Spending falling against the member's own past.</summary>
    public int SpendWeight { get; set; } = 15;
    /// <summary>Charges left unpaid past their due date, and the reminders sent for them.</summary>
    public int BalanceWeight { get; set; } = 25;
    /// <summary>Coming to fewer events than before, and not coming when booked.</summary>
    public int EventWeight { get; set; } = 10;
    /// <summary>Complaints and concerns recorded on the member and not resolved.</summary>
    public int ConcernWeight { get; set; } = 15;
    /// <summary>A short membership: members leave most in their first two years.</summary>
    public int TenureWeight { get; set; } = 5;

    /// <summary>The score (0 to 100) from which a member is at risk.</summary>
    public int AtRiskThreshold { get; set; } = 60;
    /// <summary>The score from which a member is worth watching.</summary>
    public int WatchThreshold { get; set; } = 35;

    // ── Save actions, when a member becomes at risk ─────────────────────────
    /// <summary>Make a task for the member's owner.</summary>
    public bool SaveTask { get; set; }
    /// <summary>Days the task has before it is due.</summary>
    public int SaveTaskDueDays { get; set; } = 3;
    /// <summary>Email the member through the outbox.</summary>
    public bool SaveEmail { get; set; }
    /// <summary>
    /// marketing or service (ConsentPurposes): marketing goes only to a member who agreed to it;
    /// a service email is about their membership and goes whatever they chose.
    /// </summary>
    public string SaveEmailPurpose { get; set; } = "marketing";
    /// <summary>The club's own subject; none for the standard words in the member's language.</summary>
    public string? SaveEmailSubject { get; set; }
    /// <summary>The club's own text; none for the standard words in the member's language.</summary>
    public string? SaveEmailBody { get; set; }
    /// <summary>Days after save actions ran for a member before they can run for them again.</summary>
    public int SaveCooldownDays { get; set; } = 60;

    // ── Referrals ───────────────────────────────────────────────────────────
    /// <summary><see cref="ReferralBonusKinds"/>: what the referrer gets when the member they introduced is admitted.</summary>
    public string ReferralBonus { get; set; } = ReferralBonusKinds.None;
    public int ReferralBonusPoints { get; set; }
    /// <summary>The credit, in the workspace's currency.</summary>
    public decimal ReferralBonusAmount { get; set; }

    // ── Sponsorship ─────────────────────────────────────────────────────────
    /// <summary><see cref="SponsorRules"/>.</summary>
    public string SponsorRule { get; set; } = SponsorRules.Off;
    /// <summary>Months a member must have been one before they may propose or second.</summary>
    public int SponsorMinTenureMonths { get; set; } = 12;
    /// <summary>A proposer or seconder may have nothing overdue on their account.</summary>
    public bool SponsorNoOverdue { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// A member's at-risk score as the daily run (or a recompute) last worked it out, with the
/// signs behind it, so the member's page can say why.
/// </summary>
/// <remarks>
/// One row per active member. A member who leaves (lapsed, cancelled, erased) loses the row;
/// one who comes back starts again, their first score being the baseline later changes are
/// measured from. Save actions run when a score crosses into at risk (<see cref="AtRiskSinceUtc"/>
/// set by a conditional update, so two runs at once act once) and not again within the club's
/// cooldown.
/// </remarks>
public class ClubMemberRisk : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>0 to 100: the weighted share of the signs that are showing.</summary>
    public int Score { get; set; }

    /// <summary><see cref="RiskLevels"/>.</summary>
    public string Level { get; set; } = RiskLevels.Low;

    /// <summary>The signs and what each added, as JSON: [{ key, label, weight, strength, points, detail, facts }].</summary>
    public string FactorsJson { get; set; } = "[]";

    public DateTime ComputedAtUtc { get; set; }

    /// <summary>The score before this one, when there was one.</summary>
    public int? PreviousScore { get; set; }

    /// <summary>When the member last became at risk; none while they are not.</summary>
    public DateTime? AtRiskSinceUtc { get; set; }

    /// <summary>When the save actions last ran for them.</summary>
    public DateTime? SaveActionsAtUtc { get; set; }

    /// <summary>The task and the email those save actions made, so the member's page, their export and their erasure find them.</summary>
    public Guid? SaveTaskId { get; set; }
    public Guid? SaveMessageId { get; set; }
}

/// <summary>
/// One renewal of a member's: the end of their membership's term, or of their dues
/// subscription's period, and what the club did about it.
/// </summary>
/// <remarks>
/// The renewals board works most stages out from the records (a renewal date moved on, a
/// period billed and paid, a message sent, a lapse); this row keeps what staff said by hand
/// (contacted, at risk, not renewing, renewed) and when a term was renewed from the board. The
/// daily run adds a row for each renewal coming up, so a renewal made elsewhere (the member's
/// form, the billing) still shows as renewed once the date has moved on.
/// </remarks>
public class ClubRenewal : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary><see cref="RenewalSources"/>.</summary>
    public string Source { get; set; } = RenewalSources.Membership;

    /// <summary>The subscription, for a subscription's period.</summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>When the term or period being renewed ends.</summary>
    public DateTime EndsAtUtc { get; set; }

    /// <summary>A stage set by hand (<see cref="RenewalStages.Settable"/>); none leaves the board to work it out.</summary>
    public string? Stage { get; set; }
    public DateTime? StageSetAtUtc { get; set; }
    public Guid? StageSetByUserId { get; set; }
    public string? StageSetByName { get; set; }

    /// <summary>When someone marked the member contacted about this renewal.</summary>
    public DateTime? ContactedAtUtc { get; set; }
    public Guid? ContactedByUserId { get; set; }

    /// <summary>When it was marked renewed, and the new end a membership was renewed to from the board.</summary>
    public DateTime? RenewedAtUtc { get; set; }
    public DateTime? RenewedUntilUtc { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>The bonus a referrer got, or is due, for one member they brought in: one per member brought in.</summary>
public class ClubReferralBonus : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ReferrerMemberId { get; set; }

    /// <summary>The member they introduced. A member earns their referrer one bonus, ever: the database holds to it.</summary>
    public Guid ReferredMemberId { get; set; }

    /// <summary>The application that admitted them, when one did.</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary><see cref="ReferralBonusKinds"/>: points or credit.</summary>
    public string Kind { get; set; } = ReferralBonusKinds.Points;

    /// <summary><see cref="ReferralBonusStatuses"/>.</summary>
    public string Status { get; set; } = ReferralBonusStatuses.Awarded;

    public int Points { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>The points entry, or the credit on the ledger, that paid it.</summary>
    public Guid? LoyaltyEntryId { get; set; }
    public Guid? PaymentId { get; set; }

    /// <summary>Why it is pending or was skipped.</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AwardedAtUtc { get; set; }
    public Guid? AwardedByUserId { get; set; }

    /// <summary>
    /// While one request is giving the bonus (a credit is two steps: this row, then the ledger),
    /// until when it holds it. Another request that finds the row held leaves it to that one, so
    /// two presses or an approval and a press at the same moment give it once; a hold left by a
    /// request that stopped part way runs out, and the next one finishes the job.
    /// </summary>
    public DateTime? ClaimedUntilUtc { get; set; }
}

/// <summary>A complaint or concern staff recorded about a member: an open one counts in their at-risk score.</summary>
public class ClubMemberConcern : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary><see cref="ConcernKinds"/>.</summary>
    public string Kind { get; set; } = ConcernKinds.Concern;

    public string Note { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }

    /// <summary>When it was dealt with, by whom and how: a resolved concern counts only briefly.</summary>
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public string? Resolution { get; set; }
}

/// <summary>Who started a scoring run.</summary>
public static class ScoringRunTriggers
{
    /// <summary>The daily run, once per workspace per day in its time zone.</summary>
    public const string Schedule = "schedule";

    /// <summary>Someone pressed "Work out every score now".</summary>
    public const string Manual = "manual";
}

/// <summary>
/// One run of the at-risk scoring for a workspace: what it found and did, in counts. (Not the
/// privacy settings' retention runs, which remove old personal data: this one keeps members.)
/// </summary>
public class ClubScoringRun : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary><see cref="ScoringRunTriggers"/>.</summary>
    public string Trigger { get; set; } = ScoringRunTriggers.Schedule;

    /// <summary>For the daily run, the workspace's local date: one per day, which the database holds to.</summary>
    public DateOnly? ScheduledFor { get; set; }

    /// <summary>running, succeeded or failed.</summary>
    public string Status { get; set; } = "running";

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }

    public int MembersScored { get; set; }
    public int BecameAtRisk { get; set; }
    public int NoLongerAtRisk { get; set; }
    public int TasksCreated { get; set; }
    public int EmailsQueued { get; set; }
    public int RenewalsTracked { get; set; }

    /// <summary>What stopped it, when something did.</summary>
    public string? Failure { get; set; }
}
