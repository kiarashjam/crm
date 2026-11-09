using System.Globalization;
using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The sales calendar: tasks at the time they are due, the meetings logged as activities,
/// and the meeting dates on leads' pipelines.
/// </summary>
/// <remarks>
/// The calendar showed club events and reservations only: no task, no meeting, nothing a
/// rep plans or holds.
/// </remarks>
public class CalendarTests
{
    private sealed record Team(CustomWebApplicationFactory Factory, HttpClient Marc, Guid MarcId, HttpClient Anna, Guid AnnaId, HttpClient Luca, Guid LucaId)
        : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<Team> TeamAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (marc, marcId) = await SignInAsync(factory, "marc@pavillon46.ch");
        var org = await CreateAsync(factory, marcId, "Pavillon46");
        var (anna, annaId) = await SignInAsync(factory, "anna@pavillon46.ch");
        var (luca, lucaId) = await SignInAsync(factory, "luca@pavillon46.ch");
        await AddMemberAsync(factory, org, annaId, OrgMemberRole.Member);
        await AddMemberAsync(factory, org, lucaId, OrgMemberRole.Member);
        return new Team(factory, marc.In(org), marcId, anna.In(org), annaId, luca.In(org), lucaId);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string Iso(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> CalendarAsync(HttpClient client, DateTime from, DateTime to, string? scope = null) =>
        client.GetAsync($"/api/calendar?from={Uri.EscapeDataString(Iso(from))}&to={Uri.EscapeDataString(Iso(to))}{(scope == null ? "" : $"&scope={scope}")}");

    private static async Task<Guid> LeadAsync(HttpClient client, string name, object? pipeline = null) =>
        (await Ok(await client.PostAsync("/api/leads", Body(new
        {
            name, email = $"{name.Replace(' ', '.').ToLowerInvariant()}@example.ch", status = "New",
            pipelineState = pipeline == null ? null : JsonSerializer.Serialize(pipeline),
        })), $"create {name}")).GetProperty("id").GetGuid();

    [Fact]
    public async Task MyCalendarHasMyTasksAtTheirTimeMyMeetingsAndMyLeadsMeetingDays()
    {
        using var team = await TeamAsync();
        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-10);
        var to = now.Date.AddDays(20);
        var due = now.Date.AddDays(2).AddHours(13).AddMinutes(30);
        var meetingDay = DateOnly.FromDateTime(now.Date.AddDays(5));

        var task = await Ok(await team.Marc.PostAsync("/api/tasks", Body(new { title = "Call the Müllers", dueDateUtc = due, assigneeId = team.AnnaId })), "Anna's task");
        await Ok(await team.Marc.PostAsync("/api/tasks", Body(new { title = "Luca's call", dueDateUtc = due, assigneeId = team.LucaId })), "Luca's task");
        await Ok(await team.Anna.PostAsync("/api/tasks", Body(new { title = "Next quarter", dueDateUtc = now.AddDays(60) })), "out of range");
        await Ok(await team.Anna.PostAsync("/api/tasks", Body(new { title = "Called off", dueDateUtc = due, status = "cancelled" })), "cancelled");

        var lead = await LeadAsync(team.Anna, "Jean Dupont",
            new { outreachStatus = "contacted", contactOutcome = "meeting_scheduled", meetingDate = meetingDay.ToString("yyyy-MM-dd") });
        var held = now.AddDays(-1);
        await Ok(await team.Anna.PostAsync("/api/activities", Body(new
        {
            type = "meeting", subject = "Tour of the club", leadId = lead, occurredAtUtc = held, durationMinutes = 45, outcome = "completed",
        })), "Anna's meeting");
        var lucaLead = await LeadAsync(team.Luca, "Luca Prospect");
        await Ok(await team.Luca.PostAsync("/api/activities", Body(new { type = "meeting", subject = "Luca's tour", leadId = lucaLead, occurredAtUtc = held })), "Luca's meeting");

        var mine = await Ok(await CalendarAsync(team.Anna, from, to), "my calendar");

        mine.GetProperty("scope").GetString().Should().Be("mine");
        var entries = mine.GetProperty("entries").EnumerateArray().ToList();
        entries.Select(e => e.GetProperty("title").GetString()).Should().BeEquivalentTo("Call the Müllers", "Tour of the club", "Meeting with Jean Dupont");

        var taskEntry = entries.Single(e => e.GetProperty("kind").GetString() == "task");
        taskEntry.GetProperty("startUtc").GetDateTime().Should().Be(due);
        taskEntry.GetProperty("link").GetString().Should().Be($"/tasks/{task.GetProperty("id").GetGuid()}");
        taskEntry.GetProperty("allDay").GetBoolean().Should().BeFalse();

        var meeting = entries.Single(e => e.GetProperty("kind").GetString() == "meeting");
        meeting.GetProperty("startUtc").GetDateTime().Should().BeCloseTo(held, TimeSpan.FromSeconds(1));
        meeting.GetProperty("endUtc").GetDateTime().Should().BeCloseTo(held.AddMinutes(45), TimeSpan.FromSeconds(1));
        meeting.GetProperty("status").GetString().Should().Be("completed");
        meeting.GetProperty("detail").GetString().Should().Be("Jean Dupont");
        meeting.GetProperty("link").GetString().Should().Be($"/leads/{lead}");

        var leadMeeting = entries.Single(e => e.GetProperty("kind").GetString() == "lead_meeting");
        leadMeeting.GetProperty("allDay").GetBoolean().Should().BeTrue();
        leadMeeting.GetProperty("day").GetString().Should().Be(meetingDay.ToString("yyyy-MM-dd"));
        leadMeeting.GetProperty("link").GetString().Should().Be($"/leads/{lead}");

        var everyone = await Ok(await CalendarAsync(team.Anna, from, to, "team"), "the team's calendar");
        everyone.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("title").GetString())
            .Should().Contain(new[] { "Luca's call", "Luca's tour" }).And.NotContain(new[] { "Next quarter", "Called off" });
    }

    [Fact]
    public async Task AMeetingTheLeadDidNotComeToSaysSo()
    {
        using var team = await TeamAsync();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3));
        await LeadAsync(team.Anna, "No Show", new { outreachStatus = "contacted", contactOutcome = "meeting_scheduled", meetingDate = day.ToString("yyyy-MM-dd"), meetingAttended = false });

        var calendar = await Ok(await CalendarAsync(team.Anna, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date.AddDays(10)), "my calendar");

        calendar.GetProperty("entries").EnumerateArray().Single().GetProperty("status").GetString().Should().Be("missed");
    }

    [Fact]
    public async Task WorkOnAPrivateDealIsNotOnTheTeamsCalendarForThoseWhoCannotOpenIt()
    {
        using var team = await TeamAsync();
        var deal = await Ok(await team.Marc.PostAsync("/api/deals", Body(new { name = "Board member's renewal", amount = 9000, currency = "CHF" })), "create deal");
        var dealId = deal.GetProperty("id").GetGuid();
        await Ok(await team.Marc.PutAsync($"/api/deals/{dealId}", Body(new { isPrivate = true })), "make it private");
        await Ok(await team.Marc.PostAsync("/api/tasks", Body(new { title = "Confidential terms", dealId, dueDateUtc = DateTime.UtcNow.AddDays(1) })), "task on it");

        var calendar = await Ok(await CalendarAsync(team.Anna, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7), "team"), "the team's calendar");

        calendar.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("title").GetString()).Should().NotContain("Confidential terms");
    }

    [Fact]
    public async Task ALeadsMeetingIsNotCrowdedOutByLeadsWithOtherDatesThatMonth()
    {
        // The leads are picked out in SQL by their pipeline text, and only so many are
        // read. Matched on the month anywhere in the text, a lead first called this month
        // whose meeting is in March took a place, and enough of them pushed real meetings
        // past the cut without a word.
        using var factory = new CustomWebApplicationFactory();
        var (_, marcId) = await SignInAsync(factory, "marc@pavillon46.ch");
        var org = await CreateAsync(factory, marcId, "Pavillon46");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Lead NewLead(string id, string name, string pipeline) => new()
        {
            Id = Guid.Parse(id), UserId = marcId, OrganizationId = org, Name = name, Email = $"{id[..8]}@example.ch",
            Status = "New", PipelineState = pipeline, CreatedAtUtc = DateTime.UtcNow,
        };
        db.Leads.AddRange(
            NewLead("00000000-0000-0000-0000-000000000001", "First called in November",
                """{"outreachDate":"2026-11-03","meetingDate":"2027-03-10"}"""),
            NewLead("ffffffff-ffff-ffff-ffff-ffffffffffff", "Meeting in November",
                """{"outreachDate":"2026-10-20","meetingDate":"2026-11-12"}"""));
        await db.SaveChangesAsync();
        var leads = scope.ServiceProvider.GetRequiredService<ILeadRepository>();

        var found = await leads.GetWithMeetingInMonthsAsync(marcId, org, ["2026-11"], mineOnly: false, take: 1);

        found.Select(l => l.Name).Should().Equal("Meeting in November");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(46)]
    public async Task ASpanThatIsBackwardsOrLongerThanAPageIsRefused(int days)
    {
        using var team = await TeamAsync();
        var from = DateTime.UtcNow.Date;

        var res = await CalendarAsync(team.Anna, from, from.AddDays(days));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SqlServerFact]
    public async Task TheCalendarReadsOnSqlServer()
    {
        // The lead meeting dates are found by matching text in the pipeline JSON in SQL.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (rep, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
            await LeadAsync(rep, "Jean Dupont", new { contactOutcome = "meeting_scheduled", meetingDate = day.ToString("yyyy-MM-dd") });
            await LeadAsync(rep, "Other Month", new { contactOutcome = "meeting_scheduled", meetingDate = day.AddMonths(4).ToString("yyyy-MM-dd") });
            await Ok(await rep.PostAsync("/api/tasks", Body(new { title = "Prepare the tour", dueDateUtc = DateTime.UtcNow.AddDays(1) })), "a task");

            var calendar = await Ok(await CalendarAsync(rep, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14)), "my calendar");

            calendar.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("title").GetString())
                .Should().BeEquivalentTo("Prepare the tour", "Meeting with Jean Dupont");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
