using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// A subscription's periods: calendar months, quarters, half years and years.
/// </summary>
/// <remarks>
/// Billing stepped 30, 91, 182 or 365 days, so a monthly subscription billed on 1 January
/// was billed on 31 January, 2 March and so on: thirteen times in some years.
/// </remarks>
public class BillingPeriodsTests
{
    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AMonthlySubscriptionBillsTwelveTimesAYear_OnTheSameDay()
    {
        var starts = new List<DateTime> { Utc(2027, 1, 1) };
        while (starts.Count < 13) starts.Add(BillingPeriods.Next(starts[^1], "Monthly", 1));

        starts.Take(12).Should().OnlyContain(d => d.Year == 2027 && d.Day == 1);
        starts[12].Should().Be(Utc(2028, 1, 1), "the thirteenth period is next year's first, not a second December");
    }

    [Theory]
    [InlineData("Quarterly", 2027, 4, 15)]
    [InlineData("SemiAnnual", 2027, 7, 15)]
    [InlineData("Annual", 2028, 1, 15)]
    public void LongerCadencesStepWholeMonths(string cadence, int year, int month, int day)
    {
        BillingPeriods.Next(Utc(2027, 1, 15), cadence, 15).Should().Be(Utc(year, month, day));
    }

    [Fact]
    public void ASubscriptionThatStartsOnThe31stBillsOnTheLastDayOfShortMonths_AndGoesBackToThe31st()
    {
        var starts = new List<DateTime> { Utc(2027, 1, 31) };
        for (var i = 0; i < 3; i++) starts.Add(BillingPeriods.Next(starts[^1], "Monthly", 31));

        starts.Should().Equal(Utc(2027, 1, 31), Utc(2027, 2, 28), Utc(2027, 3, 31), Utc(2027, 4, 30));
        BillingPeriods.Next(Utc(2028, 1, 31), "Monthly", 31).Should().Be(Utc(2028, 2, 29), "a leap year's February has a 29th");
    }

    [Fact]
    public void APeriodKeepsTheTimeOfDayItStartedAt_SoTheSamePeriodIsWorkedOutTheSameWayEveryTime()
    {
        var start = new DateTime(2026, 11, 10, 14, 23, 51, DateTimeKind.Utc).AddTicks(1234);
        BillingPeriods.Next(start, "Monthly", 10).Should().Be(new DateTime(2026, 12, 10, 14, 23, 51, DateTimeKind.Utc).AddTicks(1234));
    }

    [Fact]
    public void AfterAPauseBillingPicksUpAtTheNextPeriodStartingOnOrAfterTheDay()
    {
        BillingPeriods.FirstOnOrAfter(Utc(2026, 9, 1), "Monthly", 1, Utc(2026, 11, 10)).Should().Be(Utc(2026, 12, 1));
        BillingPeriods.FirstOnOrAfter(Utc(2026, 9, 1), "Monthly", 1, Utc(2026, 12, 1)).Should().Be(Utc(2026, 12, 1));
        BillingPeriods.FirstOnOrAfter(Utc(2026, 12, 1), "Monthly", 1, Utc(2026, 11, 10)).Should().Be(Utc(2026, 12, 1), "a date still ahead stays");
    }

    [Fact]
    public void APeriodIsNamedByItsFirstAndLastDay()
    {
        BillingPeriods.Label(Utc(2026, 12, 1), Utc(2027, 1, 1)).Should().Be("1 Dec 2026 – 31 Dec 2026");
        BillingPeriods.Label(Utc(2027, 1, 31), Utc(2027, 2, 28)).Should().Be("31 Jan 2027 – 27 Feb 2027");
    }
}
