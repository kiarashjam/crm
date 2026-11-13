using System.Globalization;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// A member's at-risk score: each sign the records hold, against the member's own past, weighed
/// by the club's settings, explained factor by factor.
/// </summary>
public class RiskScoringTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    private static string Money(decimal amount, string currency) => $"{currency} {amount.ToString("0.00", CultureInfo.InvariantCulture)}";

    /// <summary>A member of three years who came weekly, spent steadily and owes nothing: nothing shows.</summary>
    private static MemberSignals Steady() => new(
        Now, Now.AddYears(-3),
        VisitsRecent: 13, VisitsBefore: 39,
        SpendRecent: 900m, SpendBefore: 2700m, Currency: "CHF",
        OverdueAmount: 0m, OldestOverdueDays: 0, RemindersRecent: 0,
        EventsRecent: 3, EventsBefore: 3, NoShowsRecent: 0,
        OpenComplaints: 0, OpenConcerns: 0, RecentlyResolved: 0);

    [Fact]
    public void AMemberWhoKeepsTheirHabits_ScoresNothing()
    {
        var result = RiskScoring.Score(Steady(), RiskWeights.Default, Money);

        result.Score.Should().Be(0);
        result.Level.Should().Be(RiskLevels.Low);
        result.Factors.Select(f => f.Key).Should().Equal(RiskScoring.Visits, RiskScoring.Spend, RiskScoring.Balance, RiskScoring.Events,
            RiskScoring.Concerns, RiskScoring.Tenure);
        result.Factors.Should().OnlyContain(f => f.Points == 0);
    }

    [Fact]
    public void VisitsAreMeasuredAgainstTheMembersOwnPast_NotAgainstOtherMembers()
    {
        // Twice a month and always so: no fall. Weekly before and twice a month now: a fall of half.
        var always = Steady() with { VisitsRecent = 6, VisitsBefore = 18 };
        var dropped = Steady() with { VisitsRecent = 6, VisitsBefore = 36 };

        var steady = RiskScoring.Score(always, RiskWeights.Default, Money).Factors.Single(f => f.Key == RiskScoring.Visits);
        var fallen = RiskScoring.Score(dropped, RiskWeights.Default, Money).Factors.Single(f => f.Key == RiskScoring.Visits);

        steady.Strength.Should().Be(0m);
        fallen.Strength.Should().Be(0.5m);
        fallen.Points.Should().Be(15, "half of the visits weight of 30, out of a total weight of 100");
        fallen.Detail.Should().Be("6 visits in the last 90 days, against about 12 in a usual 90 days before.");
    }

    [Fact]
    public void ANewMember_ShowsNoFall_ButTheirShortMembershipCounts()
    {
        var signals = Steady() with { MemberSinceUtc = Now.AddDays(-40), VisitsRecent = 0, VisitsBefore = 0, SpendRecent = 0, SpendBefore = 0, EventsRecent = 0, EventsBefore = 0 };

        var result = RiskScoring.Score(signals, RiskWeights.Default, Money);

        result.Factors.Single(f => f.Key == RiskScoring.Visits).Detail.Should().Contain("too new a member");
        result.Factors.Single(f => f.Key == RiskScoring.Visits).Strength.Should().Be(0);
        var tenure = result.Factors.Single(f => f.Key == RiskScoring.Tenure);
        tenure.Strength.Should().Be(0.96m);
        tenure.Detail.Should().Be("A member for 1 month: members leave most in their first two years.");
        result.Score.Should().Be(5, "only the tenure sign shows, at nearly all of its weight of 5");
    }

    [Fact]
    public void WhatTheyOweCountsByHowLongItHasBeenOverdue_AndTheRemindersItTook()
    {
        var a = Steady() with { OverdueAmount = 450m, OldestOverdueDays = 30, RemindersRecent = 1 };
        var b = Steady() with { OverdueAmount = 450m, OldestOverdueDays = 90, RemindersRecent = 1 };
        var c = Steady() with { OverdueAmount = 10m, OldestOverdueDays = 1, RemindersRecent = 0 };

        RiskScoring.Score(a, RiskWeights.Default, Money).Factors.Single(f => f.Key == RiskScoring.Balance).Strength.Should().Be(0.5m);
        var full = RiskScoring.Score(b, RiskWeights.Default, Money).Factors.Single(f => f.Key == RiskScoring.Balance);
        full.Strength.Should().Be(1m);
        full.Points.Should().Be(25);
        full.Detail.Should().Be("CHF 450.00 overdue, the oldest 90 days past its due date; 1 reminder sent in the last 90 days.");
        RiskScoring.Score(c, RiskWeights.Default, Money).Factors.Single(f => f.Key == RiskScoring.Balance).Strength
            .Should().Be(0.2m, "anything overdue counts a little");
    }

    [Fact]
    public void AMemberWhoStoppedComing_StoppedSpending_AndOwesMoney_IsAtRisk_AndEachSignSaysWhy()
    {
        var signals = Steady() with
        {
            VisitsRecent = 0, SpendRecent = 0, OverdueAmount = 600m, OldestOverdueDays = 75, RemindersRecent = 2,
            EventsRecent = 0, NoShowsRecent = 1, OpenComplaints = 1,
        };

        var result = RiskScoring.Score(signals, RiskWeights.Default, Money);

        result.Score.Should().Be(30 + 15 + 25 + 10 + 8, "visits, spending, balance and events all show in full, an open complaint half");
        result.Level.Should().Be(RiskLevels.AtRisk);
        result.Factors.Single(f => f.Key == RiskScoring.Concerns).Detail.Should().Be("1 open complaint.");
        result.Factors.Single(f => f.Key == RiskScoring.Events).Detail.Should().Be("Came to 0 events in the last 6 months, against about 3 in the 6 months before; 1 no-show.");
    }

    [Fact]
    public void TheClubsWeights_DecideHowMuchEachSignCounts()
    {
        var signals = Steady() with { OverdueAmount = 600m, OldestOverdueDays = 90 };

        var byDefault = RiskScoring.Score(signals, RiskWeights.Default, Money);
        var onlyMoney = RiskScoring.Score(signals, RiskWeights.Default with { Visits = 0, Spend = 0, Events = 0, Concerns = 0, Tenure = 0 }, Money);

        byDefault.Score.Should().Be(25);
        byDefault.Level.Should().Be(RiskLevels.Low);
        onlyMoney.Score.Should().Be(100, "with the balance the only sign that counts, it is the whole score");
        onlyMoney.Level.Should().Be(RiskLevels.AtRisk);
    }

    [Fact]
    public void AMemberAtRiskStaysSo_UntilTheirScoreFallsTheMarginUnderTheThreshold()
    {
        var w = RiskWeights.Default;
        RiskScoring.LevelOf(58, w).Should().Be(RiskLevels.Watch);
        RiskScoring.LevelOf(58, w, wasAtRisk: true).Should().Be(RiskLevels.AtRisk, "two points under the threshold is noise, not a recovery");
        RiskScoring.LevelOf(54, w, wasAtRisk: true).Should().Be(RiskLevels.Watch);
        RiskScoring.LevelOf(20, w).Should().Be(RiskLevels.Low);
    }
}
