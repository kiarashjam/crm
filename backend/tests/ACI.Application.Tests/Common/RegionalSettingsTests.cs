using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Reading a workspace's currency, locale and time zone, and its calendar days.
/// </summary>
public class RegionalSettingsTests
{
    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf("Europe/Zurich");

    [Theory]
    [InlineData("chf", "CHF")]
    [InlineData(" EUR ", "EUR")]
    public void ACurrencyIsAThreeLetterCode(string raw, string code)
    {
        RegionalSettings.TryCurrency(raw, out var read).Should().BeTrue();
        read.Should().Be(code);
    }

    [Theory]
    [InlineData("€")]
    [InlineData("EURO")]
    [InlineData("C1F")]
    [InlineData(null)]
    public void WhatIsNotThreeLettersIsNoCurrency(string? raw)
    {
        RegionalSettings.TryCurrency(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void ALocaleIsOneThisHostKnowsWrittenAsBrowsersWriteIt()
    {
        RegionalSettings.TryLocale("de-CH", out var swiss).Should().BeTrue();
        swiss.Should().Be("de-CH");
        RegionalSettings.TryLocale("de_CH", out _).Should().BeFalse("a browser would not read it");
        RegionalSettings.TryLocale("xx-YY", out _).Should().BeFalse();
    }

    [Fact]
    public void AZoneIsAnIanaNameAndAWindowsNameIsGivenBackAsOne()
    {
        RegionalSettings.TryTimeZone("Europe/Zurich", out var iana).Should().BeTrue();
        iana.Should().Be("Europe/Zurich");
        RegionalSettings.TryTimeZone("Mars/Olympus_Mons", out _).Should().BeFalse();
    }

    [Fact]
    public void AZoneThisHostDoesNotKnowCountsInTheDefaultNotInUtc()
    {
        var zone = RegionalSettings.ZoneOf("Mars/Olympus_Mons");

        // Zurich is an hour ahead of UTC in winter and two in summer.
        zone.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)).Should().Be(TimeSpan.FromHours(1));
        zone.GetUtcOffset(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)).Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void TodayIsTheWorkspacesNotUtcs()
    {
        // Half past eleven at night UTC on 31 October is half past midnight on 1 November in Zurich.
        RegionalSettings.TodayIn(Zurich, new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 11, 1));
        RegionalSettings.TodayIn(TimeZoneInfo.Utc, new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void ADayStartsAtTheWorkspacesMidnightInSummerAndWinterTime()
    {
        RegionalSettings.StartOfDayUtc(new DateOnly(2026, 10, 1), Zurich).Should().Be(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc));
        RegionalSettings.StartOfDayUtc(new DateOnly(2026, 11, 1), Zurich).Should().Be(new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc));
        // The day the clocks go back is 25 hours long, and still starts at its midnight.
        RegionalSettings.StartOfDayUtc(new DateOnly(2026, 10, 25), Zurich).Should().Be(new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ADayWhoseMidnightIsSkippedStartsWhenTheClocksLand()
    {
        // Havana puts its clocks forward at midnight: 8 March 2026 has no 00:00, and
        // begins at 01:00 local time, 05:00 UTC.
        var havana = RegionalSettings.ZoneOf("America/Havana");
        RegionalSettings.StartOfDayUtc(new DateOnly(2026, 3, 8), havana).Should().Be(new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc));
    }
}
