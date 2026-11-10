using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;

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
    public async Task LeadsReadUpToTheCutSayTheCalendarMayBeMissingSome()
    {
        // The leads come from SQL up to a cut. When the cut is reached there may be more
        // meetings than were read, so the calendar says some may be missing rather than
        // passing the ones read off as all of them.
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(t => t.GetDueBetweenAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var activities = new Mock<IActivityRepository>();
        activities.Setup(a => a.GetMeetingsBetweenAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var leads = new Mock<ILeadRepository>();
        leads.Setup(l => l.GetWithMeetingInMonthsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid? _, IReadOnlyList<string> _, bool _, int take, CancellationToken _) =>
                Enumerable.Range(0, take).Select(i => new Lead
                {
                    Id = Guid.NewGuid(), Name = $"Lead {i}",
                    // In the months asked for, but outside the days on show.
                    PipelineState = """{"meetingDate":"2026-12-28"}""",
                }).ToList());

        var calendar = await new CalendarService(tasks.Object, activities.Object, leads.Object)
            .GetAsync(Guid.NewGuid(), Guid.NewGuid(), from, from.AddDays(42), "team");

        calendar.IsSuccess.Should().BeTrue();
        calendar.Value.Entries.Should().BeEmpty();
        calendar.Value.Truncated.Should().BeTrue();
    }

    [Fact]
    public void TheLongestSpanReadNeverCoversMoreThanThreeMonths()
    {
        // The lead query matches text for each month, so the months are kept few: the
        // longest span, with a day either side, starting on the last day of a month, must
        // stay inside three.
        var start = new DateOnly(2027, 1, 31).AddDays(-1);
        var end = new DateOnly(2027, 1, 31).AddDays(CalendarService.MaxDays + 1);
        CalendarService.Months(start, end).Should().HaveCountLessThanOrEqualTo(3);
    }
}
