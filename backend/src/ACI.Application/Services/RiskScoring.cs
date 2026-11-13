using System.Globalization;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>What the club's records say about one member, for their at-risk score.</summary>
/// <param name="NowUtc">When the score is worked out.</param>
/// <param name="MemberSinceUtc">When their membership began (their start, else the day they joined).</param>
/// <param name="VisitsRecent">Check-ins in the last <see cref="RiskScoring.RecentDays"/> days.</param>
/// <param name="VisitsBefore">Check-ins in the <see cref="RiskScoring.BaselineDays"/> days before those.</param>
/// <param name="SpendRecent">What they spent (charges other than dues, voids taken off) in the recent window, in <paramref name="Currency"/>.</param>
/// <param name="SpendBefore">The same over the baseline window.</param>
/// <param name="OverdueAmount">What they owe past its due date, after credit they hold, in <paramref name="Currency"/>.</param>
/// <param name="OldestOverdueDays">Days the oldest of those charges is past due.</param>
/// <param name="RemindersRecent">Payment reminders sent to them in the recent window.</param>
/// <param name="EventsRecent">Events they came to (checked in) in the last <see cref="RiskScoring.EventRecentDays"/> days.</param>
/// <param name="EventsBefore">Events they came to in the same length of time before that.</param>
/// <param name="NoShowsRecent">Events they had a seat at and did not come to, in the recent event window.</param>
/// <param name="OpenComplaints">Complaints recorded about them in the last year and not resolved.</param>
/// <param name="OpenConcerns">Concerns recorded about them in the last year and not resolved.</param>
/// <param name="RecentlyResolved">Complaints or concerns resolved in the recent window.</param>
public sealed record MemberSignals(
    DateTime NowUtc,
    DateTime MemberSinceUtc,
    int VisitsRecent,
    int VisitsBefore,
    decimal SpendRecent,
    decimal SpendBefore,
    string Currency,
    decimal OverdueAmount,
    int OldestOverdueDays,
    int RemindersRecent,
    int EventsRecent,
    int EventsBefore,
    int NoShowsRecent,
    int OpenComplaints,
    int OpenConcerns,
    int RecentlyResolved);

/// <summary>How much each sign counts, and where "watch" and "at risk" start.</summary>
public sealed record RiskWeights(int Visits, int Spend, int Balance, int Events, int Concerns, int Tenure, int AtRiskThreshold, int WatchThreshold)
{
    public static readonly RiskWeights Default = new(30, 15, 25, 10, 15, 5, 60, 35);

    public int Total => Visits + Spend + Balance + Events + Concerns + Tenure;
}

/// <summary>One sign behind a score: how strongly it shows (0 to 1), what it added (points of 100), and in words.</summary>
public sealed record RiskFactor(string Key, string Label, int Weight, decimal Strength, int Points, string Detail);

/// <summary>A score, its level and the signs behind it.</summary>
public sealed record RiskResult(int Score, string Level, IReadOnlyList<RiskFactor> Factors);

/// <summary>
/// A member's at-risk score: each sign the club's records hold (visits and spending against
/// the member's own past, what they owe past due, events, complaints, a short membership) is
/// given a strength from 0 to 1, and the score is the weighted share of the signs showing, out
/// of 100. Every factor says in words what it saw, so the score is never a number nobody can
/// explain.
/// </summary>
/// <remarks>
/// <para>
/// Visits, spending and events are measured against the member's own past, not the club's
/// average: a member who comes twice a month and still does is fine, one who came every week
/// and now comes twice a month is not. A member too new to have a past (under a month of it)
/// shows no fall, rather than a fall from nothing.
/// </para>
/// <para>
/// Pure arithmetic on what it is given, so the rule is tested on its own and the same for the
/// daily run, a recompute and the member's page.
/// </para>
/// </remarks>
public static class RiskScoring
{
    /// <summary>The recent window visits and spending are read over.</summary>
    public const int RecentDays = 90;

    /// <summary>The past they are compared with: the days before the recent window.</summary>
    public const int BaselineDays = 270;

    /// <summary>The recent window for events, which happen less often; the past compared is as long.</summary>
    public const int EventRecentDays = 180;

    /// <summary>Under this much past to compare with, a fall is not measured.</summary>
    private const int MinimumBaselineDays = 30;

    /// <summary>
    /// A member at risk stays so until their score falls this far under the threshold, so a
    /// score that moves a point either side of it does not set the save actions off each time.
    /// </summary>
    public const int RecoveryMargin = 5;

