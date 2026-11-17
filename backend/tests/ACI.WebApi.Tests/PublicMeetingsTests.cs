using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A meeting a lead books on Pavillon 46's website is posted back, signed with the workspace's
/// onboarding secret, and lands on the lead: on the lead named by its id, else the one with its
/// email, else a new lead; once, however often it is sent; cancelled when the website says so.
/// Anything not signed with the secret, or signed too long ago, is refused without a word.
/// </summary>
public class PublicMeetingsTests
{
    private const string BookingPage = "https://pavillon46.ch/rendez-vous";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected}, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed record Club(CustomWebApplicationFactory App, HttpClient Owner, Guid OwnerId, Guid Org, string Secret);

    private static async Task<Club> ClubAsync(CustomWebApplicationFactory app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        await Json(await owner.PutAsync("/api/onboarding/settings", Body(new { defaultLanguage = "fr", bookingPageUrl = BookingPage })), "settings");
        var secret = (await Json(await owner.PostAsync("/api/onboarding/settings/secret", null), "secret")).GetProperty("secret").GetString()!;
        return new Club(app, owner, ownerId, org, secret);
    }

    private static async Task<Guid> LeadAsync(Club club, string name, string email) =>
        (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name, email, status = "New" })), $"lead {name}")).GetProperty("id").GetGuid();

    private static readonly DateTimeOffset Starts = new(DateTime.UtcNow.Date.AddDays(16).AddHours(14).AddMinutes(30), TimeSpan.Zero);

    /// <summary>What the website posts: the booking, in the person's own words, at Swiss winter time.</summary>
    private static string Booking(Guid org, Guid? leadId, string email, string bookingId = "bk_1001", string status = "booked",
        string firstName = "Léa", string lastName = "Favre", string? code = "RDV-7K2Q") =>
        JsonSerializer.Serialize(new
        {
            organizationId = org, leadId, code, bookingId, firstName, lastName, email, phone = "+41 79 123 45 67",
            message = "J'aimerais visiter le spa.", startsAt = Starts.ToOffset(TimeSpan.FromHours(1)).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            durationMinutes = 45, language = "fr", status,
        });

    /// <summary>Posts it as the website does: signed with the secret over "{timestamp}.{body}".</summary>
    private static Task<HttpResponseMessage> PostAsync(Club club, string body, string? secret = null, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret ?? club.Secret), Encoding.UTF8.GetBytes($"{ts}.{body}"))).ToLowerInvariant();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/public/meetings") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Cadence-Timestamp", ts.ToString());
        request.Headers.Add("X-Cadence-Signature-256", signature);
        return club.App.CreateClient().SendAsync(request);
    }

    private static async Task<List<JsonElement>> MeetingsOfAsync(Club club, Guid leadId) =>
        (await Json(await club.Owner.GetAsync($"/api/activities/lead/{leadId}"), "the lead's timeline")).EnumerateArray()
            .Where(a => a.GetProperty("type").GetString() == "meeting").ToList();

    [Fact]
    public async Task ABooking_GoesOnTheLeadNamedByItsId_AtItsTime_Once_AndTheOwnerIsTold()
    {
        await using var app = new CustomWebApplicationFactory();
        var club = await ClubAsync(app);
        var lea = await LeadAsync(club, "Léa Favre", "lea@example.ch");
        await LeadAsync(club, "Léa Favre (work)", "lea.favre@work.example");

        // Booked with her invitation, with another address typed in on the website: the id decides.
        var receipt = await Json(await PostAsync(club, Booking(club.Org, lea, "lea.favre@work.example")), "the booking");
        receipt.GetProperty("result").GetString().Should().Be("recorded");
        receipt.GetProperty("leadId").GetGuid().Should().Be(lea);

        var meeting = (await MeetingsOfAsync(club, lea)).Should().ContainSingle().Subject;
        meeting.GetProperty("subject").GetString().Should().Be("Meeting with Léa Favre (RDV-7K2Q)");
        meeting.GetProperty("occurredAtUtc").GetDateTime().Should().Be(Starts.UtcDateTime);
        meeting.GetProperty("durationMinutes").GetInt32().Should().Be(45);
        var body = meeting.GetProperty("body").GetString()!;
        body.Should().Contain("Booked on pavillon46.ch").And.Contain("Booking code: RDV-7K2Q").And.Contain("J'aimerais visiter le spa.")
            .And.Contain("+41 79 123 45 67").And.Contain("(UTC+01:00)").And.Contain("15:30–16:15");

        // The lead's pipeline records the meeting, on its day.
        var lead = await Json(await club.Owner.GetAsync($"/api/leads/{lea}"), "Léa");
        using (var pipeline = JsonDocument.Parse(lead.GetProperty("pipelineState").GetString()!))
        {
            pipeline.RootElement.GetProperty("contactOutcome").GetString().Should().Be("meeting_scheduled");
            pipeline.RootElement.GetProperty("meetingDate").GetString().Should().Be(Starts.ToOffset(TimeSpan.FromHours(1)).ToString("yyyy-MM-dd"));
        }

        // On the calendar at its time, once.
        var calendar = await Json(await club.Owner.GetAsync(
            $"/api/calendar?from={Starts.UtcDateTime.Date.AddDays(-3):yyyy-MM-dd'T'HH:mm:ss'Z'}&to={Starts.UtcDateTime.Date.AddDays(3):yyyy-MM-dd'T'HH:mm:ss'Z'}&scope=team"), "calendar");
        calendar.GetProperty("entries").EnumerateArray().Where(e => e.GetProperty("link").GetString() == $"/leads/{lea}")
            .Select(e => e.GetProperty("kind").GetString()).Should().Equal("meeting");

        // The owner is told.
        var bell = await Json(await club.Owner.GetAsync("/api/notifications"), "notifications");
        bell.EnumerateArray().Should().Contain(n => n.GetProperty("title").GetString() == "Meeting booked: Léa Favre"
            && n.GetProperty("link").GetString() == $"/leads/{lea}");

        // The website sends it again (its own retry): nothing more.
        var again = await Json(await PostAsync(club, Booking(club.Org, lea, "lea.favre@work.example")), "the same booking again");
        again.GetProperty("result").GetString().Should().Be("unchanged");
        (await MeetingsOfAsync(club, lea)).Should().HaveCount(1);
        (await app.Services.Read(db => db.MeetingBookings.Count())).Should().Be(1);
    }

    /// <summary>The timeline's store, which fails the first meeting written to it once armed: the database going away mid-report.</summary>
    public class FailingTimeline : DispatchProxy
    {
        public IActivityRepository Inner { get; set; } = null!;
        public Breaker Breaker { get; set; } = null!;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IActivityRepository.AddAsync) && args![0] is Activity { Type: "meeting" } && Breaker.Trip())
                return Task.FromException<Activity>(new InvalidOperationException("The database went away."));
            try
            {
                return method.Invoke(Inner, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }
    }

    public sealed class Breaker
    {
        private int _armed;
        public void Arm() => _armed = 1;
        public bool Trip() => Interlocked.Exchange(ref _armed, 0) == 1;
    }

    private sealed class AppWithFailingTimeline : CustomWebApplicationFactory
    {
        public readonly Breaker Breaker = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IActivityRepository>(sp =>
            {
                var proxy = DispatchProxy.Create<IActivityRepository, FailingTimeline>();
                var failing = (FailingTimeline)(object)proxy;
                failing.Inner = new ActivityRepository(sp.GetRequiredService<AppDbContext>());
                failing.Breaker = Breaker;
                return proxy;
            }));
        }
    }

    [Fact]
    public async Task AReportThatFailsAfterItsBookingIsClaimed_IsFinishedByTheRetry_Once()
    {
        await using var app = new AppWithFailingTimeline();
        var club = await ClubAsync(app);
        var lea = await LeadAsync(club, "Léa Favre", "lea@example.ch");
        var body = Booking(club.Org, lea, "lea@example.ch");

        // The first report claims the booking, then fails writing the meeting: the website is told it did not go.
        app.Breaker.Arm();
        HttpResponseMessage? first = null;
        try
        {
            first = await PostAsync(club, body);
        }
        catch (InvalidOperationException)
        {
            // The test server hands the failure back as it is, where a real one answers 500.
        }
        first?.IsSuccessStatusCode.Should().BeFalse();
        (await app.Services.Read(db => db.MeetingBookings.Single().ActivityId)).Should().BeNull("the booking is claimed, not done");
        (await MeetingsOfAsync(club, lea)).Should().BeEmpty();

        // The website's retry finishes it: the meeting on the timeline, the owner told.
        var retry = await Json(await PostAsync(club, body), "the retry");
        retry.GetProperty("result").GetString().Should().Be("recorded");
        retry.GetProperty("activityId").GetGuid().Should().NotBeEmpty();
        (await MeetingsOfAsync(club, lea)).Should().ContainSingle().Which.GetProperty("subject").GetString().Should().Be("Meeting with Léa Favre (RDV-7K2Q)");
        var told = (await Json(await club.Owner.GetAsync("/api/notifications"), "notifications")).EnumerateArray()
            .Count(n => n.GetProperty("title").GetString() == "Meeting booked: Léa Favre");
        told.Should().Be(1);

        // A third time, it is done: nothing more.
        (await Json(await PostAsync(club, body), "again")).GetProperty("result").GetString().Should().Be("unchanged");
        (await MeetingsOfAsync(club, lea)).Should().HaveCount(1);
        (await Json(await club.Owner.GetAsync("/api/notifications"), "notifications")).EnumerateArray()
            .Count(n => n.GetProperty("title").GetString() == "Meeting booked: Léa Favre").Should().Be(1);
        (await app.Services.Read(db => db.MeetingBookings.Count())).Should().Be(1);
    }

    [Fact]
    public async Task WithoutALeadId_TheLeadWithTheEmailHasIt_AndACancellationIsRecordedAndTold()
    {
        await using var app = new CustomWebApplicationFactory();
        var club = await ClubAsync(app);
        var marc = await LeadAsync(club, "Marc Dubois", "marc@example.ch");

        await Json(await PostAsync(club, Booking(club.Org, null, "MARC@example.ch", "bk_2002", firstName: "Marc", lastName: "Dubois", code: null)), "booked by email");
        var meeting = (await MeetingsOfAsync(club, marc)).Should().ContainSingle().Subject;
        meeting.GetProperty("subject").GetString().Should().Be("Meeting with Marc Dubois");

        // An id the CRM does not know (a lead deleted since) falls back to the email too.
        await Json(await PostAsync(club, Booking(club.Org, Guid.NewGuid(), "marc@example.ch", "bk_2003", firstName: "Marc", lastName: "Dubois")), "an unknown id");
        (await MeetingsOfAsync(club, marc)).Should().HaveCount(2);

        var cancelled = await Json(await PostAsync(club, Booking(club.Org, null, "marc@example.ch", "bk_2002", "cancelled", "Marc", "Dubois", null)), "cancelled");
        cancelled.GetProperty("result").GetString().Should().Be("cancelled");
        var after = (await MeetingsOfAsync(club, marc)).Single(m => m.GetProperty("id").GetGuid() == meeting.GetProperty("id").GetGuid());
        after.GetProperty("outcome").GetString().Should().Be("cancelled");
        after.GetProperty("subject").GetString().Should().Be("Cancelled: Meeting with Marc Dubois");
        after.GetProperty("body").GetString().Should().Contain("Cancelled on the website");
        var bell = await Json(await club.Owner.GetAsync("/api/notifications"), "notifications");
        bell.EnumerateArray().Should().Contain(n => n.GetProperty("title").GetString() == "Meeting cancelled: Marc Dubois");

        // Cancelled twice: nothing more.
        (await Json(await PostAsync(club, Booking(club.Org, null, "marc@example.ch", "bk_2002", "cancelled", "Marc", "Dubois", null)), "cancelled again"))
            .GetProperty("result").GetString().Should().Be("unchanged");
    }

    [Fact]
    public async Task SomeoneTheCrmDoesNotKnow_BecomesALead_FromTheWebsite()
    {
        await using var app = new CustomWebApplicationFactory();
        var club = await ClubAsync(app);

        var receipt = await Json(await PostAsync(club, Booking(club.Org, null, "hugo@example.ch", "bk_3003", firstName: "Hugo", lastName: "Meyer", code: null)), "a stranger");
        receipt.GetProperty("leadCreated").GetBoolean().Should().BeTrue();
        var leadId = receipt.GetProperty("leadId").GetGuid();
        var lead = await Json(await club.Owner.GetAsync($"/api/leads/{leadId}"), "the new lead");
        (lead.GetProperty("name").GetString(), lead.GetProperty("email").GetString(), lead.GetProperty("source").GetString())
            .Should().Be(("Hugo Meyer", "hugo@example.ch", "Rendez-vous pavillon46.ch"));
        lead.GetProperty("pipelineState").GetString().Should().Contain("meeting_scheduled");
        (await MeetingsOfAsync(club, leadId)).Should().ContainSingle();
    }

    [Fact]
    public async Task AnythingNotSignedWithTheSecret_OrSignedTooLongAgo_IsRefusedWithoutAWord()
    {
        await using var app = new CustomWebApplicationFactory();
        var club = await ClubAsync(app);
        var lea = await LeadAsync(club, "Léa Favre", "lea@example.ch");
        var body = Booking(club.Org, lea, "lea@example.ch");

        async Task Refused(HttpResponseMessage res, string why)
        {
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized, why);
            // The framework's bare "Unauthorized", the same each time: not which check failed.
            var said = await res.Content.ReadAsStringAsync();
            said.Should().NotContain("detail", $"{why}: nothing is said").And.NotContain("secret").And.NotContain("organization");
        }

        await Refused(await PostAsync(club, body, secret: "not the secret"), "another secret");
        await Refused(await PostAsync(club, body, timestamp: DateTimeOffset.UtcNow.AddMinutes(-11).ToUnixTimeSeconds()), "eleven minutes old");
        await Refused(await PostAsync(club, body, timestamp: DateTimeOffset.UtcNow.AddMinutes(11).ToUnixTimeSeconds()), "from the future");
        await Refused(await PostAsync(club, Booking(Guid.NewGuid(), lea, "lea@example.ch")), "no such workspace");
        await Refused(await PostAsync(club, "not json"), "not even JSON");
        var unsigned = new HttpRequestMessage(HttpMethod.Post, "/api/public/meetings") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        await Refused(await app.CreateClient().SendAsync(unsigned), "unsigned");
        // Signed, then changed on the way.
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tampered = new HttpRequestMessage(HttpMethod.Post, "/api/public/meetings")
        {
            Content = new StringContent(body.Replace("bk_1001", "bk_6666"), Encoding.UTF8, "application/json"),
        };
        tampered.Headers.Add("X-Cadence-Timestamp", ts.ToString());
        tampered.Headers.Add("X-Cadence-Signature-256", WebhookSigning.Sign(club.Secret, ts, body));
        await Refused(await app.CreateClient().SendAsync(tampered), "changed after signing");

        (await MeetingsOfAsync(club, lea)).Should().BeEmpty();
        (await app.Services.Read(db => db.MeetingBookings.Count())).Should().Be(0);

        // Within ten minutes it is taken.
        await Json(await PostAsync(club, body, timestamp: DateTimeOffset.UtcNow.AddMinutes(-9).ToUnixTimeSeconds()), "nine minutes old");
        (await MeetingsOfAsync(club, lea)).Should().ContainSingle();
    }

    [Fact]
    public async Task ABookingFromATestEmail_IsTakenAndRecordedNowhere()
    {
        await using var app = new CustomWebApplicationFactory();
        var club = await ClubAsync(app);
        var receipt = await Json(await PostAsync(club, Booking(club.Org, Guid.Empty, "owner@pavillon46.ch")), "a test booking");
        receipt.GetProperty("result").GetString().Should().Be("test");
        (await app.Services.Read(db => db.MeetingBookings.Count() + db.Leads.Count())).Should().Be(0);
    }

    [Fact]
    public void ThePipeline_GetsTheMeeting_UnlessTheLeadIsPastIt()
    {
        var starts = new DateTimeOffset(2026, 12, 3, 14, 30, 0, TimeSpan.FromHours(1));
        var fresh = ACI.Application.Services.MeetingBookingService.Pipeline(null, starts, cancelled: false)!;
        fresh.Should().Contain("\"contactOutcome\":\"meeting_scheduled\"").And.Contain("\"meetingDate\":\"2026-12-03\"").And.Contain("\"outreachStatus\":\"contacted\"");
        ACI.Application.Services.MeetingBookingService.Pipeline("""{"meetingDate":"2026-11-01","meetingAttended":true}""", starts, cancelled: false)
            .Should().BeNull("a lead that has met already keeps its pipeline; the timeline has the new meeting");
        var cancelled = ACI.Application.Services.MeetingBookingService.Pipeline(fresh, starts, cancelled: true)!;
        cancelled.Should().NotContain("meetingDate").And.Contain("\"contactOutcome\":\"follow_up\"");
        ACI.Application.Services.MeetingBookingService.Pipeline("""{"meetingDate":"2026-12-10"}""", starts, cancelled: true)
            .Should().BeNull("another meeting's day is left alone");
    }
}
