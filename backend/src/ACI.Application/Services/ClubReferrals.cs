using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Referrals and sponsorship: the member who introduced a new member, the proposer and seconder
/// of an application (members in good standing, by the club's rule), the referral bonus the club
/// gives once the new member is admitted, and the report of who brings members and revenue in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Good standing</b>, by the club's rule (ClubRetentionSettings): an active member, a member
/// for at least the months the club asks, and, when the club asks, with nothing overdue on
/// their account. With the sponsor rule on, a proposer or seconder who is not in good standing
/// is refused when they are named; with it required, an application is approved only with both.
/// </para>
/// <para>
/// <b>The bonus</b> is points or a credit through the ledger, by the club's setting, given once
/// per member brought in (a unique row per referred member holds to it, as the ledger's key
/// holds to one credit). A credit is money: when whoever admitted the member may not put money
/// on an account, it waits as pending for someone who may.
/// </para>
/// </remarks>
public interface IClubReferrals
{
    /// <summary>Whether each member is in good standing to propose or second, and if not why.</summary>
    Task<IReadOnlyList<SponsorStandingDto>> StandingAsync(Guid organizationId, IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default);

    /// <summary>
    /// Checks the referrer, proposer and seconder an application names: members of this club,
    /// not the applicant, two different sponsors, each in good standing when the club's rule asks.
    /// </summary>
    Task<Result> CheckApplicationAsync(Guid organizationId, Guid? applicantMemberId, Guid? referrerId, Guid? proposerId, Guid? seconderId,
        bool referrerChanged, bool proposerChanged, bool seconderChanged, CancellationToken ct = default);

    /// <summary>At approval: with the sponsor rule required, both sponsors, each still in good standing.</summary>
    Task<Result> CheckApprovalAsync(Guid organizationId, ClubApplication application, CancellationToken ct = default);

    /// <summary>
    /// After an approval admitted the applicant: the bonus for their referrer, once. Answers a
    /// sentence for the reviewer, or null when there is no bonus to speak of.
    /// </summary>
    Task<string?> RewardReferrerAsync(Guid organizationId, Guid applicationId, Guid userId, bool mayHandleMoney, CancellationToken ct = default);

    /// <summary>Gives the bonus for a member brought in, when it was missed or is pending: once, ever.</summary>
    Task<Result<ReferralBonusDto>> AwardAsync(Guid organizationId, Guid referredMemberId, Guid userId, CancellationToken ct = default);

    /// <summary>Who brought the member in, who proposed and seconded them, and whom they brought in, proposed and seconded.</summary>
    Task<Result<MemberReferralsDto>> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Sets (or with null takes off) the member who referred a member, for members who came without an application naming one.</summary>
    Task<Result<MemberReferralsDto>> SetReferrerAsync(Guid organizationId, Guid memberId, Guid userId, Guid? referrerId, CancellationToken ct = default);

    /// <summary>Which members bring members in, and what those members bring: their dues and spending in the period.</summary>
    Task<Result<ReferralReportDto>> ReportAsync(Guid organizationId, ReferralReportQuery query, CancellationToken ct = default);
}

/// <summary>Whether a member may propose or second, by the club's rule.</summary>
public sealed record SponsorStandingDto(Guid MemberId, string Name, bool InGoodStanding, IReadOnlyList<string> Reasons);

/// <summary>A referral bonus.</summary>
public sealed record ReferralBonusDto(
    Guid Id, Guid ReferrerMemberId, string ReferrerName, Guid ReferredMemberId, string ReferredName, Guid? ApplicationId,
    string Kind, string Status, int Points, decimal Amount, string Currency, string? Reason, DateTime CreatedAtUtc, DateTime? AwardedAtUtc);

/// <summary>A member as the referrals card names them.</summary>
public sealed record ReferralPersonDto(Guid MemberId, string Name, string Status, string Tier, DateTime JoinedAtUtc);

/// <summary>What a member's page shows of referrals and sponsorship.</summary>
public sealed record MemberReferralsDto
{
    public Guid MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReferralPersonDto? ReferredBy { get; init; }

