using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Keeping members: each member's at-risk score (worked out on the server from what the club's
/// records hold, explained, recomputed every day), what the club does when a member becomes at
/// risk, the concerns staff record about members, and the club's rules for all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The score</b> (<see cref="RiskScoring"/>) weighs visits and spending against the member's
/// own past, what they owe past due and the reminders it took, events they came to or missed,
/// open complaints and concerns, and how long they have been a member. The weights and the
/// thresholds are the club's settings.
/// </para>
/// <para>
/// <b>Save actions</b> run when a member's score crosses into at risk, once per crossing and not
/// again within the club's cooldown: a task for the member's owner, an email through the outbox
/// (each off until the club switches it on), and the automation trigger "When a member becomes
/// at risk" for the rules the club makes. A member's first score is the baseline: it sets off
/// nothing, so switching scoring on does not act on everyone at once.
/// </para>
/// <para>
/// <b>The daily run</b> (<see cref="RunScheduledAsync"/>) scores every active member of each
/// workspace whose club is on, once per local day, claimed with a row the database takes once.
/// It also notes each renewal coming up, so the renewals board knows a renewal made elsewhere.
/// </para>
/// </remarks>
public interface IClubRetention
{
    Task<RetentionSettingsDto> SettingsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Saves the rules; <paramref name="expectedVersion"/> (body or If-Match) refuses a save from an older copy (409).</summary>
    Task<Result<RetentionSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, RetentionSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>The member's score as last worked out, with its signs; none before the first.</summary>
    Task<Result<MemberRiskDto?>> MemberRiskAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Works the member's score out now. A crossing into at risk sets the save actions off, as the daily run would.</summary>
    Task<Result<MemberRiskDto>> RecomputeAsync(Guid organizationId, Guid memberId, Guid userId, CancellationToken ct = default);

    /// <summary>Members at a level (at_risk by default, or watch), highest score first.</summary>
    Task<IReadOnlyList<AtRiskMemberDto>> AtRiskAsync(Guid organizationId, string? level, CancellationToken ct = default);

    /// <summary>Scores every active member now (settings.manage): after the weights change, say.</summary>
    Task<Result<ScoringRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<ScoringRunDto>> RunsAsync(Guid organizationId, int take = 10, CancellationToken ct = default);

    /// <summary>The daily run, for every workspace due one: answers how many it ran.</summary>
    Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default);

    // ── concerns ─────────────────────────────────────────────────────────────

    Task<Result<IReadOnlyList<ConcernDto>>> ConcernsAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);
    Task<Result<ConcernDto>> AddConcernAsync(Guid organizationId, Guid memberId, Guid userId, ConcernRequest request, CancellationToken ct = default);
    Task<Result<ConcernDto>> ResolveConcernAsync(Guid organizationId, Guid memberId, Guid concernId, Guid userId, ResolveConcernRequest request, CancellationToken ct = default);
    Task<Result> DeleteConcernAsync(Guid organizationId, Guid memberId, Guid concernId, CancellationToken ct = default);
}

/// <summary>The club's rules for keeping members and for referrals, as the settings page edits them.</summary>
public sealed record RetentionSettingsDto
{
    public int VisitWeight { get; init; }
    public int SpendWeight { get; init; }
    public int BalanceWeight { get; init; }
    public int EventWeight { get; init; }
    public int ConcernWeight { get; init; }
    public int TenureWeight { get; init; }
    public int AtRiskThreshold { get; init; }
    public int WatchThreshold { get; init; }

    public bool SaveTask { get; init; }
    public int SaveTaskDueDays { get; init; }
    public bool SaveEmail { get; init; }
    public string SaveEmailPurpose { get; init; } = "marketing";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SaveEmailSubject { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SaveEmailBody { get; init; }
    public int SaveCooldownDays { get; init; }

    public string ReferralBonus { get; init; } = "none";
    public int ReferralBonusPoints { get; init; }
    public decimal ReferralBonusAmount { get; init; }

    /// <summary>The workspace's currency, which a referral credit is in.</summary>
    public string Currency { get; init; } = "CHF";

    public string SponsorRule { get; init; } = "off";
    public int SponsorMinTenureMonths { get; init; }
    public bool SponsorNoOverdue { get; init; }

    /// <summary>The version to send back with a change; none before the first save.</summary>
    public Guid? Version { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string? UpdatedByName { get; init; }
}

/// <summary>The whole of the rules, as saved: a field left out takes its default.</summary>
public sealed class RetentionSettingsRequest
{
    public int? VisitWeight { get; set; }
    public int? SpendWeight { get; set; }
    public int? BalanceWeight { get; set; }
    public int? EventWeight { get; set; }
    public int? ConcernWeight { get; set; }
    public int? TenureWeight { get; set; }
    public int? AtRiskThreshold { get; set; }
    public int? WatchThreshold { get; set; }
    public bool? SaveTask { get; set; }
    public int? SaveTaskDueDays { get; set; }
    public bool? SaveEmail { get; set; }
    public string? SaveEmailPurpose { get; set; }
    public string? SaveEmailSubject { get; set; }
    public string? SaveEmailBody { get; set; }
    public int? SaveCooldownDays { get; set; }
    public string? ReferralBonus { get; set; }
    public int? ReferralBonusPoints { get; set; }
    public decimal? ReferralBonusAmount { get; set; }
    public string? SponsorRule { get; set; }
    public int? SponsorMinTenureMonths { get; set; }
    public bool? SponsorNoOverdue { get; set; }
    public Guid? Version { get; set; }
}

/// <summary>A member's at-risk score and why.</summary>
public sealed record MemberRiskDto
{
    public Guid MemberId { get; init; }
    public int Score { get; init; }
    public string Level { get; init; } = "low";
    public IReadOnlyList<RiskFactor> Factors { get; init; } = Array.Empty<RiskFactor>();
    public DateTime ComputedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreviousScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? AtRiskSinceUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? SaveActionsAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? SaveTaskId { get; init; }
    public int AtRiskThreshold { get; init; }
    public int WatchThreshold { get; init; }

    /// <summary>What this recompute set off, in sentences for staff; empty for a read.</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

/// <summary>A member on the at-risk list.</summary>
public sealed record AtRiskMemberDto(
    Guid MemberId, string Name, string Tier, string Status, int Score, string Level, DateTime ComputedAtUtc, DateTime? AtRiskSinceUtc,
    Guid OwnerUserId, string? OwnerName, IReadOnlyList<RiskFactor> TopFactors);

/// <summary>A scoring run, in counts.</summary>
public sealed record ScoringRunDto(
    Guid Id, string Trigger, string Status, DateTime StartedAtUtc, DateTime? FinishedAtUtc, int MembersScored, int BecameAtRisk,
    int NoLongerAtRisk, int TasksCreated, int EmailsQueued, int RenewalsTracked, string? Failure);

/// <summary>A complaint or concern recorded about a member.</summary>
public sealed record ConcernDto(
    Guid Id, Guid MemberId, string Kind, string Note, DateTime RecordedAtUtc, string? RecordedByName,
    DateTime? ResolvedAtUtc, string? ResolvedByName, string? Resolution);

public sealed class ConcernRequest
{
    /// <summary>complaint or concern.</summary>
    public string? Kind { get; set; }
    public string? Note { get; set; }
}

public sealed class ResolveConcernRequest
{
    /// <summary>How it was dealt with, in a line.</summary>
    public string? Resolution { get; set; }
}
