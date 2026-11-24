using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// "Invite members" on an event: an audience of members, a letter in each member's language,
/// and a reply link of their own to a public page where they answer "I'll come" (registered by
/// the event's own rules: a seat, else the waitlist) or "I can't come", until the event starts;
/// the list of who answered, one reminder for those who did not, and the expected attendance.
/// </summary>
public class EventInvitationTests
{
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FrontendBaseUrl"] = "https://crm.example.ch",
            }));
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }

        public async Task SeedAsync(Action<ACI.Infrastructure.Persistence.AppDbContext> change)
        {
            using var scope = Services.CreateScope();
            await using var db = scope.ServiceProvider.EveryWorkspace();
            change(db);
            await db.SaveChangesAsync();
        }
    }

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org);

    private static readonly Regex ReplyLink = new(@"https://crm\.example\.ch/invitation/(?<token>[A-Za-z0-9_-]{43})");

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<Club> ClubAsync(CustomWebApplicationFactory app, string email = "olga@pavillon46.ch", string name = "Pavillon 46")
    {
        var (owner, ownerId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, ownerId, name);
        owner.In(org);
        return new Club(owner, ownerId, org);
    }

    private static async Task<Guid> MemberAsync(Club club, string first, string? language = null, bool consent = true)
    {
        var id = (await Json(await club.Owner.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active", preferredLanguage = language,
        })), $"add {first}")).GetProperty("id").GetGuid();
        if (consent)
            await Json(await club.Owner.PostAsync($"/api/members/{id}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })), $"{first} agrees");
        return id;
    }

    /// <summary>A published event a week from now.</summary>
    private static async Task<Guid> EventAsync(Club club, int capacity = 0, decimal memberPrice = 0, string name = "Wine night")
    {
        var start = DateTime.UtcNow.AddDays(7);
        return (await Json(await club.Owner.PostAsync("/api/events", Body(new
        {
            name, location = "Library Bar", startAtUtc = start, endAtUtc = start.AddHours(3), capacity, memberPrice, status = "Published",
        })), "add the event")).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> SendAsync(Club club, Guid eventId, Guid sendId, object? audience = null, object? letters = null, int? expectedRecipients = null) =>
        club.Owner.PostAsync($"/api/events/{eventId}/invitations/send", Body(new { sendId, audience = audience ?? new { }, letters, expectedRecipients }));

    /// <summary>The reply links that went to a member, oldest first, read from their emails in the outbox.</summary>
    private static async Task<List<string>> LinksOfAsync(App app, Guid memberId) =>
        (await app.Services.Read(db => db.OutboundMessages
                .Where(m => m.MemberId == memberId && m.Source == OutboundSources.Invitation)
                .OrderBy(m => m.CreatedAtUtc).Select(m => m.Body).ToList()))
            .Select(body => ReplyLink.Match(body).Groups["token"].Value)
            .ToList();

    private static async Task<string> LinkOfAsync(App app, Guid memberId) => (await LinksOfAsync(app, memberId)).Single();

    private static Task<HttpResponseMessage> PageAsync(HttpClient anonymous, string token) => anonymous.GetAsync($"/api/public/invitations/{token}");

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient anonymous, string token, string answer) =>
        anonymous.PostAsync($"/api/public/invitations/{token}", Body(new { answer }));

    private static async Task<List<JsonElement>> ListAsync(Club club, Guid eventId) =>
        (await Json(await club.Owner.GetAsync($"/api/events/{eventId}/invitations"), "the invitations")).GetProperty("invitations").EnumerateArray().ToList();

    private static async Task<string> StateOf(Club club, Guid eventId, Guid memberId) =>
        (await ListAsync(club, eventId)).Single(i => i.GetProperty("memberId").GetGuid() == memberId).GetProperty("state").GetString()!;

    private static async Task<List<JsonElement>> AttendeesAsync(Club club, Guid eventId) =>
        (await Json(await club.Owner.GetAsync($"/api/events/{eventId}/attendees"), "attendees")).EnumerateArray().ToList();

    [Fact]
    public async Task InvitingSendsEachMemberTheirOwnReplyLinkInTheirLanguage_AndTheSameSendIdSendsNothingMore()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno", "de");
        var clara = await MemberAsync(club, "Clara", "fr");
        await MemberAsync(club, "Dario", consent: false);
        var eventId = await EventAsync(club, memberPrice: 40);

        var audience = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/audience", Body(new { statuses = new[] { "Active" } })), "count");
        audience.GetProperty("willReceive").GetInt32().Should().Be(3);
        audience.GetProperty("noConsent").GetInt32().Should().Be(1, "invitations are marketing");

        var sendId = Guid.NewGuid();
        var sent = await Json(await SendAsync(club, eventId, sendId, new { statuses = new[] { "Active" } }, expectedRecipients: 3), "send");
        sent.GetProperty("recipients").GetInt32().Should().Be(3);
        sent.GetProperty("leftOut").GetInt32().Should().Be(1);
        var again = await Json(await SendAsync(club, eventId, sendId, new { statuses = new[] { "Active" } }, expectedRecipients: 3), "the same press again");
        again.GetProperty("alreadySent").GetBoolean().Should().BeTrue();
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Invitation))).Should().Be(3, "the same send id sends once");

        var emails = await app.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.Invitation).ToList());
        emails.Should().OnlyContain(m => m.Purpose == ConsentPurposes.Marketing && m.NextAttemptAtUtc != null);
        var annas = emails.Single(m => m.MemberId == anna);
        annas.Subject.Should().Be("You are invited: Wine night");
        annas.Body.Should().Contain("Dear Anna,").And.Contain("Library Bar").And.Contain("CHF 40.00");
        var brunos = emails.Single(m => m.MemberId == bruno);
        brunos.Subject.Should().Be("Einladung: Wine night");
        brunos.Body.Should().Contain("Guten Tag Bruno,").And.Contain(" um ", "the date is written in German");
        emails.Single(m => m.MemberId == clara).Body.Should().Contain("Bonjour Clara,");
        // Each its own link; only the hash of each is kept.
        var tokens = new[] { await LinkOfAsync(app, anna), await LinkOfAsync(app, bruno), await LinkOfAsync(app, clara) };
        tokens.Distinct().Should().HaveCount(3);
        var hashes = await app.Services.Read(db => db.ClubEventInvitations.Select(i => i.TokenHash).ToList());
        hashes.Should().BeEquivalentTo(tokens.Select(InvitationReplyToken.Hash));
        hashes.Should().NotIntersectWith(tokens, "the token itself is never stored");
        // Each invitation is a line in the member's communications.
        (await Json(await club.Owner.GetAsync($"/api/communications?memberId={bruno}"), "Bruno's communications")).EnumerateArray()
            .Should().Contain(m => m.GetProperty("subject").GetString() == "Einladung: Wine night");

        // A second send leaves out who has been invited already.
        var second = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/audience", Body(new { statuses = new[] { "Active" } })), "count again");
        second.GetProperty("alreadyInvited").GetInt32().Should().Be(3);
        (await Refused(await SendAsync(club, eventId, Guid.NewGuid()), HttpStatusCode.BadRequest, "a second send to the same members")).Should().Contain("Invitation.Nobody");
    }

    [Fact]
    public async Task ThePreviewIsInTheMembersLanguage_AndALetterWithoutTheReplyLinkIsNotSent()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var bruno = await MemberAsync(club, "Bruno", "de");
        var eventId = await EventAsync(club);

        var setup = await Json(await club.Owner.GetAsync($"/api/events/{eventId}/invitations/setup"), "setup");
        setup.GetProperty("defaults").GetProperty("fr").GetProperty("subject").GetString().Should().Contain("{{eventName}}");
        setup.GetProperty("fields").EnumerateArray().Select(f => f.GetString()).Should().Contain(["firstName", "eventDate", "replyLink"]);

        var preview = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/preview", Body(new { sampleMemberId = bruno })), "preview as Bruno");
        preview.GetProperty("language").GetString().Should().Be("de");
        preview.GetProperty("body").GetString().Should().Contain("Guten Tag Bruno").And.Contain("Kostenlos").And.Contain("/invitation/sample");

        var edited = new { en = new { subject = "Join us", body = "Hello {{firstName}}, see you at {{eventName}}." } };
        (await Refused(await SendAsync(club, eventId, Guid.NewGuid(), letters: edited), HttpStatusCode.BadRequest, "no reply link"))
            .Should().Contain("Invitation.NoReplyLink");
        var unknown = new { en = new { subject = "Join us", body = "Hello {{nickname}}: {{replyLink}}" } };
        (await Refused(await SendAsync(club, eventId, Guid.NewGuid(), letters: unknown), HttpStatusCode.BadRequest, "an unknown placeholder"))
            .Should().Contain("Invitation.Unfilled");

        var test = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/test", Body(new { sampleMemberId = bruno })), "a test to me");
        test.GetProperty("to").GetString().Should().Be("olga@pavillon46.ch");
        app.Mail.SentTo.Should().Equal("olga@pavillon46.ch");
        (await app.Services.Read(db => db.ClubEventInvitations.Count())).Should().Be(0, "a test invites nobody");
    }

    [Fact]
    public async Task IllComeGivesASeat_ThenTheWaitlist_AndChangingToICantComePromotesTheWaitlist()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno", "de");
        var eventId = await EventAsync(club, capacity: 1, memberPrice: 40);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var visitor = app.CreateClient();
        var annasLink = await LinkOfAsync(app, anna);
        var brunosLink = await LinkOfAsync(app, bruno);

        var page = await Json(await PageAsync(visitor, brunosLink), "Bruno opens his link");
        page.GetProperty("language").GetString().Should().Be("de");
        page.GetProperty("firstName").GetString().Should().Be("Bruno");
        page.GetProperty("club").GetString().Should().Be("Pavillon 46");
        page.GetProperty("eventName").GetString().Should().Be("Wine night");
        page.GetProperty("memberPrice").GetDecimal().Should().Be(40);
        page.GetProperty("answer").GetString().Should().Be("none");
        page.GetRawText().Should().NotContain("anna", "the page says nothing about anyone else");

        var seat = await Json(await AnswerAsync(visitor, annasLink, "coming"), "Anna: I'll come");
        seat.GetProperty("place").GetString().Should().Be("seat");
        var wait = await Json(await AnswerAsync(visitor, brunosLink, "coming"), "Bruno: I'll come");
        (wait.GetProperty("place").GetString(), wait.GetProperty("waitlistPosition").GetInt32()).Should().Be(("waitlist", 1));
        (await Json(await AnswerAsync(visitor, annasLink, "coming"), "Anna again")).GetProperty("place").GetString().Should().Be("seat", "the same answer twice changes nothing");

        var list = await AttendeesAsync(club, eventId);
        list.Should().HaveCount(2);
        var annasPlace = list.Single(a => a.GetProperty("memberId").GetGuid() == anna);
        (annasPlace.GetProperty("status").GetString(), annasPlace.GetProperty("chargeState").GetString()).Should().Be(("Reserved", "charged"), "the ticket rules are the event's own");
        list.Single(a => a.GetProperty("memberId").GetGuid() == bruno).GetProperty("status").GetString().Should().Be("Waitlisted");
        (await StateOf(club, eventId, anna)).Should().Be("coming");
        (await StateOf(club, eventId, bruno)).Should().Be("waitlist");

        // Anna can't come after all: cancelled as staff would, her ticket taken back, and Bruno gets the seat and is told.
        (await Json(await AnswerAsync(visitor, annasLink, "declined"), "Anna: I can't come")).GetProperty("answer").GetString().Should().Be("declined");
        list = await AttendeesAsync(club, eventId);
        var cancelled = list.Single(a => a.GetProperty("memberId").GetGuid() == anna);
        (cancelled.GetProperty("status").GetString(), cancelled.GetProperty("chargeState").GetString()).Should().Be(("Cancelled", "reversed"));
        list.Single(a => a.GetProperty("memberId").GetGuid() == bruno).GetProperty("status").GetString().Should().Be("Reserved");
        (await app.Services.Read(db => db.OutboundMessages.Count(m => m.MemberId == bruno && m.Source == OutboundSources.Event))).Should().Be(1, "the promotion is emailed as it always is");
        (await Json(await PageAsync(visitor, brunosLink), "Bruno's page")).GetProperty("place").GetString().Should().Be("seat");
        (await StateOf(club, eventId, anna)).Should().Be("declined");

        // And changes her mind again: a new place, on the waitlist now.
        (await Json(await AnswerAsync(visitor, annasLink, "coming"), "Anna: I'll come after all")).GetProperty("place").GetString().Should().Be("waitlist");
    }

    [Fact]
    public async Task ICantComeRecordsTheDecline_AndTheListSaysWhereEachMemberStands_BeforeAndAfterTheEvent()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno");
        var clara = await MemberAsync(club, "Clara");
        var dario = await MemberAsync(club, "Dario");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var visitor = app.CreateClient();
        await Json(await AnswerAsync(visitor, await LinkOfAsync(app, anna), "coming"), "Anna comes");
        await Json(await AnswerAsync(visitor, await LinkOfAsync(app, bruno), "coming"), "Bruno comes");
        await Json(await AnswerAsync(visitor, await LinkOfAsync(app, clara), "declined"), "Clara can't");

        var all = await Json(await club.Owner.GetAsync($"/api/events/{eventId}/invitations"), "the invitations");
        var counts = all.GetProperty("counts");
        (counts.GetProperty("invited").GetInt32(), counts.GetProperty("coming").GetInt32(), counts.GetProperty("declined").GetInt32(), counts.GetProperty("noAnswer").GetInt32())
            .Should().Be((4, 2, 1, 1));
        (await StateOf(club, eventId, clara)).Should().Be("declined");
        (await AttendeesAsync(club, eventId)).Should().NotContain(a => a.GetProperty("memberId").GetGuid() == clara, "a decline takes no place");

        // After the event: who came and who did not, from the door.
        await app.SeedAsync(db => db.ClubEvents.Single(e => e.Id == eventId).StartAtUtc = DateTime.UtcNow.AddHours(-1));
        var places = await AttendeesAsync(club, eventId);
        string PlaceOf(Guid member) => places.Single(a => a.GetProperty("memberId").GetGuid() == member).GetProperty("id").GetString()!;
        await Json(await club.Owner.PutAsync($"/api/events/{eventId}/attendees/{PlaceOf(anna)}", Body(new { status = "CheckedIn" })), "Anna at the door");
        await Json(await club.Owner.PutAsync($"/api/events/{eventId}/attendees/{PlaceOf(bruno)}", Body(new { status = "NoShow" })), "Bruno did not come");
        (await StateOf(club, eventId, anna)).Should().Be("came");
        (await StateOf(club, eventId, bruno)).Should().Be("no_show");
        (await StateOf(club, eventId, dario)).Should().Be("no_answer");
        (await Json(await club.Owner.GetAsync($"/api/events/{eventId}/invitations"), "after")).GetProperty("open").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TheReplyPageRefusesOnceTheEventStarts_AndForACancelledEvent()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno");
        var started = await EventAsync(club, name: "Started");
        var cancelled = await EventAsync(club, name: "Cancelled");
        await Json(await SendAsync(club, started, Guid.NewGuid(), new { tiers = new[] { "Gold" } }), "invite to the first");
        await Json(await SendAsync(club, cancelled, Guid.NewGuid()), "invite to the second");
        var links = await LinksOfAsync(app, anna);
        var visitor = app.CreateClient();

        await app.SeedAsync(db => db.ClubEvents.Single(e => e.Id == started).StartAtUtc = DateTime.UtcNow.AddMinutes(-5));
        (await Refused(await PageAsync(visitor, links[0]), HttpStatusCode.Conflict, "an event that started")).Should().Contain("Invitation.Started.Conflict");
        await Refused(await AnswerAsync(visitor, links[0], "coming"), HttpStatusCode.Conflict, "an answer once it started");
        (await AttendeesAsync(club, started)).Should().BeEmpty();

        var version = (await Json(await club.Owner.GetAsync($"/api/events/{cancelled}"), "read")).GetProperty("version").GetString();
        await Json(await club.Owner.PutAsync($"/api/events/{cancelled}", Body(new { status = "Cancelled", version })), "cancel the event");
        (await Refused(await PageAsync(visitor, links[1]), HttpStatusCode.Conflict, "a cancelled event")).Should().Contain("Invitation.Cancelled.Conflict");
        await Refused(await AnswerAsync(visitor, (await LinksOfAsync(app, bruno))[1], "coming"), HttpStatusCode.Conflict, "an answer to a cancelled event");
    }

    [Fact]
    public async Task AWrongLinkAndAnotherWorkspacesLinkAnswerNothingHere()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var other = await ClubAsync(app, "zoe@other.ch", "Other club");
        var anna = await MemberAsync(club, "Anna");
        var zoe = await MemberAsync(other, "Zoe");
        var ours = await EventAsync(club);
        var theirs = await EventAsync(other, name: "Their gala");
        await Json(await SendAsync(club, ours, Guid.NewGuid()), "our send");
        await Json(await SendAsync(other, theirs, Guid.NewGuid()), "their send");
        var visitor = app.CreateClient();

        (await Refused(await PageAsync(visitor, InvitationReplyToken.Create()), HttpStatusCode.NotFound, "a token nobody was sent")).Should().Contain("Invitation.NotFound");
        await Refused(await AnswerAsync(visitor, "not-a-token", "coming"), HttpStatusCode.NotFound, "something not shaped like a token");
        await Refused(await AnswerAsync(visitor, await LinkOfAsync(app, anna), "maybe"), HttpStatusCode.BadRequest, "an answer that is neither");

        // Zoe's link answers for her invitation to her club's event, and nothing of ours.
        var page = await Json(await AnswerAsync(visitor, await LinkOfAsync(app, zoe), "coming"), "Zoe answers");
        (page.GetProperty("club").GetString(), page.GetProperty("eventName").GetString()).Should().Be(("Other club", "Their gala"));
        (await AttendeesAsync(other, theirs)).Should().ContainSingle();
        (await AttendeesAsync(club, ours)).Should().BeEmpty();

        // Nor can the other club read or touch our invitations.
        await Refused(await other.Owner.GetAsync($"/api/events/{ours}/invitations"), HttpStatusCode.NotFound, "another workspace's list");
        await Refused(await other.Owner.PostAsync($"/api/events/{ours}/invitations/remind", null), HttpStatusCode.NotFound, "another workspace's reminder");
        await Refused(await SendAsync(other, ours, Guid.NewGuid()), HttpStatusCode.NotFound, "another workspace's send");
        (await app.Services.Read(db => db.ClubEventInvitations.Count(i => i.EventId == ours))).Should().Be(1);
    }

    [Fact]
    public async Task TheReplyPageRefusesWhileTheClubModuleIsOff()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var link = await LinkOfAsync(app, anna);
        await app.SeedAsync(db => db.Organizations.Single(o => o.Id == club.Org).ClubModuleOff = true);
        var visitor = app.CreateClient();

        (await Refused(await PageAsync(visitor, link), HttpStatusCode.Forbidden, "the club off")).Should().Contain("Module.Off");
        await Refused(await AnswerAsync(visitor, link, "coming"), HttpStatusCode.Forbidden, "an answer with the club off");
        await app.SeedAsync(db => db.Organizations.Single(o => o.Id == club.Org).ClubModuleOff = false);
        await Json(await AnswerAsync(visitor, link, "coming"), "on again");
    }

    [Fact]
    public async Task TheReplyLinkIsRateLimited()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var link = await LinkOfAsync(app, anna);
        var visitor = app.CreateClient();

        for (var i = 0; i < 10; i++) (await PageAsync(visitor, link)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PageAsync(visitor, link)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        for (var i = 0; i < 10; i++) await PageAsync(visitor, "x" + i);
        (await PageAsync(visitor, "guess")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "what is not a link of ours shares one limit");
    }

    [Fact]
    public async Task RemindingThoseWhoHaveNotAnsweredSendsEachOfThemOneReminder_HoweverOftenItIsPressed()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno");
        var clara = await MemberAsync(club, "Clara", "fr");
        var dario = await MemberAsync(club, "Dario");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var visitor = app.CreateClient();
        await Json(await AnswerAsync(visitor, await LinkOfAsync(app, anna), "coming"), "Anna comes");
        await Json(await AnswerAsync(visitor, await LinkOfAsync(app, bruno), "declined"), "Bruno can't");
        // Dario was put on the list by the front desk: he has his answer.
        await Json(await club.Owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = dario, ticketTier = "Member" })), "the desk registers Dario");

        var first = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/remind", null), "remind");
        (first.GetProperty("reminded").GetInt32(), first.GetProperty("alreadyReminded").GetInt32()).Should().Be((1, 0));
        var second = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/invitations/remind", null), "remind again");
        (second.GetProperty("reminded").GetInt32(), second.GetProperty("alreadyReminded").GetInt32()).Should().Be((0, 1));

        var links = await LinksOfAsync(app, clara);
        links.Should().HaveCount(2, "the invitation and its one reminder");
        links.Distinct().Should().HaveCount(2, "the reminder carries a link of its own");
        (await app.Services.Read(db => db.OutboundMessages.Where(m => m.MemberId == clara && m.Source == OutboundSources.Invitation).OrderBy(m => m.CreatedAtUtc).Select(m => m.Subject).ToList()))
            .Last().Should().Be("Rappel\u00A0: Wine night", "in her language, with the no-break space French puts before a colon");
        (await Json(await PageAsync(visitor, links[0]), "the first link still works")).GetProperty("answer").GetString().Should().Be("none");
        (await Json(await AnswerAsync(visitor, links[1], "coming"), "she answers from the reminder")).GetProperty("place").GetString().Should().Be("seat");
        (await Json(await club.Owner.GetAsync($"/api/events/{eventId}/invitations"), "counts")).GetProperty("counts").GetProperty("reminded").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AnIllComeFromTheReplyPageSetsOffTheClubsRegistrationRules()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await Json(await club.Owner.PostAsync("/api/automations", Body(new
        {
            name = "Welcome the guest", trigger = "event.registered",
            actions = new[] { new { type = "create_task", config = new { title = "Prepare the place card" } } },
        })), "add the rule");
        var anna = await MemberAsync(club, "Anna");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");

        await Json(await AnswerAsync(app.CreateClient(), await LinkOfAsync(app, anna), "coming"), "Anna comes");

        var task = await app.Services.Read(db => db.TaskItems.Single());
        task.Title.Should().Be("Prepare the place card");
        task.Description.Should().Contain("Anna Keller", "the same trigger as a registration staff make");
    }

    [Fact]
    public async Task ErasingAMemberRemovesTheirInvitations_AndTheirLinkStopsWorking()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var eventId = await EventAsync(club);
        await Json(await SendAsync(club, eventId, Guid.NewGuid()), "send");
        var link = await LinkOfAsync(app, anna);
        var visitor = app.CreateClient();
        await Json(await AnswerAsync(visitor, link, "declined"), "Anna can't");

        var export = await club.Owner.PostAsync("/api/privacy/export", Body(new { memberIds = new[] { anna } }));
        export.StatusCode.Should().Be(HttpStatusCode.OK, await export.Content.ReadAsStringAsync());
        using (var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(), ZipArchiveMode.Read))
        {
            using var reader = new StreamReader(zip.GetEntry("club/event-invitations.json")!.Open());
            var text = await reader.ReadToEndAsync();
            text.Should().Contain("declined").And.Contain("Wine night");
            text.Should().NotContain(InvitationReplyToken.Hash(link), "the link's hash is the system's own");
        }

        await Json(await club.Owner.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { anna }, confirm = "ERASE" })), "erase Anna");
        (await app.Services.Read(db => db.ClubEventInvitations.Count())).Should().Be(0);
        await Refused(await PageAsync(visitor, link), HttpStatusCode.NotFound, "an erased member's link");
        (await ListAsync(club, eventId)).Should().BeEmpty();
    }

    [Fact]
    public void ExpectedAttendanceWeighsEachMemberByTheirOwnShowRate()
    {
        var anna = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        var clara = Guid.NewGuid();
        // Anna came three times and missed once (75%); Bruno came once and missed once (50%).
        var history = new List<(Guid, bool)> { (anna, true), (anna, true), (anna, true), (anna, false), (bruno, true), (bruno, false) };
        // Club: 4 check-ins of 6 = 66.67%. Anna 0.75 + Bruno 0.5 + Clara (no history) at the club's 0.6667 + a guest at 0.6667 + one checked in already.
        var estimate = EventInvitationService.Estimate([(anna, false), (bruno, false), (clara, false), (null, false), (Guid.NewGuid(), true)], 0, history);
        estimate.ClubRate.Should().Be(0.6667m);
        estimate.Expected.Should().Be(Math.Round(0.75m + 0.5m + 4m / 6 + 4m / 6 + 1m, 1, MidpointRounding.AwayFromZero));
        (estimate.Seats, estimate.Members, estimate.MembersWithHistory, estimate.Guests, estimate.CheckedIn).Should().Be((5, 4, 2, 1, 1));
        estimate.ClubHasHistory.Should().BeTrue();

        // A club with no history yet: everyone at 90%, and an unnamed seat too.
        var fresh = EventInvitationService.Estimate([(anna, false), (bruno, false)], 1, []);
        (fresh.Expected, fresh.ClubRate, fresh.ClubHasHistory).Should().Be((2.7m, 0.9m, false));
    }

    [Fact]
    public async Task TheEventPageReadsTheExpectedAttendanceFromThePastAtTheClub()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno");
        var past = await EventAsync(club, name: "Last tasting");
        var next = await EventAsync(club, name: "Next tasting");
        foreach (var member in new[] { anna, bruno })
            await Json(await club.Owner.PostAsync($"/api/events/{past}/attendees", Body(new { memberId = member, ticketTier = "Member" })), "register for the last one");
        await app.SeedAsync(db => db.ClubEvents.Single(e => e.Id == past).StartAtUtc = DateTime.UtcNow.AddDays(-30));
        var places = await AttendeesAsync(club, past);
        await Json(await club.Owner.PutAsync($"/api/events/{past}/attendees/{places.Single(p => p.GetProperty("memberId").GetGuid() == anna).GetProperty("id").GetString()}", Body(new { status = "CheckedIn" })), "Anna came");
        await Json(await club.Owner.PutAsync($"/api/events/{past}/attendees/{places.Single(p => p.GetProperty("memberId").GetGuid() == bruno).GetProperty("id").GetString()}", Body(new { status = "NoShow" })), "Bruno did not");
        foreach (var member in new[] { anna, bruno })
            await Json(await club.Owner.PostAsync($"/api/events/{next}/attendees", Body(new { memberId = member, ticketTier = "Member" })), "register for the next one");

        var expected = await Json(await club.Owner.GetAsync($"/api/events/{next}/expected-attendance"), "expected attendance");
        expected.GetProperty("expected").GetDecimal().Should().Be(1.0m, "Anna always comes and Bruno never has");
        expected.GetProperty("clubRate").GetDecimal().Should().Be(0.5m);
        expected.GetProperty("membersWithHistory").GetInt32().Should().Be(2);
    }
}
