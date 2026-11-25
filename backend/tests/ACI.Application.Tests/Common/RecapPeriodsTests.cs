using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Common;

/// <summary>
/// When the recaps fall due and what each covers, on the workspace's calendar: daily from 07:00
/// for the day before, weekly on Monday from 07:00 for the week just ended, monthly on the 1st
/// from 07:00 for the month just ended; days counted on the calendar through the clock changes.
/// </summary>
public class RecapPeriodsTests
{
    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf("Europe/Zurich");

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void TheDailyRecapIsDueFromSevenForTheDayBefore()
    {
        // Tuesday 24 November 2026, CET (UTC+1): 07:00 is 06:00 UTC.
        var due = RecapPeriods.LatestDue(RecapFrequencies.Daily, Utc(2026, 11, 24, 6), Zurich);
        due.FirstDay.Should().Be(new DateOnly(2026, 11, 23));
        due.LastDay.Should().Be(new DateOnly(2026, 11, 23));
        due.FromUtc.Should().Be(Utc(2026, 11, 22, 23));
        due.ToUtc.Should().Be(Utc(2026, 11, 23, 23));
        due.DueUtc.Should().Be(Utc(2026, 11, 24, 6));

        var early = RecapPeriods.LatestDue(RecapFrequencies.Daily, Utc(2026, 11, 24, 5, 59), Zurich);
        early.FirstDay.Should().Be(new DateOnly(2026, 11, 22), "06:59 in Zurich: Monday's is not due yet, so Sunday's is the latest");

        // In summer, seven in Zurich is five UTC.
        RecapPeriods.LatestDue(RecapFrequencies.Daily, Utc(2026, 7, 1, 5), Zurich).FirstDay.Should().Be(new DateOnly(2026, 6, 30));
        RecapPeriods.LatestDue(RecapFrequencies.Daily, Utc(2026, 7, 1, 4, 59), Zurich).FirstDay.Should().Be(new DateOnly(2026, 6, 29));
    }

    [Fact]
    public void TheDayTheClocksGoForwardIs23HoursAndTheDayTheyGoBack25_EachStillOneDay()
    {
        // Sunday 29 March 2026: 02:00 CET becomes 03:00 CEST.
        var spring = RecapPeriods.For(RecapFrequencies.Daily, new DateOnly(2026, 3, 29), Zurich);
        spring.FromUtc.Should().Be(Utc(2026, 3, 28, 23));
        spring.ToUtc.Should().Be(Utc(2026, 3, 29, 22));
        (spring.ToUtc - spring.FromUtc).Should().Be(TimeSpan.FromHours(23));
        spring.DueUtc.Should().Be(Utc(2026, 3, 30, 5), "07:00 summer time");
        RecapPeriods.LatestDue(RecapFrequencies.Daily, Utc(2026, 3, 30, 5), Zurich).Should().Be(spring);

        // Sunday 25 October 2026: 03:00 CEST becomes 02:00 CET.
        var autumn = RecapPeriods.For(RecapFrequencies.Daily, new DateOnly(2026, 10, 25), Zurich);
        autumn.FromUtc.Should().Be(Utc(2026, 10, 24, 22));
        autumn.ToUtc.Should().Be(Utc(2026, 10, 25, 23));
        (autumn.ToUtc - autumn.FromUtc).Should().Be(TimeSpan.FromHours(25));
        autumn.DueUtc.Should().Be(Utc(2026, 10, 26, 6), "07:00 winter time");
    }

    [Fact]
    public void TheWeeklyRecapIsDueOnMondayFromSevenForMondayToSundayJustEnded()
    {
        // Monday 30 March 2026, 07:00 CEST: the week of the clock change, 23 to 29 March.
        var week = RecapPeriods.LatestDue(RecapFrequencies.Weekly, Utc(2026, 3, 30, 5), Zurich);
        week.FirstDay.Should().Be(new DateOnly(2026, 3, 23));
        week.LastDay.Should().Be(new DateOnly(2026, 3, 29));
        week.FromUtc.Should().Be(Utc(2026, 3, 22, 23));
        week.ToUtc.Should().Be(Utc(2026, 3, 29, 22));

        RecapPeriods.LatestDue(RecapFrequencies.Weekly, Utc(2026, 3, 30, 4, 59), Zurich).FirstDay
            .Should().Be(new DateOnly(2026, 3, 16), "06:59 on Monday: the week before is still the latest");
        RecapPeriods.LatestDue(RecapFrequencies.Weekly, Utc(2026, 4, 2, 12), Zurich).FirstDay
            .Should().Be(new DateOnly(2026, 3, 23), "all week long, the week just ended");
    }