    public const string Visits = "visits";
    public const string Spend = "spend";
    public const string Balance = "balance";
    public const string Events = "events";
    public const string Concerns = "concerns";
    public const string Tenure = "tenure";

    /// <param name="money">Writes an amount in the workspace's way ("CHF 450.00").</param>
    /// <param name="wasAtRisk">Whether the member was at risk before this score: they stay so down to <see cref="RecoveryMargin"/> under the threshold.</param>
    public static RiskResult Score(MemberSignals s, RiskWeights w, Func<decimal, string, string> money, bool wasAtRisk = false)
    {
        var factors = new List<(string Key, string Label, int Weight, decimal Strength, string Detail)>
        {
            VisitsFactor(s, w.Visits),
            SpendFactor(s, w.Spend, money),
            BalanceFactor(s, w.Balance, money),
            EventsFactor(s, w.Events),
            ConcernsFactor(s, w.Concerns),
            TenureFactor(s, w.Tenure),
        };
        var total = w.Total;
        var scored = factors.Select(f => new RiskFactor(f.Key, f.Label, f.Weight, Math.Round(f.Strength, 2),
                total <= 0 ? 0 : (int)Math.Round(100m * f.Weight * f.Strength / total, MidpointRounding.AwayFromZero), f.Detail))
            .ToList();
        var exact = total <= 0 ? 0m : factors.Sum(f => 100m * f.Weight * f.Strength / total);
        var score = (int)Math.Clamp(Math.Round(exact, MidpointRounding.AwayFromZero), 0, 100);
        return new RiskResult(score, LevelOf(score, w, wasAtRisk), scored);
    }

    /// <summary>The level of a score: at risk from the threshold (down to the margin for someone who was), watch from its own.</summary>
    public static string LevelOf(int score, RiskWeights w, bool wasAtRisk = false)
    {
        if (score >= w.AtRiskThreshold || (wasAtRisk && score >= w.AtRiskThreshold - RecoveryMargin)) return RiskLevels.AtRisk;
        return score >= w.WatchThreshold ? RiskLevels.Watch : RiskLevels.Low;
    }

    // ── the signs ────────────────────────────────────────────────────────────

    /// <summary>The days of the member's past a window before the recent one covers: up to <paramref name="length"/>, from when they joined.</summary>
    private static int PastDays(MemberSignals s, int recent, int length)
    {
        var windowStart = s.NowUtc.AddDays(-recent);
        var days = (int)Math.Floor((windowStart - s.MemberSinceUtc).TotalDays);
        return Math.Clamp(days, 0, length);
    }

    private static (string, string, int, decimal, string) VisitsFactor(MemberSignals s, int weight)
    {
        const string label = "Visits";
        var past = PastDays(s, RecentDays, BaselineDays);
        if (past < MinimumBaselineDays)
            return (Visits, label, weight, 0, $"{Plural(s.VisitsRecent, "visit")} in the last {RecentDays} days; too new a member to compare with their past.");
        var usual = s.VisitsBefore * (decimal)RecentDays / past;
        if (usual < 1)
            return (Visits, label, weight, 0, $"{Plural(s.VisitsRecent, "visit")} in the last {RecentDays} days; they rarely came before either, so there is no fall.");
        var strength = Clamp(1 - s.VisitsRecent / usual);
        return (Visits, label, weight, strength,
            $"{Plural(s.VisitsRecent, "visit")} in the last {RecentDays} days, against about {Number(usual)} in a usual {RecentDays} days before.");
    }

    private static (string, string, int, decimal, string) SpendFactor(MemberSignals s, int weight, Func<decimal, string, string> money)
    {
        const string label = "Spending";
        var past = PastDays(s, RecentDays, BaselineDays);
        if (past < MinimumBaselineDays)
            return (Spend, label, weight, 0, $"Spent {money(s.SpendRecent, s.Currency)} in the last {RecentDays} days; too new a member to compare with their past.");
        var usual = s.SpendBefore * RecentDays / past;
        if (usual < 1)
            return (Spend, label, weight, 0, $"Spent {money(s.SpendRecent, s.Currency)} in the last {RecentDays} days; they spent little before either, so there is no fall.");
        var strength = Clamp(1 - Math.Max(0, s.SpendRecent) / usual);
        return (Spend, label, weight, strength,
            $"Spent {money(Math.Max(0, s.SpendRecent), s.Currency)} in the last {RecentDays} days, against about {money(Math.Round(usual, 2), s.Currency)} in a usual {RecentDays} days before.");
    }

