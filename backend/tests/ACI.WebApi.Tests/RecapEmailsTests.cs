using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The workspace's recap emails: chosen per person by those who manage the settings and switched
/// by each person, sent daily, weekly and monthly from 07:00 on the workspace's calendar through
/// the outbox, once per person, frequency and period, to people who see every deal, with the
/// sales funnel and the club's figures beside the period before's, in the recipient's language.
/// </summary>
public class RecapEmailsTests
{
    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf("Europe/Zurich");

    /// <summary>Tuesday 24 November 2026, 07:30 in Zurich (CET): Monday's daily recap is due.</summary>
    private static readonly DateTime TuesdayMorning = new(2026, 11, 24, 6, 30, 0, DateTimeKind.Utc);

    /// <summary>Monday 23 November 2026, 07:30 in Zurich: Sunday's daily and last week's weekly are due.</summary>
    private static readonly DateTime MondayMorning = new(2026, 11, 23, 6, 30, 0, DateTimeKind.Utc);

    /// <summary>When the tests' recaps were switched on: before any period they look at.</summary>
    private static readonly DateTime LongAgo = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed record World(CustomWebApplicationFactory App, Guid Org, HttpClient Owner, Guid OwnerId) : IDisposable
    {
        public void Dispose() => App.Dispose();
    }

