using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The renewals board: members whose membership's term, or their dues subscription's period,
/// ends within a window, each in a stage (upcoming, contacted, at risk, renewed, lapsed), with
/// their owner, what the renewal is worth and when someone last spoke to them.
/// </summary>
/// <remarks>
/// <para>
/// A member's renewal is the earlier of their membership's end (ClubMember.RenewsAtUtc) and the
/// end of the current period of a dues subscription billed less often than monthly; a monthly
/// period ends every month, which would put every monthly member on the board all the time, so
/// those join only when asked (<see cref="RenewalBoardQuery.Monthly"/>).
/// </para>
/// <para>
/// Stages are worked out from the records where they can be, and staff can set one by hand:
/// <list type="bullet">
/// <item><b>Lapsed</b>: the member is Lapsed or Cancelled; or staff said they are not renewing; or
/// the membership ended and nobody renewed it.</item>
/// <item><b>Renewed</b>: the membership's end has moved on (renewed from the board, which moves it
/// on a year, or on the member's form); the subscription's next period is billed and paid; or
/// staff said so.</item>
/// <item><b>At risk</b>: staff said so, or the member's at-risk score says so.</item>
/// <item><b>Contacted</b>: staff said so, or someone wrote to them or logged a call, meeting or
/// email on their contact in the 90 days before the end.</item>
/// <item><b>Upcoming</b>: nothing of the above.</item>
/// </list>
/// A member leaving or renewing outranks what staff set by hand; what staff set outranks what the
/// score and the last contact suggest.
/// </para>
/// </remarks>
public interface IClubRenewals
{
    Task<Result<RenewalBoardDto>> BoardAsync(Guid organizationId, Guid userId, RenewalBoardQuery query, CancellationToken ct = default);

    /// <summary>
    /// Puts a renewal in a stage by hand, or back to automatic. Renewed, for a membership, moves
    /// the member's renewal date on (to <see cref="RenewalStageRequest.RenewUntilUtc"/>, else a
    /// year after the end), once: a second press finds it moved already.
    /// </summary>
    Task<Result<RenewalCardDto>> SetStageAsync(Guid organizationId, Guid userId, RenewalStageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Notes each renewal coming up in the next 90 days that has no record yet, so a renewal made
    /// elsewhere (the member's form, the billing) still shows as renewed once its date has moved
    /// on. The daily scoring run calls it; it answers how many it noted.
    /// </summary>
    Task<int> TrackUpcomingAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct = default);
}

/// <param name="Days">How far ahead the board looks, 1 to 365 days; 60 when left out.</param>
/// <param name="PastDays">How far back, for renewals just made or missed, 0 to 365 days; 30 when left out.</param>
/// <param name="Owner">"me" for the signed-in person's members, a user id for theirs, else everyone's.</param>
/// <param name="Monthly">Count the end of each monthly dues period as a renewal too.</param>
public sealed record RenewalBoardQuery(int? Days = null, int? PastDays = null, string? Owner = null, bool? Monthly = null);

/// <summary>The board: its window and its columns, in the order the board shows them.</summary>
public sealed record RenewalBoardDto(
    DateTime FromUtc, DateTime ToUtc, int Days, int PastDays, string Currency, int AtRiskThreshold,
    IReadOnlyList<RenewalColumnDto> Columns);

/// <summary>One stage: how many, what they are worth a year per currency, and the cards, soonest first.</summary>
public sealed record RenewalColumnDto(string Stage, string Label, int Count, IReadOnlyList<ClubCurrencyAmountDto> Totals, IReadOnlyList<RenewalCardDto> Cards);

/// <summary>A member's renewal on the board.</summary>
public sealed record RenewalCardDto
{
    public Guid MemberId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Tier { get; init; } = string.Empty;
    public string MemberStatus { get; init; } = string.Empty;

    /// <summary>membership or subscription.</summary>
    public string Source { get; init; } = "membership";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? SubscriptionId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SubscriptionName { get; init; }

    /// <summary>When the term or period ends; negative days left once it has.</summary>
    public DateTime EndsAtUtc { get; init; }
    public int DaysLeft { get; init; }

    public string Stage { get; init; } = "upcoming";

    /// <summary>Why it is in its stage, in a few words: "Marked contacted by Anna", "Score 72", "Renewal date moved on".</summary>
    public string StageReason { get; init; } = string.Empty;

    /// <summary>Whether staff set the stage by hand.</summary>
    public bool StageSetByHand { get; init; }

    public Guid OwnerUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OwnerName { get; init; }

    /// <summary>What the renewal brings in a year, and per period.</summary>
    public decimal AnnualValue { get; init; }
    public decimal PeriodAmount { get; init; }
    public string Cadence { get; init; } = "Monthly";
    public string Currency { get; init; } = "CHF";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastContactAtUtc { get; init; }
    /// <summary>message, activity or marked: what the last contact was.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LastContactKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? RiskScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RiskLevel { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Note { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? RenewedUntilUtc { get; init; }

    /// <summary>The renewal record's version, to send back with a change; none before anything was recorded.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? Version { get; init; }
}

/// <summary>A stage set by hand on one renewal.</summary>
public sealed class RenewalStageRequest
{
    public Guid MemberId { get; set; }

    /// <summary>membership or subscription.</summary>
    public string? Source { get; set; }
    public Guid? SubscriptionId { get; set; }

    /// <summary>The end the renewal is about, as the board gave it.</summary>
    public DateTime EndsAtUtc { get; set; }

    /// <summary>contacted, renewed, at_risk, lapsed, or auto to let the board decide again.</summary>
    public string? Stage { get; set; }

    public string? Note { get; set; }

    /// <summary>For a membership marked renewed: its new end; a year after the old one when left out.</summary>
    public DateTime? RenewUntilUtc { get; set; }

    public Guid? Version { get; set; }
}