    private static (string, string, int, decimal, string) BalanceFactor(MemberSignals s, int weight, Func<decimal, string, string> money)
    {
        const string label = "Unpaid balance";
        if (s.OverdueAmount <= 0)
            return (Balance, label, weight, 0, s.RemindersRecent > 0
                ? $"Nothing overdue now ({Plural(s.RemindersRecent, "reminder")} sent in the last {RecentDays} days, since paid)."
                : "Nothing overdue.");
        // Anything overdue counts a little; the longer it has been and the more reminders it
        // took, the more: two months past due, or three reminders, is the whole of the sign.
        var strength = Math.Max(0.2m, Math.Max(Clamp(s.OldestOverdueDays / 60m), Clamp(s.RemindersRecent / 3m)));
        var reminders = s.RemindersRecent > 0 ? $"; {Plural(s.RemindersRecent, "reminder")} sent in the last {RecentDays} days" : "";
        return (Balance, label, weight, strength,
            $"{money(s.OverdueAmount, s.Currency)} overdue, the oldest {Plural(s.OldestOverdueDays, "day")} past its due date{reminders}.");
    }

    private static (string, string, int, decimal, string) EventsFactor(MemberSignals s, int weight)
    {
        const string label = "Events";
        var past = PastDays(s, EventRecentDays, EventRecentDays);
        var noShows = s.NoShowsRecent > 0 ? $"; {Plural(s.NoShowsRecent, "no-show")}" : "";
        var noShowStrength = Clamp(s.NoShowsRecent / 3m);
        if (past < MinimumBaselineDays * 2)
            return (Events, label, weight, noShowStrength,
                $"Came to {Plural(s.EventsRecent, "event")} in the last 6 months{noShows}; too new a member to compare with their past.");
        var usual = s.EventsBefore * (decimal)EventRecentDays / past;
        if (usual < 1)
            return (Events, label, weight, noShowStrength,
                $"Came to {Plural(s.EventsRecent, "event")} in the last 6 months{noShows}; they rarely came to events before either.");
        var strength = Math.Max(Clamp(1 - s.EventsRecent / usual), noShowStrength);
        return (Events, label, weight, strength,
            $"Came to {Plural(s.EventsRecent, "event")} in the last 6 months, against about {Number(usual)} in the 6 months before{noShows}.");
    }

    private static (string, string, int, decimal, string) ConcernsFactor(MemberSignals s, int weight)
    {
        const string label = "Complaints and concerns";
        // An open complaint is half the sign and two are all of it; a concern counts half as
        // much, and one resolved in the last months a little, as the member may still feel it.
        var strength = Clamp((s.OpenComplaints + 0.5m * s.OpenConcerns + 0.25m * s.RecentlyResolved) / 2m);
        if (s.OpenComplaints == 0 && s.OpenConcerns == 0)
            return (Concerns, label, weight, strength, s.RecentlyResolved > 0
                ? $"None open; {Plural(s.RecentlyResolved, "complaint or concern", "complaints or concerns")} resolved in the last {RecentDays} days."
                : "No open complaints or concerns.");
        var parts = new List<string>();
        if (s.OpenComplaints > 0) parts.Add(Plural(s.OpenComplaints, "open complaint"));
        if (s.OpenConcerns > 0) parts.Add(Plural(s.OpenConcerns, "open concern"));
        return (Concerns, label, weight, strength, $"{string.Join(" and ", parts)}.");
    }

    private static (string, string, int, decimal, string) TenureFactor(MemberSignals s, int weight)
    {
        const string label = "Length of membership";
        var months = Math.Max(0, (int)Math.Floor((s.NowUtc - s.MemberSinceUtc).TotalDays / 30.44));
        // Members leave most in their first two years, and most of all at the first renewal.
        var strength = Clamp(1 - months / 24m);
        var length = months < 24 ? Plural(months, "month") : Plural(months / 12, "year");
        return (Tenure, label, weight, strength, strength > 0
            ? $"A member for {length}: members leave most in their first two years."
            : $"A member for {length}.");
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);

    private static string Number(decimal value) =>
        Math.Round(value, value < 10 ? 1 : 0, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Plural(int count, string one, string? many = null) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many ?? one + "s")}";
}