    private static async Task<World> WorldAsync(string name = "Pavillon 46")
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, $"olga@{name.Replace(" ", "").ToLowerInvariant()}.ch");
        var org = await CreateAsync(app, ownerId, name);
        return new World(app, org, owner.In(org), ownerId);
    }

    /// <summary>Someone on the team with a preset (and the role it goes with).</summary>
    private static async Task<(HttpClient Client, Guid UserId)> JoinAsync(World w, string email, string preset, string? language = null)
    {
        var (client, userId) = await SignInAsync(w.App, email);
        var role = preset switch { "manager" => OrgMemberRole.Manager, "read_only" or "read_only_accountant" => OrgMemberRole.Viewer, _ => OrgMemberRole.Member };
        using var scope = w.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = w.Org, UserId = userId, Role = role, PermissionPreset = preset, JoinedAtUtc = DateTime.UtcNow });
        if (language != null) db.Users.Single(u => u.Id == userId).Language = language;
        await db.SaveChangesAsync();
        return (client.In(w.Org), userId);
    }

    /// <summary>Recaps switched on long before the periods the test looks at.</summary>
    private static Task SubscribeAsync(World w, Guid userId, bool daily = true, bool weekly = false, bool monthly = false, DateTime? since = null) =>
        SaveAsync(w, new RecapSubscription
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, UserId = userId,
            DailySinceUtc = daily ? since ?? LongAgo : null, WeeklySinceUtc = weekly ? since ?? LongAgo : null, MonthlySinceUtc = monthly ? since ?? LongAgo : null,
            CreatedAtUtc = LongAgo, UpdatedAtUtc = LongAgo,
        });

    private static async Task SaveAsync(World w, params object[] rows)
    {
        using var scope = w.App.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static Lead Lead(World w, string name, DateTime created, string? source = null, Guid? referredBy = null, string status = "New", string? pipeline = null) => new()
    {
        Id = Guid.NewGuid(), UserId = w.OwnerId, OrganizationId = w.Org, Name = name, Email = $"{name.Split(' ')[0].ToLowerInvariant()}@example.ch",
        Status = status, Source = source, ReferredByContactId = referredBy, CreatedAtUtc = created, PipelineState = pipeline,
    };

    private static async Task<int> SendAsync(World w, DateTime now)
    {
        using var scope = w.App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecapEmails>().SendDueAsync(now);
    }

    private static Task<List<OutboundMessage>> RecapsAsync(World w) =>
        w.App.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.Recap).OrderBy(m => m.CreatedAtUtc).ToList());

    private static Task<List<RecapSend>> SendsAsync(World w) =>
        w.App.Services.Read(db => db.RecapSends.OrderBy(r => r.PeriodStart).ToList());

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should work, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static DateTime Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ThoseWhoManageTheSettingsChooseWhoGetsWhich_AndOnlyPeopleWhoSeeAllDealsMayGetOne()
    {
        using var w = await WorldAsync();
        var (_, managerId) = await JoinAsync(w, "max@pavillon46.ch", "manager");
        var (founder, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        var (rep, repId) = await JoinAsync(w, "rita@pavillon46.ch", "sales_rep");
        await JoinAsync(w, "books@pavillon46.ch", "read_only_accountant");

        var list = await Ok(await w.Owner.GetAsync("/api/recaps/recipients"), "the owner reads who gets recaps");
        list.GetProperty("people").EnumerateArray().Select(p => p.GetProperty("userId").GetGuid())
            .Should().Equal(new[] { w.OwnerId, managerId, founderId }, "the owner first, then by name; only people who see all deals");
        list.GetProperty("notEligible").GetInt32().Should().Be(2, "the sales rep and the accountant do not see all deals");
        list.GetProperty("timeZone").GetString().Should().Be("Europe/Zurich");

        var set = await Ok(await w.Owner.PutAsync($"/api/recaps/recipients/{founderId}", Body(new { daily = true, weekly = true, monthly = true })), "switch the founder's recaps on");
        (set.GetProperty("daily").GetBoolean(), set.GetProperty("weekly").GetBoolean(), set.GetProperty("monthly").GetBoolean()).Should().Be((true, true, true));
        set.GetProperty("preset").GetString().Should().Be("read_only");

        var refused = await w.Owner.PutAsync($"/api/recaps/recipients/{repId}", Body(new { daily = true }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Recap.NotEligible").And.Contain("See all deals");
        await Ok(await w.Owner.PutAsync($"/api/recaps/recipients/{repId}", Body(new { daily = false })), "switching off is never refused");
        (await w.Owner.PutAsync($"/api/recaps/recipients/{Guid.NewGuid()}", Body(new { daily = true }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await rep.GetAsync("/api/recaps/recipients")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "it takes settings.manage");
        (await rep.PutAsync($"/api/recaps/recipients/{repId}", Body(new { daily = true }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The founder, read-only, switches their own: Settings › Notifications.
        var mine = await Ok(await founder.GetAsync("/api/settings/recaps"), "the founder reads their recaps");
        (mine.GetProperty("eligible").GetBoolean(), mine.GetProperty("daily").GetBoolean(), mine.GetProperty("weekly").GetBoolean()).Should().Be((true, true, true));
        mine = await Ok(await founder.PutAsync("/api/settings/recaps", Body(new { daily = false })), "the founder switches the daily off");
        (mine.GetProperty("daily").GetBoolean(), mine.GetProperty("weekly").GetBoolean(), mine.GetProperty("monthly").GetBoolean()).Should().Be((false, true, true));
        var row = await w.App.Services.Read(db => db.RecapSubscriptions.Single(s => s.UserId == founderId));
        row.DailySinceUtc.Should().BeNull();
        row.WeeklySinceUtc.Should().NotBeNull();
        row.UpdatedByUserId.Should().Be(founderId);
        var forRep = await rep.PutAsync("/api/settings/recaps", Body(new { weekly = true }));
        forRep.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a sales rep does not see all deals");
        (await Ok(await rep.GetAsync("/api/settings/recaps"), "the rep reads theirs")).GetProperty("eligible").GetBoolean().Should().BeFalse();

        list = await Ok(await w.Owner.GetAsync("/api/recaps/recipients"), "the owner reads it again");
        var founderRow = list.GetProperty("people").EnumerateArray().Single(p => p.GetProperty("userId").GetGuid() == founderId);
        (founderRow.GetProperty("daily").GetBoolean(), founderRow.GetProperty("weekly").GetBoolean()).Should().Be((false, true), "the founder's own choice shows here");
    }

    [Fact]
    public async Task EachPersonGetsTheDaysRecapOnce_FromSevenOnTheWorkspacesCalendar()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        await SubscribeAsync(w, w.OwnerId);
        await SubscribeAsync(w, founderId);
        await SaveAsync(w, Lead(w, "Jean Dupont", Utc(11, 23, 10), "pavillon46.ch"));

        (await SendAsync(w, TuesdayMorning.AddMinutes(-31))).Should().Be(0, "06:59: Sunday's is the latest due, and nothing happened on Sunday");
        (await SendAsync(w, TuesdayMorning)).Should().Be(2);
        (await SendAsync(w, TuesdayMorning.AddMinutes(15))).Should().Be(0, "each has had Monday's");
        var restarted = new RecapBackgroundService(w.App.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(), NullLogger<RecapBackgroundService>.Instance);
        (await restarted.RunAsync(TuesdayMorning.AddHours(2), default)).Should().Be(0, "a restart sends nothing twice");

        var recaps = await RecapsAsync(w);
        recaps.Select(m => m.To).Should().BeEquivalentTo("olga@pavillon46.ch", "miguel@pavillon46.ch");
        var sends = await SendsAsync(w);
        sends.Where(s => s.Status == RecapSendStatuses.Nothing).Select(s => s.PeriodStart).Should().AllBeEquivalentTo(new DateOnly(2026, 11, 22));
        sends.Where(s => s.Status == RecapSendStatuses.Sent).Should().HaveCount(2).And.OnlyContain(s => s.PeriodStart == new DateOnly(2026, 11, 23) && s.Frequency == RecapFrequencies.Daily);
        var mail = recaps.Single(m => m.To == "miguel@pavillon46.ch");
        sends.Single(s => s.UserId == founderId && s.Status == RecapSendStatuses.Sent).OutboundMessageId.Should().Be(mail.Id);
        mail.SourceId.Should().Be(sends.Single(s => s.UserId == founderId && s.Status == RecapSendStatuses.Sent).Id);
        mail.Status.Should().Be(OutboundStatuses.Queued);
        mail.FromName.Should().Be("Pavillon 46", "from the workspace's sender");
        mail.UnsubscribeUrl.Should().BeNull("for the team: they switch it off in their settings, which it links to");
        mail.HtmlBody.Should().StartWith("<!DOCTYPE html>").And.Contain("pavillon46.ch");
        mail.TextBody.Should().Contain("/settings?tab=notifications").And.Contain("pavillon46.ch");

        // Tuesday's goes on Wednesday, when Tuesday had something.
        await SaveAsync(w, Lead(w, "Anne Martin", Utc(11, 24, 12), "pavillon46.ch"));
        (await SendAsync(w, TuesdayMorning.AddDays(1))).Should().Be(2);
    }

    [Fact]
    public async Task ARecapMissedWhileTheAppWasDownGoesOnce_AndOnlyTheLatestOfEachFrequency()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        await SubscribeAsync(w, founderId, daily: true, weekly: true, monthly: true);
        await SaveAsync(w,
            Lead(w, "October Lead", Utc(10, 15, 12)),
            Lead(w, "Sunday Lead", Utc(11, 22, 12)),
            Lead(w, "Monday Lead", Utc(11, 23, 12)),
            Lead(w, "Tuesday Lead", Utc(11, 24, 12)));

        // Back up on Wednesday at ten: Tuesday's daily, last week's weekly and October's monthly.
        (await SendAsync(w, Utc(11, 25, 9))).Should().Be(3);

        var sends = await SendsAsync(w);
        sends.Select(s => (s.Frequency, s.PeriodStart)).Should().BeEquivalentTo(new[]
        {
            (RecapFrequencies.Daily, new DateOnly(2026, 11, 24)),
            (RecapFrequencies.Weekly, new DateOnly(2026, 11, 16)),
            (RecapFrequencies.Monthly, new DateOnly(2026, 10, 1)),
        }, "Monday's daily, missed while the app was down, is never sent: only the latest of each");
        (await SendAsync(w, Utc(11, 25, 10))).Should().Be(0);
        (await RecapsAsync(w)).Should().HaveCount(3);
    }

    [Fact]
    public async Task SomeoneWhoLosesDealsAllOrLeavesTheWorkspaceGetsNone()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        var (_, managerId) = await JoinAsync(w, "max@pavillon46.ch", "manager");
        var (_, goneId) = await JoinAsync(w, "gone@pavillon46.ch", "read_only");
        foreach (var id in new[] { w.OwnerId, founderId, managerId, goneId }) await SubscribeAsync(w, id);
        await SaveAsync(w, Lead(w, "Jean Dupont", Utc(11, 23, 10)));

        // The founder becomes a sales rep: no deals.all, so no recap.
        await Ok(await w.Owner.PutAsync($"/api/organizations/{w.Org}/members/{founderId}/permissions", Body(new { preset = "sales_rep" })), "make the founder a sales rep");
        // The manager leaves through the Team page: their recaps go with them.
        await Ok(await w.Owner.DeleteAsync($"/api/organizations/{w.Org}/members/{managerId}"), "remove the manager");
        (await w.App.Services.Read(db => db.RecapSubscriptions.Any(s => s.UserId == managerId))).Should().BeFalse();
        // And someone whose membership went another way, their row left behind.
        using (var scope = w.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrganizationMembers.Remove(db.OrganizationMembers.Single(m => m.OrganizationId == w.Org && m.UserId == goneId));
            await db.SaveChangesAsync();
        }

        (await SendAsync(w, TuesdayMorning)).Should().Be(1);
        (await RecapsAsync(w)).Select(m => m.To).Should().Equal("olga@pavillon46.ch");
    }

    [Fact]
    public async Task ARecipientWhoSwitchedAFrequencyOffGetsOnlyTheOthers_AndOneSwitchedOnLateStartsWithTheNext()
    {
        using var w = await WorldAsync();
        var (founder, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        await SubscribeAsync(w, founderId, daily: true, weekly: true);
        await Ok(await founder.PutAsync("/api/settings/recaps", Body(new { weekly = false })), "the founder switches the weekly off");
        // Switched on after October's fell due, on 1 November: the first monthly is November's.
        using (var scope = w.App.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.RecapSubscriptions.Single(s => s.UserId == founderId).MonthlySinceUtc = Utc(11, 2, 9);
            await db.SaveChangesAsync();
        }
        await SaveAsync(w, Lead(w, "October Lead", Utc(10, 20, 12)), Lead(w, "Sunday Lead", Utc(11, 22, 12)));

        (await SendAsync(w, MondayMorning)).Should().Be(1);

        (await SendsAsync(w)).Select(s => s.Frequency).Should().Equal(RecapFrequencies.Daily);
        (await RecapsAsync(w)).Single().Subject.Should().Contain("Sonntag, 22. November 2026", "a daily recap, in German for a de-CH workspace");
    }

    [Fact]
    public async Task EachFigureIsCountedFromTheRecords_BesideTheWeekBefore()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        var (_, repId) = await JoinAsync(w, "rita@pavillon46.ch", "sales_rep");
        // The week of 16 to 22 November, beside 9 to 15 November.
        DateTime Now(int day, int hour = 10) => Utc(11, day, hour);
        DateTime Before(int day, int hour = 10) => Utc(11, day, hour);
        var anna = new Contact { Id = Guid.NewGuid(), UserId = w.OwnerId, OrganizationId = w.Org, Name = "Anna Muster", Email = "anna@example.ch", CreatedAtUtc = LongAgo };
        var bob = new Contact { Id = Guid.NewGuid(), UserId = w.OwnerId, OrganizationId = w.Org, Name = "Bob Keller", Email = "bob@example.ch", CreatedAtUtc = LongAgo };
        const string meeting = """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-30"}""";
        const string met = """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-18","meetingAttended":true,"stillInterested":true}""";
        const string notInterested = """{"outreachStatus":"contacted","contactOutcome":"not_interested"}""";
        var l1 = Lead(w, "Lea One", Now(16), "pavillon46.ch", anna.Id);
        var l2 = Lead(w, "Lou Two", Now(17), "pavillon46.ch", status: "Lost / Not Interested");
        var l3 = Lead(w, "Lia Three", Now(18), "Referral", pipeline: meeting);
        var l4 = Lead(w, "Leo Four", Now(19), pipeline: met);
        var l5 = Lead(w, "Lars Five", Now(22, 22), "Instagram", bob.Id, pipeline: notInterested);
        var p1 = Lead(w, "Paul Prior", Before(10), "pavillon46.ch");
        var p2 = Lead(w, "Pia Prior", Before(15, 22), "Instagram", anna.Id, pipeline: meeting);
        var gone = Lead(w, "Old Converted", LongAgo);
        gone.IsConverted = true;
        gone.ConvertedAtUtc = Now(20);
        var goneBefore = Lead(w, "Older Converted", LongAgo);
        goneBefore.IsConverted = true;
        goneBefore.ConvertedAtUtc = Before(12);
        var next = Lead(w, "Next Week", Utc(11, 23, 1));
        await SaveAsync(w, anna, bob, l1, l2, l3, l4, l5, p1, p2, gone, goneBefore, next);

        OutboundMessage Mail(string source, string status, DateTime? sent) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, Channel = OutboundChannels.Email, Source = source, Status = status,
            To = "x@example.ch", Subject = "Rendez-vous", CreatedAtUtc = sent ?? Now(18), SentAtUtc = sent,
        };
        MeetingBooking Booking(DateTime created, DateTime starts, string status, DateTime? updated = null) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, LeadId = l3.Id, BookingId = Guid.NewGuid().ToString("N"), StartsAtUtc = starts,
            Status = status, CreatedAtUtc = created, UpdatedAtUtc = updated ?? created,
        };
        Contract Contract(DateTime? sent, DateTime? clientSigned, DateTime? countersigned = null, bool uploaded = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, LeadId = l4.Id, Status = clientSigned == null ? ContractStatuses.Sent : ContractStatuses.Countersigned,
            Title = "Membership", Body = "…", CounterpartyName = "Leo Four", CounterpartyEmail = "leo@example.ch", CreatedByUserId = w.OwnerId,
            CreatedAtUtc = LongAgo, UpdatedAtUtc = LongAgo, SentAtUtc = sent, ClientSignedAtUtc = clientSigned, CounterSignedAtUtc = countersigned,
            SignedCopyAttachmentId = uploaded ? Guid.NewGuid() : null,
        };
        var c1 = Contract(Now(16), Now(17), Now(17));
        // Signed on paper in the week before, its copy uploaded this week: counted this week.
        var c2 = Contract(null, Before(14).Date, Now(18), uploaded: true);
        var c3 = Contract(null, null);
        var c4 = Contract(Before(10), Before(11));
        ContractDeposit Deposit(Contract c, decimal amount, string currency, DateTime? paid, decimal? amountPaid = null) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, ContractId = c.Id, Amount = amount, Currency = currency,
            Status = paid == null ? ContractDepositStatuses.Awaiting : ContractDepositStatuses.Paid, AmountPaid = amountPaid, PaidAtUtc = paid, CreatedAtUtc = LongAgo,
        };
        Deal Won(string value, string currency, DateTime? closed, bool? isWon = true, Guid? owner = null, bool isPrivate = false) => new()
        {
            Id = Guid.NewGuid(), UserId = owner ?? w.OwnerId, OrganizationId = w.Org, Name = "Membership " + value, Value = value, Currency = currency,
            IsWon = isWon, ClosedAtUtc = closed, CreatedAtUtc = LongAgo, IsPrivate = isPrivate,
        };
        ClubMember Member(string first, DateTime joined, string status = "Active", DateTime? renews = null, bool erased = false) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, OwnerUserId = w.OwnerId, FirstName = first, LastName = "Member", Email = $"{first.ToLowerInvariant()}@example.ch",
            Tier = "Founding", Status = status, JoinedAtUtc = joined, RenewsAtUtc = renews ?? joined.AddYears(1), ErasedAtUtc = erased ? Now(20) : null,
        };
        var m1 = Member("Mia", Now(17), renews: MondayMorning.AddDays(10));
        var m2 = Member("Max", Before(12), status: "Lapsed", renews: MondayMorning.AddDays(40));
        var m3 = Member("Erased", Now(18), erased: true);
        var e1 = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = w.Org, Name = "Wine evening", StartAtUtc = Now(19, 18), EndAtUtc = Now(19, 21), Status = ClubEventStatuses.Published, CreatedAtUtc = LongAgo, UpdatedAtUtc = LongAgo };
        var e2 = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = w.Org, Name = "Cancelled talk", StartAtUtc = Now(20, 18), EndAtUtc = Now(20, 20), Status = ClubEventStatuses.Cancelled, CreatedAtUtc = LongAgo, UpdatedAtUtc = LongAgo };
        var e3 = new ClubEvent { Id = Guid.NewGuid(), OrganizationId = w.Org, Name = "Golf day", StartAtUtc = Before(12, 9), EndAtUtc = Before(12, 17), Status = ClubEventStatuses.Completed, CreatedAtUtc = LongAgo, UpdatedAtUtc = LongAgo };
        ClubEventRegistration Seat(ClubEvent e, string status) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, EventId = e.Id, MemberId = m1.Id, Name = "Mia Member", Status = status, RegisteredAtUtc = LongAgo,
        };
        ClubVisit Visit(DateTime at) => new() { Id = Guid.NewGuid(), OrganizationId = w.Org, MemberId = m1.Id, MemberName = "Mia Member", VisitedAtUtc = at };
        await SaveAsync(w,
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = l2.Id, OrganizationId = w.Org, FromStatus = "New", ToStatus = "Lost / Not Interested", ChangedAtUtc = Now(18) },
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = l2.Id, OrganizationId = w.Org, FromStatus = "Contacted", ToStatus = "Lost / Not Interested", ChangedAtUtc = Now(19) },
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = l4.Id, OrganizationId = w.Org, FromStatus = "New", ToStatus = "Contacted", ChangedAtUtc = Now(18) },
            new LeadStatusEvent { Id = Guid.NewGuid(), LeadId = p1.Id, OrganizationId = w.Org, FromStatus = "New", ToStatus = "Not interested", ChangedAtUtc = Before(13) },
            Mail(OutboundSources.Newsletter, OutboundStatuses.Sent, Now(17)), Mail(OutboundSources.Newsletter, OutboundStatuses.Sent, Now(17)),
            Mail(OutboundSources.Newsletter, OutboundStatuses.Sent, Now(18)), Mail(OutboundSources.Newsletter, OutboundStatuses.Sent, Before(10)),
            Mail(OutboundSources.Newsletter, OutboundStatuses.Queued, null), Mail(OutboundSources.Composer, OutboundStatuses.Sent, Now(17)),
            Booking(Now(16), Utc(11, 26, 14), MeetingBookingStatuses.Booked),
            Booking(Before(13), Now(18, 14), MeetingBookingStatuses.Booked),
            Booking(Before(14), Now(19, 14), MeetingBookingStatuses.Cancelled, updated: Now(17)),
            Booking(Now(20), Utc(11, 27, 14), MeetingBookingStatuses.Cancelled, updated: Now(21)),
            c1, c2, c3, c4,
            Deposit(c1, 5000m, "CHF", Now(17), amountPaid: 5000m), Deposit(c2, 1000m, "EUR", Now(19)), Deposit(c4, 2000m, "CHF", Before(11)), Deposit(c3, 3000m, "CHF", null),
            Won("45000", "CHF", Now(18)), Won("20000", "EUR", Now(19)), Won("on request", "CHF", Now(20)), Won("12000", "CHF", Before(11)),
            Won("9000", "CHF", Now(18), isWon: false), Won("7000", "", Now(21), owner: repId, isPrivate: true),
            m1, m2, m3, e1, e2, e3,
            Seat(e1, RegistrationStatuses.CheckedIn), Seat(e1, RegistrationStatuses.CheckedIn), Seat(e1, RegistrationStatuses.Reserved),
            Seat(e2, RegistrationStatuses.CheckedIn), Seat(e3, RegistrationStatuses.CheckedIn),
            Visit(Now(16)), Visit(Now(18)), Visit(Now(22, 20)), Visit(Before(9)),
            new ClubMemberRisk { Id = Guid.NewGuid(), OrganizationId = w.Org, MemberId = m1.Id, Score = 70, Level = RiskLevels.AtRisk, ComputedAtUtc = Now(22) },
            new ClubMemberRisk { Id = Guid.NewGuid(), OrganizationId = w.Org, MemberId = m2.Id, Score = 80, Level = RiskLevels.AtRisk, ComputedAtUtc = Now(22) });

        RecapFigures f;
        using (var scope = w.App.Services.CreateScope())
            f = await scope.ServiceProvider.GetRequiredService<IRecapEmails>()
                .FiguresAsync(w.Org, founderId, RecapPeriods.LatestDue(RecapFrequencies.Weekly, MondayMorning, Zurich), MondayMorning);

        f.Period.FirstDay.Should().Be(new DateOnly(2026, 11, 16));
        var s = f.Sales!;
        s.NewLeads.Should().Be(new RecapCount(5, 2), "Lars came at 23:00 on Sunday in Zurich, Pia at 23:00 on the Sunday before");
        s.Sources.Should().Equal(
            new RecapNamedCount("pavillon46.ch", 2, 1), new RecapNamedCount("Instagram", 1, 1),
            new RecapNamedCount("", 1, 0), new RecapNamedCount("Referral", 1, 0));
        s.OtherSources.Should().Be(RecapCount.Zero);
        s.Referred.Should().Be(new RecapCount(3, 1), "Lea and Lars were referred by someone, Lia came by the source Referral");
        s.Referrers.Should().Equal(new RecapNamedCount("Anna Muster", 1, 1), new RecapNamedCount("Bob Keller", 1, 0));
        s.Lost.Should().Be(new RecapCount(1, 1), "Lou, marked lost twice, counts once");
        s.Invitations.Should().Be(new RecapCount(3, 1));
        s.MeetingsBooked.Should().Be(new RecapCount(2, 2));
        s.MeetingsHeld.Should().Be(new RecapCount(1, 0), "one took place; the other was cancelled");
        s.MeetingsCancelled.Should().Be(new RecapCount(2, 0));
        s.ContractsSent.Should().Be(new RecapCount(1, 1));
        s.ContractsSigned.Should().Be(new RecapCount(2, 1), "the copy uploaded this week counts this week");
        s.Deposits.Should().Be(new RecapCount(2, 1));
        s.DepositAmounts.Should().Equal(new RecapAmount("CHF", 5000m, 2000m), new RecapAmount("EUR", 1000m, 0m));
        s.Converted.Should().Be(new RecapCount(1, 1));
        s.Won.Should().Be(new RecapCount(4, 1), "the colleague's private deal counts for someone who sees every deal");
        s.WonValue.Should().Equal(new RecapAmount("CHF", 52000m, 12000m), new RecapAmount("EUR", 20000m, 0m));
        s.WonWithoutValue.Should().Be(1);
        // Open leads now: Lea, Paul and Next Week at outreach; Lia and Pia at the meeting; Leo at the
        // contract. Lou (lost status) and Lars (not interested) have dropped out; the converted are gone.
        s.PipelineNow.Should().Equal(3, 2, 1, 0, 0);

        f.NewMembers.Should().Be(new RecapCount(1, 1), "the erased member counts in none of it");
        var club = f.Club!;
        club.Visits.Should().Be(new RecapCount(3, 1));
        club.Events.Should().Be(new RecapCount(1, 1), "the cancelled talk was not held");
        club.Attendance.Should().Be(new RecapCount(2, 1));
        club.AtRiskNow.Should().Be(1, "the lapsed member is not counted");
        club.RenewalsDue.Should().Be(1);
        f.HasNews.Should().BeTrue();
    }

    [Fact]
    public async Task TheRecapIsWrittenInEachRecipientsLanguage()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only", language: "fr");
        var (_, managerId) = await JoinAsync(w, "max@pavillon46.ch", "manager", language: "en");
        foreach (var id in new[] { w.OwnerId, founderId, managerId }) await SubscribeAsync(w, id);
        await SaveAsync(w, Lead(w, "Jean Dupont", Utc(11, 23, 10), "pavillon46.ch"));

        (await SendAsync(w, TuesdayMorning)).Should().Be(3);

        var recaps = await RecapsAsync(w);
        var french = recaps.Single(m => m.To == "miguel@pavillon46.ch");
        french.Subject.Should().Be("Pavillon 46 · Récapitulatif du jour · lundi 23 novembre 2026");
        french.TextBody.Should().Contain("- Nouveaux prospects\u00a0: Jour 1, Veille 0");
        french.HtmlBody.Should().Contain("<html lang=\"fr\"").And.Contain(">Veille<");
        recaps.Single(m => m.To == "max@pavillon46.ch").Subject.Should().Be("Pavillon 46 · Daily recap · Monday 23 November 2026");
        recaps.Single(m => m.To == "olga@pavillon46.ch").Subject
            .Should().Be("Pavillon 46 · Tagesrückblick · Montag, 23. November 2026", "with no language of their own, the workspace's (de-CH)");
    }

    [Fact]
    public async Task WithSalesOffTheSalesPartsAreLeftOut_AndAPeriodWithNothingToReportSendsNothing()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only", language: "en");
        await SubscribeAsync(w, founderId);
        using (var scope = w.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Organizations.Single(o => o.Id == w.Org).SalesModuleOff = true;
            await db.SaveChangesAsync();
        }
        var member = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, OwnerUserId = w.OwnerId, FirstName = "Mia", LastName = "Member", Email = "mia@example.ch",
            Tier = "Founding", Status = "Active", JoinedAtUtc = LongAgo, RenewsAtUtc = LongAgo.AddYears(2),
        };
        // A lead on Monday: the Sales module's, so not in the recap of a workspace that turned it off.
        await SaveAsync(w, member, Lead(w, "Jean Dupont", Utc(11, 23, 10)));

        (await SendAsync(w, TuesdayMorning)).Should().Be(0);
        (await SendsAsync(w)).Single().Status.Should().Be(RecapSendStatuses.Nothing);

        await SaveAsync(w, new ClubVisit { Id = Guid.NewGuid(), OrganizationId = w.Org, MemberId = member.Id, MemberName = "Mia Member", VisitedAtUtc = Utc(11, 24, 12) });
        (await SendAsync(w, TuesdayMorning.AddDays(1))).Should().Be(1);
        var text = (await RecapsAsync(w)).Single().TextBody;
        text.Should().Contain("- Visits: Day 1, Day before 0").And.NotContain("New leads").And.NotContain("/dashboard");
    }

    [Fact]
    public async Task AnotherWorkspacesRecapsAreInvisibleAndCannotBeChanged()
    {
        using var w = await WorldAsync();
        var (_, founderId) = await JoinAsync(w, "miguel@pavillon46.ch", "read_only");
        await SubscribeAsync(w, founderId);
        await SaveAsync(w, new RecapSend
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, UserId = founderId, Frequency = RecapFrequencies.Daily, PeriodStart = new DateOnly(2026, 11, 23),
            Status = RecapSendStatuses.Sent, CreatedAtUtc = TuesdayMorning, SentAtUtc = TuesdayMorning,
        });
        var (other, otherOwner) = await SignInAsync(w.App, "owner@cercle.example");
        var otherOrg = await CreateAsync(w.App, otherOwner, "Cercle du Lac");
        other.In(otherOrg);

        (await other.PutAsync($"/api/recaps/recipients/{founderId}", Body(new { daily = false })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "the founder is not on the other workspace's team");
        var theirs = await Ok(await other.GetAsync("/api/recaps/recipients"), "the other owner reads their own list");
        theirs.GetProperty("people").EnumerateArray().Select(p => p.GetProperty("userId").GetGuid()).Should().Equal(otherOwner);
        (await w.App.Services.Read(db => db.RecapSubscriptions.Single(s => s.UserId == founderId))).DailySinceUtc.Should().NotBeNull("nothing changed it");

        using var scope = w.App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using (context.ActFor(otherOrg, otherOwner))
        {
            (await context.RecapSubscriptions.CountAsync()).Should().Be(0);
            (await context.RecapSends.CountAsync()).Should().Be(0);
        }
        using (context.ActFor(w.Org, w.OwnerId))
        {
            (await context.RecapSubscriptions.CountAsync()).Should().Be(1);
            (await context.RecapSends.CountAsync()).Should().Be(1);
        }
    }
}