    [Fact]
    public void TheMonthlyRecapIsDueOnTheFirstFromSevenForTheMonthJustEnded()
    {
        var november = RecapPeriods.LatestDue(RecapFrequencies.Monthly, Utc(2026, 12, 1, 6), Zurich);
        november.FirstDay.Should().Be(new DateOnly(2026, 11, 1));
        november.LastDay.Should().Be(new DateOnly(2026, 11, 30));
        november.FromUtc.Should().Be(Utc(2026, 10, 31, 23));
        november.ToUtc.Should().Be(Utc(2026, 11, 30, 23));
        RecapPeriods.LatestDue(RecapFrequencies.Monthly, Utc(2026, 12, 1, 5, 59), Zurich).FirstDay
            .Should().Be(new DateOnly(2026, 10, 1), "06:59 on the 1st: October is still the latest");

        // Month ends: a leap February, January's 31 days, and October across the clock change.
        var february = RecapPeriods.LatestDue(RecapFrequencies.Monthly, Utc(2028, 3, 1, 7), Zurich);
        february.LastDay.Should().Be(new DateOnly(2028, 2, 29));
        RecapPeriods.For(RecapFrequencies.Monthly, new DateOnly(2027, 1, 1), Zurich).LastDay.Should().Be(new DateOnly(2027, 1, 31));
        var october = RecapPeriods.For(RecapFrequencies.Monthly, new DateOnly(2026, 10, 1), Zurich);
        october.FromUtc.Should().Be(Utc(2026, 9, 30, 22), "midnight summer time");
        october.ToUtc.Should().Be(Utc(2026, 10, 31, 23), "midnight winter time");
        october.DueUtc.Should().Be(Utc(2026, 11, 1, 6));
    }

    [Fact]
    public void OnAFirstThatIsAMondayTheWeeklyAndTheMonthlyFallDueTogether()
    {
        // Monday 1 June 2026, 07:00 CEST.
        var at = Utc(2026, 6, 1, 5);
        var week = RecapPeriods.LatestDue(RecapFrequencies.Weekly, at, Zurich);
        var month = RecapPeriods.LatestDue(RecapFrequencies.Monthly, at, Zurich);
        (week.FirstDay, week.LastDay).Should().Be((new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 31)));
        (month.FirstDay, month.LastDay).Should().Be((new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)));
        week.DueUtc.Should().Be(month.DueUtc);
        RecapPeriods.LatestDue(RecapFrequencies.Daily, at, Zurich).FirstDay.Should().Be(new DateOnly(2026, 5, 31));

        var before = Utc(2026, 6, 1, 4, 59);
        RecapPeriods.LatestDue(RecapFrequencies.Weekly, before, Zurich).FirstDay.Should().Be(new DateOnly(2026, 5, 18));
        RecapPeriods.LatestDue(RecapFrequencies.Monthly, before, Zurich).FirstDay.Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public void ThePeriodBeforeIsTheDayTheWeekOrTheMonthBefore()
    {
        var day = RecapPeriods.For(RecapFrequencies.Daily, new DateOnly(2026, 3, 1), Zurich);
        RecapPeriods.Previous(day, Zurich).FirstDay.Should().Be(new DateOnly(2026, 2, 28));
        var week = RecapPeriods.For(RecapFrequencies.Weekly, new DateOnly(2026, 11, 16), Zurich);
        var weekBefore = RecapPeriods.Previous(week, Zurich);
        (weekBefore.FirstDay, weekBefore.LastDay).Should().Be((new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)));
        weekBefore.ToUtc.Should().Be(week.FromUtc, "the two periods meet without a gap");
        var march = RecapPeriods.For(RecapFrequencies.Monthly, new DateOnly(2026, 3, 1), Zurich);
        var february = RecapPeriods.Previous(march, Zurich);
        (february.FirstDay, february.LastDay).Should().Be((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
        RecapPeriods.Previous(RecapPeriods.For(RecapFrequencies.Monthly, new DateOnly(2027, 1, 1), Zurich), Zurich).FirstDay
            .Should().Be(new DateOnly(2026, 12, 1));
    }

    [Fact]
    public void ADayThatStartsAtAMidnightTheClocksSkipStartsWhenTheyLand()
    {
        // A zone whose clocks jump from 00:00 to 01:00 on the first Monday of March.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 1, DayOfWeek.Monday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 5, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test/MondayJump", TimeSpan.Zero, "Monday jump", "Standard", "Summer", new[] { rule });

        var monday = RecapPeriods.For(RecapFrequencies.Daily, new DateOnly(2027, 3, 1), zone);
        monday.FromUtc.Should().Be(Utc(2027, 3, 1, 0), "01:00 summer time, when that Monday began");
        RecapPeriods.LatestDue(RecapFrequencies.Weekly, Utc(2027, 3, 8, 6), zone).FirstDay.Should().Be(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public void OnlyTheThreeFrequenciesAreKnown()
    {
        var act = () => RecapPeriods.LatestDue("hourly", Utc(2026, 11, 24, 6), Zurich);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
