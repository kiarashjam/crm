using ACI.Application.Services;

namespace ACI.Application.Tests.Services;

/// <summary>How the calendar reads a lead's meeting date, and which months it asks for.</summary>
public class CalendarServiceTests
{
    [Theory]
    [InlineData("""{"contactOutcome":"meeting_scheduled","meetingDate":"2026-11-12"}""", "2026-11-12", null)]
    [InlineData("""{"meetingDate":"2026-11-12T15:30","meetingAttended":true}""", "2026-11-12", true)]
    [InlineData("""{ "meetingDate" : "2026-11-12", "meetingAttended": false }""", "2026-11-12", false)]
    public void AMeetingDateIsReadAsItsDay(string json, string day, bool? attended)
    {
        var meeting = CalendarService.MeetingOf(json);

        meeting.Should().NotBeNull();
        meeting!.Value.Day.Should().Be(DateOnly.Parse(day));
        meeting.Value.Attended.Should().Be(attended);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"meetingDate":null}""")]
    [InlineData("""{"meetingDate":"soon"}""")]
    [InlineData("""{"contractSentDate":"2026-11-12"}""")]
    public void AnythingElseIsNoMeeting(string? json)
    {
        CalendarService.MeetingOf(json).Should().BeNull();
    }

    [Fact]
    public void TheMonthsRunFromTheFirstDaysMonthToTheLasts()
    {
        CalendarService.Months(new DateOnly(2026, 10, 25), new DateOnly(2026, 12, 7))
            .Should().Equal("2026-10", "2026-11", "2026-12");
        CalendarService.Months(new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 2))
            .Should().Equal("2026-12", "2027-01");
    }

    [Fact]
    public void TheLongestSpanReadNeverCoversMoreThanThreeMonths()
    {
        // The lead query is written for three month patterns: the longest span, with a day
        // either side, starting on the last day of a month, must stay inside three.
        var start = new DateOnly(2027, 1, 31).AddDays(-1);
        var end = new DateOnly(2027, 1, 31).AddDays(CalendarService.MaxDays + 1);
        CalendarService.Months(start, end).Should().HaveCountLessThanOrEqualTo(3);
    }
}