    /// <summary>The application that admitted them, and who proposed and seconded it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ApplicationId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReferralPersonDto? ProposedBy { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReferralPersonDto? SecondedBy { get; init; }

    /// <summary>The members they brought in, and the applications they proposed and seconded.</summary>
    public IReadOnlyList<ReferralPersonDto> Referred { get; init; } = Array.Empty<ReferralPersonDto>();
    public IReadOnlyList<SponsoredApplicationDto> Proposed { get; init; } = Array.Empty<SponsoredApplicationDto>();
    public IReadOnlyList<SponsoredApplicationDto> Seconded { get; init; } = Array.Empty<SponsoredApplicationDto>();

    /// <summary>Bonuses they earned, and the one their own admission earned their referrer.</summary>
    public IReadOnlyList<ReferralBonusDto> BonusesEarned { get; init; } = Array.Empty<ReferralBonusDto>();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ReferralBonusDto? BonusForThem { get; init; }

    /// <summary>Whether a bonus can still be given for this member (they have a referrer, the club gives one, none was given).</summary>
    public bool BonusAvailable { get; init; }

    /// <summary>Whether they may propose or second now, by the club's rule.</summary>
    public SponsorStandingDto? Standing { get; init; }
}

/// <summary>An application a member proposed or seconded.</summary>
public sealed record SponsoredApplicationDto(Guid ApplicationId, string Name, string Status, DateTime SubmittedAtUtc, Guid? MemberId);

/// <summary>A report period; left out, the last twelve months.</summary>
public sealed record ReferralReportQuery(DateTime? FromUtc, DateTime? ToUtc);

/// <summary>Who brings members in, and what those members bring.</summary>
public sealed record ReferralReportDto(
    DateTime FromUtc, DateTime ToUtc, string Currency, IReadOnlyList<ReferrerRowDto> Referrers, ReferralTotalsDto Totals);

/// <summary>One member who brought others in, or proposed or seconded them.</summary>
public sealed record ReferrerRowDto
{
    public Guid MemberId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Tier { get; init; } = string.Empty;

    /// <summary>Members they brought in, ever, and in the period (by the day they joined).</summary>
    public int ReferredAllTime { get; init; }
    public int ReferredInPeriod { get; init; }

    /// <summary>Approved applications they proposed and seconded in the period.</summary>
    public int Proposed { get; init; }
    public int Seconded { get; init; }

    /// <summary>What the members they brought in were charged in the period: dues, everything else, and both; per currency.</summary>
    public IReadOnlyList<ClubCurrencyAmountDto> Dues { get; init; } = Array.Empty<ClubCurrencyAmountDto>();
    public IReadOnlyList<ClubCurrencyAmountDto> Spend { get; init; } = Array.Empty<ClubCurrencyAmountDto>();
    public IReadOnlyList<ClubCurrencyAmountDto> Revenue { get; init; } = Array.Empty<ClubCurrencyAmountDto>();

    /// <summary>The bonuses they were given: points, and credit per currency.</summary>
    public int BonusPoints { get; init; }
    public IReadOnlyList<ClubCurrencyAmountDto> BonusCredit { get; init; } = Array.Empty<ClubCurrencyAmountDto>();

    /// <summary>The members they brought in, with what each brought in the period.</summary>
    public IReadOnlyList<ReferredMemberRowDto> Members { get; init; } = Array.Empty<ReferredMemberRowDto>();
}

/// <summary>A member someone brought in, as the report lists them.</summary>
public sealed record ReferredMemberRowDto(
    Guid MemberId, string Name, string Status, string Tier, DateTime JoinedAtUtc,
    IReadOnlyList<ClubCurrencyAmountDto> Dues, IReadOnlyList<ClubCurrencyAmountDto> Spend);

/// <summary>The report's totals.</summary>
public sealed record ReferralTotalsDto(
    int Referrers, int ReferredAllTime, int ReferredInPeriod, int MembersJoinedInPeriod,
    IReadOnlyList<ClubCurrencyAmountDto> Dues, IReadOnlyList<ClubCurrencyAmountDto> Spend, IReadOnlyList<ClubCurrencyAmountDto> Revenue,
    int BonusPoints, IReadOnlyList<ClubCurrencyAmountDto> BonusCredit);
