using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Campaigns to a club's members, delivered by email through the outbox, one message per
/// member, sent once whoever presses Send or however many instances run the scheduler.
/// </summary>
/// <remarks>
/// "Send" only wrote a log entry per member marked "Sent", nobody received anything, the
/// open rate read 0%, and a campaign planned for Friday stayed planned for ever.
/// </remarks>
public class CampaignTests
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

        public async Task<int> RunSchedulerAsync()
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICampaignService>().RunDueAsync(DateTime.UtcNow);
        }
    }

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org);

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Club> ClubAsync(CustomWebApplicationFactory app, string email = "olga@pavillon46.ch", string name = "Pavillon46")
    {
        var (owner, ownerId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, ownerId, name);
        owner.In(org);
        return new Club(owner, ownerId, org);
    }

    private static async Task<Guid> MemberAsync(Club club, string first, string email, string tier = "Gold", string status = "Active") =>
        (await Json(await club.Owner.PostAsync("/api/members", Body(new { firstName = first, lastName = "Dupont", email, tier, status })), $"add {first}"))
            .GetProperty("id").GetGuid();

    private static async Task ConsentAsync(Club club, Guid memberId, bool granted) =>
        await Json(await club.Owner.PostAsync($"/api/members/{memberId}/consent", Body(new { channel = "email", purpose = "marketing", granted, source = "form" })), "consent");

    private static async Task<JsonElement> CampaignAsync(Club club, string purpose = "marketing", object? segment = null, DateTime? scheduledAtUtc = null, string name = "Autumn gala") =>
        await Json(await club.Owner.PostAsync("/api/campaigns", Body(new
        {
            name, channel = "Email", category = "Event", purpose, subject = "The autumn gala", body = "Dear {{firstName}},\nJoin us on Friday.",
            segment = segment ?? new { statuses = new[] { "Active" } }, scheduledAtUtc,
        })), "create campaign");

    private static async Task<List<JsonElement>> RecipientsAsync(Club club, Guid campaignId) =>
        (await Json(await club.Owner.GetAsync($"/api/campaigns/{campaignId}/recipients"), "recipients")).EnumerateArray().ToList();

    private static JsonElement Recipient(List<JsonElement> recipients, Guid memberId) =>
        recipients.Single(r => r.GetProperty("memberId").GetGuid() == memberId);

    /// <summary>Four members: one who agreed, one never asked, one who withdrew, and one who agreed sharing the first one's address.</summary>
    private static async Task<(Guid Agreed, Guid NeverAsked, Guid Withdrew, Guid SameAddress)> MembersAsync(Club club)
    {
        var agreed = await MemberAsync(club, "Anna", "anna@example.ch");
        var neverAsked = await MemberAsync(club, "Bruno", "bruno@example.ch");
        var withdrew = await MemberAsync(club, "Clara", "clara@example.ch");
        var sameAddress = await MemberAsync(club, "Dario", "ANNA@example.ch");
        await ConsentAsync(club, agreed, true);
        await ConsentAsync(club, withdrew, true);
        await ConsentAsync(club, withdrew, false);
        await ConsentAsync(club, sameAddress, true);
        return (agreed, neverAsked, withdrew, sameAddress);
    }

    [Fact]
    public async Task AMarketingCampaignEmailsEachMemberWhoAgreedOnceAndSaysWhyNotForTheRest()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var m = await MembersAsync(club);
        var campaign = await CampaignAsync(club);
        var id = campaign.GetProperty("id").GetGuid();
        campaign.GetProperty("status").GetString().Should().Be("Draft");

        var audience = await Json(await club.Owner.GetAsync("/api/campaigns/audience?statuses=Active&purpose=marketing"), "audience");
        audience.GetProperty("matching").GetInt32().Should().Be(4);
        audience.GetProperty("agreed").GetInt32().Should().Be(2);
        audience.GetProperty("willReceive").GetInt32().Should().Be(1, "two members who agreed share one address, and get one email");

        var sent = await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send");
        sent.GetProperty("status").GetString().Should().Be("Sent");
        sent.GetProperty("totals").GetProperty("recipients").GetInt32().Should().Be(4);
        sent.GetProperty("totals").GetProperty("queued").GetInt32().Should().Be(1);
        sent.GetProperty("totals").GetProperty("suppressed").GetInt32().Should().Be(2);
        sent.GetProperty("totals").GetProperty("skipped").GetInt32().Should().Be(1);

        var recipients = await RecipientsAsync(club, id);
        Recipient(recipients, m.Agreed).GetProperty("status").GetString().Should().Be("queued");
        Recipient(recipients, m.NeverAsked).GetProperty("status").GetString().Should().Be("suppressed");
        Recipient(recipients, m.NeverAsked).GetProperty("reason").GetString().Should().Contain("has not agreed to marketing by email");
        Recipient(recipients, m.Withdrew).GetProperty("reason").GetString().Should().Contain("withdrew consent");
        Recipient(recipients, m.SameAddress).GetProperty("status").GetString().Should().Be("skipped");
        Recipient(recipients, m.SameAddress).GetProperty("reason").GetString().Should().Contain("Another member at this address");

        await app.DispatchOutboxAsync();
        var email = app.Mail.Sent.Should().ContainSingle("one email per address that may have it").Subject;
        email.To.Should().Equal("anna@example.ch");
        email.TextBody.Should().StartWith("Dear Anna,").And.Contain("https://crm.example.ch/unsubscribe/");
        Recipient(await RecipientsAsync(club, id), m.Agreed).GetProperty("status").GetString().Should().Be("sent");
        var after = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read campaign");
        after.GetProperty("totals").GetProperty("sent").GetInt32().Should().Be(1);
        after.GetProperty("totals").GetProperty("queued").GetInt32().Should().Be(0);

        var log = (await Json(await club.Owner.GetAsync($"/api/communications?memberId={m.Agreed}"), "the member's log")).EnumerateArray().ToList();
        log.Should().ContainSingle().Which.GetProperty("campaignName").GetString().Should().Be("Autumn gala");
    }

    [Fact]
    public async Task WhatTheMailServiceReportsShowsPerMemberAndInTheTotals()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        var bruno = await MemberAsync(club, "Bruno", "bruno@example.ch");
        var clara = await MemberAsync(club, "Clara", "clara@example.ch");
        var id = (await CampaignAsync(club, purpose: "service")).GetProperty("id").GetGuid();
        await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send");
        await app.DispatchOutboxAsync();

        var outbound = await app.Services.Read(db => db.MemberMessages.Where(r => r.CampaignId == id).ToDictionary(r => r.MemberId, r => r.OutboundMessageId!.Value));
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IInboundEmailService>().ReceiveEventsAsync(
            [
                new MailEvent("delivered", "anna@example.ch", DateTime.UtcNow, outbound[anna], null, null, null),
                new MailEvent("open", "anna@example.ch", DateTime.UtcNow, outbound[anna], null, null, null),
                new MailEvent("delivered", "bruno@example.ch", DateTime.UtcNow, outbound[bruno], null, null, null),
                new MailEvent("bounce", "clara@example.ch", DateTime.UtcNow, outbound[clara], null, "550 5.1.1 No such user", "bounce"),
            ]);
        }

        var recipients = await RecipientsAsync(club, id);
        Recipient(recipients, anna).GetProperty("status").GetString().Should().Be("opened");
        Recipient(recipients, bruno).GetProperty("status").GetString().Should().Be("delivered");
        Recipient(recipients, clara).GetProperty("status").GetString().Should().Be("bounced");
        Recipient(recipients, clara).GetProperty("reason").GetString().Should().Contain("No such user");
        var totals = (await Json(await club.Owner.GetAsync("/api/campaigns"), "list")).EnumerateArray().Single().GetProperty("totals");
        totals.GetProperty("sent").GetInt32().Should().Be(3, "the mail server took all three");
        totals.GetProperty("delivered").GetInt32().Should().Be(2, "an opened message was delivered");
        totals.GetProperty("opened").GetInt32().Should().Be(1);
        totals.GetProperty("bounced").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task OnceSendingHasStartedItCannotBeSentEditedOrDeletedAgain()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        await ConsentAsync(club, anna, true);
        var id = (await CampaignAsync(club)).GetProperty("id").GetGuid();

        // Two presses of Send at once: one sends, the other is told it already has.
        var presses = await Task.WhenAll(
            club.Owner.PostAsync($"/api/campaigns/{id}/send", null),
            club.Owner.PostAsync($"/api/campaigns/{id}/send", null));
        presses.Select(p => p.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        (await app.Services.Read(db => db.OutboundMessages.Count(o => o.SourceId == id))).Should().Be(1);

        var again = await club.Owner.PostAsync($"/api/campaigns/{id}/send", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("Campaign.AlreadySent.Conflict");
        var edit = await club.Owner.PutAsync($"/api/campaigns/{id}", Body(new { subject = "Changed after it went" }));
        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await edit.Content.ReadAsStringAsync()).Should().Contain("Campaign.Locked.Conflict");
        (await club.Owner.DeleteAsync($"/api/campaigns/{id}")).StatusCode.Should().Be(HttpStatusCode.Conflict, "what went to whom is kept");
        (await app.Services.Read(db => db.Campaigns.Single(c => c.Id == id).Subject)).Should().Be("The autumn gala");
        (await app.Services.Read(db => db.MemberMessages.Count(r => r.CampaignId == id))).Should().Be(1);
    }

    [Fact]
    public async Task AScheduledCampaignIsSentAtItsTimeByTheSchedulerOnce()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        await ConsentAsync(club, anna, true);
        var created = await CampaignAsync(club, scheduledAtUtc: DateTime.UtcNow.AddHours(2));
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("status").GetString().Should().Be("Scheduled");

        (await app.RunSchedulerAsync()).Should().Be(0, "its time has not come");

        // Its time comes; two instances run the scheduler at the same moment.
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.Campaigns.Single(c => c.Id == id).ScheduledAtUtc = DateTime.UtcNow.AddSeconds(-5);
            await db.SaveChangesAsync();
        }
        var taken = await Task.WhenAll(app.RunSchedulerAsync(), app.RunSchedulerAsync());
        taken.Sum().Should().Be(1, "one claim wins");
        (await app.RunSchedulerAsync()).Should().Be(0, "a sent campaign is not taken up again");

        var campaign = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read");
        campaign.GetProperty("status").GetString().Should().Be("Sent");
        (await app.Services.Read(db => db.MemberMessages.Count(r => r.CampaignId == id))).Should().Be(1);
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().ContainSingle().Which.To.Should().Equal("anna@example.ch");
        var reply = await app.Services.Read(db => db.OutboundMessages.Single(o => o.SourceId == id).ReplyTo);
        reply.Should().Contain("olga@pavillon46.ch", "replies go to whoever scheduled it");
    }

    [Fact]
    public async Task OfTwoMembersAtOneAddressTheOneWhoJoinedFirstGetsTheEmail()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        async Task<Guid> Joined(string first, string email, DateTime joinedAtUtc) =>
            (await Json(await club.Owner.PostAsync("/api/members", Body(new
            {
                firstName = first, lastName = "Dupont", email, tier = "Gold", status = "Active", joinedAtUtc,
            })), $"add {first}")).GetProperty("id").GetGuid();
        // Six households, the newer member's record made first in each: in record order the
        // member who joined first got the email about one time in two, and the answer changed
        // from one campaign to the next.
        var firstJoined = new List<Guid>();
        var joinedLater = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            joinedLater.Add(await Joined($"Zoe{i}", $"home{i}@example.ch", new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)));
            firstJoined.Add(await Joined($"Yann{i}", $"HOME{i}@example.ch", new DateTime(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
        var id = (await CampaignAsync(club, purpose: "service")).GetProperty("id").GetGuid();

        await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send");

        var recipients = await RecipientsAsync(club, id);
        foreach (var member in firstJoined) Recipient(recipients, member).GetProperty("status").GetString().Should().Be("queued");
        foreach (var member in joinedLater) Recipient(recipients, member).GetProperty("status").GetString().Should().Be("skipped");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().HaveCount(6).And.OnlyContain(e => e.TextBody.StartsWith("Dear Yann"));
    }

    [Fact]
    public async Task ASendingThatStoppedPartWayIsFinishedWithoutEmailingAnyoneTwice()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        var bruno = await MemberAsync(club, "Bruno", "bruno@example.ch");
        var id = (await CampaignAsync(club, purpose: "service")).GetProperty("id").GetGuid();
        await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send");

        // As if the instance sending it had died after Anna's message and before Bruno's:
        // Bruno's message is gone, the campaign still sending, its claim run out.
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var brunos = db.MemberMessages.Single(r => r.CampaignId == id && r.MemberId == bruno);
            db.OutboundMessages.Remove(db.OutboundMessages.Single(o => o.Id == brunos.OutboundMessageId));
            db.MemberMessages.Remove(brunos);
            var campaign = db.Campaigns.Single(c => c.Id == id);
            campaign.Status = CampaignStatuses.Sending;
            campaign.SentAtUtc = null;
            campaign.ClaimedUntilUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        (await app.RunSchedulerAsync()).Should().Be(1);

        var messages = await app.Services.Read(db => db.MemberMessages.Where(r => r.CampaignId == id).Select(r => r.MemberId).ToList());
        messages.Should().BeEquivalentTo([anna, bruno], "Bruno gets his, and Anna is not sent a second");
        (await app.Services.Read(db => db.OutboundMessages.Count(o => o.SourceId == id))).Should().Be(2);
        (await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read")).GetProperty("status").GetString().Should().Be("Sent");
    }

    [Fact]
    public async Task ATestGoesToTheSenderAloneMarkedAsATestAndCountsForNothing()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        await ConsentAsync(club, anna, true);
        var id = (await CampaignAsync(club)).GetProperty("id").GetGuid();

        var test = await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/test", null), "send a test");
        test.GetProperty("to").GetString().Should().Be("olga@pavillon46.ch");
        test.GetProperty("status").GetString().Should().Be("sent");

        var email = app.Mail.Sent.Should().ContainSingle().Subject;
        email.To.Should().Equal("olga@pavillon46.ch");
        email.Subject.Should().Be("[Test] The autumn gala");
        email.TextBody.Should().StartWith("This is a test of the campaign \"Autumn gala\", sent to you alone. Members have not received it.");
        email.TextBody.Should().NotContain("/unsubscribe/", "nobody on the staff is unsubscribing from their own test");
        (await app.Services.Read(db => db.MemberMessages.Count())).Should().Be(0, "a test is no member's message");
        var campaign = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read");
        campaign.GetProperty("status").GetString().Should().Be("Draft", "a test sends nothing to members and locks nothing");

        var sent = await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send it for real");
        sent.GetProperty("totals").GetProperty("recipients").GetInt32().Should().Be(1, "the test is not counted");
    }

    [Fact]
    public async Task AServiceCampaignGoesToEveryoneItIsForWithoutAnUnsubscribeLink()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        await ConsentAsync(club, anna, false);
        await MemberAsync(club, "Bruno", "bruno@example.ch");
        await MemberAsync(club, "Clara", "clara@example.ch", status: "Lapsed");
        var id = (await CampaignAsync(club, purpose: "service", name: "Closed on Monday")).GetProperty("id").GetGuid();

        await Json(await club.Owner.PostAsync($"/api/campaigns/{id}/send", null), "send");
        await app.DispatchOutboxAsync();

        app.Mail.SentTo.Should().BeEquivalentTo(["anna@example.ch", "bruno@example.ch"], "the active members, whatever they said about marketing");
        app.Mail.Sent.Should().OnlyContain(e => !e.TextBody.Contains("nsubscribe") && e.UnsubscribeUrl == null);
    }

    [Theory]
    [InlineData("SMS")]
    [InlineData("Push")]
    public async Task SmsAndPushAreNotOfferedAsIfTheyWorked(string channel)
    {
        await using var app = new App();
        var club = await ClubAsync(app);

        var res = await club.Owner.PostAsync("/api/campaigns", Body(new { name = "Text them", channel, subject = "Hi", body = "Hi" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Campaign.ChannelUnavailable").And.Contain("not available");
    }

    [Fact]
    public async Task ATextCampaignKeptFromBeforeCannotBeScheduledToGoByEmail()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        await MemberAsync(club, "Anna", "anna@example.ch");
        // As the move from the old documents leaves an SMS campaign that was never sent: a draft.
        var texts = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var now = DateTime.UtcNow;
            db.Campaigns.Add(new Campaign
            {
                Id = texts, OrganizationId = club.Org, Name = "Text blast", Channel = CampaignChannels.Sms, Purpose = ConsentPurposes.Service,
                Subject = "Hi", Body = "Court 2 is free tonight", Status = CampaignStatuses.Draft, CreatedAtUtc = now, UpdatedAtUtc = now,
            });
            await db.SaveChangesAsync();
        }

        var scheduled = await club.Owner.PutAsync($"/api/campaigns/{texts}", Body(new { scheduledAtUtc = DateTime.UtcNow.AddMinutes(30) }));

        scheduled.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Send now refuses it, so the scheduler must not get it to send by email");
        (await scheduled.Content.ReadAsStringAsync()).Should().Contain("Campaign.ChannelUnavailable");
        (await app.Services.Read(db => db.Campaigns.Single(c => c.Id == texts).Status)).Should().Be(CampaignStatuses.Draft);
    }

    [Fact]
    public async Task AnEditMadeFromAnOlderCopyIsRefused()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var created = await CampaignAsync(club, scheduledAtUtc: DateTime.UtcNow.AddDays(1));
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetGuid();

        var first = await Json(await club.Owner.PutAsync($"/api/campaigns/{id}", Body(new { subject = "First edit", version })), "first edit");
        first.GetProperty("version").GetGuid().Should().NotBe(version);
        var stale = await club.Owner.PutAsync($"/api/campaigns/{id}", Body(new { subject = "Second edit, from the old copy", version }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadAsStringAsync()).Should().Contain("Campaign.Changed.Conflict");

        // Sending null for the time takes the schedule off.
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/campaigns/{id}") { Content = new StringContent("{\"scheduledAtUtc\":null}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{first.GetProperty("version").GetGuid()}\""));
        var unscheduled = await Json(await club.Owner.SendAsync(request), "unschedule");
        unscheduled.GetProperty("status").GetString().Should().Be("Draft");
        unscheduled.GetProperty("subject").GetString().Should().Be("First edit");
    }

    [Fact]
    public async Task OnlyThoseWhoManageMembersWriteAndSendCampaigns()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var id = (await CampaignAsync(club)).GetProperty("id").GetGuid();
        var (desk, deskId) = await SignInAsync(app, "desk@pavillon46.ch");
        await AddMemberAsync(app, club.Org, deskId, OrgMemberRole.Member);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OrganizationMembers.Single(m => m.UserId == deskId && m.OrganizationId == club.Org).PermissionPreset = "front_desk";
            await db.SaveChangesAsync();
        }
        desk.In(club.Org);

        (await desk.GetAsync("/api/campaigns")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await desk.PostAsync($"/api/campaigns/{id}/send", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await desk.PostAsync($"/api/campaigns/{id}/test", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await desk.PostAsync("/api/campaigns", Body(new { name = "Mine", subject = "Hi", body = "Hi" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await desk.PostAsync("/api/communications/send", Body(new { memberId = Guid.NewGuid(), purpose = "service", subject = "Hi", body = "Hi" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnotherWorkspacesCampaignsAndMessagesCanBeNeitherSeenNorTouched()
    {
        await using var app = new App();
        var alpine = await ClubAsync(app, "owner@alpine.example", "Alpine");
        var bernina = await ClubAsync(app, "owner@bernina.example", "Bernina");
        var theirMember = await MemberAsync(bernina, "Zebrafish", "zebra@example.ch");
        var theirs = (await CampaignAsync(bernina, purpose: "service", name: "Zebrafish news")).GetProperty("id").GetGuid();
        await Json(await bernina.Owner.PostAsync($"/api/campaigns/{theirs}/send", null), "their own send");

        (await alpine.Owner.GetStringAsync("/api/campaigns")).Should().NotContain("Zebrafish");
        (await alpine.Owner.GetStringAsync("/api/communications")).Should().NotContain("Zebrafish");
        foreach (var (method, url) in new[]
        {
            (HttpMethod.Get, $"/api/campaigns/{theirs}"), (HttpMethod.Get, $"/api/campaigns/{theirs}/recipients"),
            (HttpMethod.Put, $"/api/campaigns/{theirs}"), (HttpMethod.Delete, $"/api/campaigns/{theirs}"),
            (HttpMethod.Post, $"/api/campaigns/{theirs}/send"), (HttpMethod.Post, $"/api/campaigns/{theirs}/test"),
        })
        {
            var res = await alpine.Owner.SendAsync(new HttpRequestMessage(method, url) { Content = method == HttpMethod.Put ? Body(new { name = "Taken" }) : null });
            res.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{method} {url}");
            (await res.Content.ReadAsStringAsync()).Should().NotContain("Zebrafish");
        }
        var write = await alpine.Owner.PostAsync("/api/communications/send", Body(new { memberId = theirMember, purpose = "service", subject = "Hi", body = "Hi" }));
        write.StatusCode.Should().Be(HttpStatusCode.NotFound, "a member of another workspace cannot be written to");
        (await app.Services.Read(db => db.Campaigns.Single(c => c.Id == theirs).Name)).Should().Be("Zebrafish news");
        (await app.Services.Read(db => db.MemberMessages.Count(r => r.OrganizationId == alpine.Org))).Should().Be(0);
    }

    [Fact]
    public async Task MergingTwoRecordsOfOneMemberKeepsTheirMessagesAndAnEmailWaitingStillGoes()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var keep = await MemberAsync(club, "Jean", "jean@example.ch");
        var drop = await MemberAsync(club, "Jeannot", "jeannot@example.ch");
        await Json(await club.Owner.PostAsync("/api/communications/send", Body(new
        {
            memberId = drop, purpose = "service", subject = "Your locker", body = "It is ready.",
        })), "send to the duplicate");

        (await club.Owner.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await app.DispatchOutboxAsync();

        app.Mail.Sent.Should().ContainSingle("the person still exists, so the email waiting for them is not cancelled as if they had gone");
        var log = (await Json(await club.Owner.GetAsync($"/api/communications?memberId={keep}"), "the kept member's log")).EnumerateArray().ToList();
        log.Should().ContainSingle().Which.GetProperty("status").GetString().Should().Be("sent");
        log[0].GetProperty("memberName").GetString().Should().Be("Jean Dupont");
    }

    [Fact]
    public async Task AMessageToOneMemberGoesThroughTheOutboxAndALogOnlyRecords()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");

        (await club.Owner.PostAsync("/api/communications/send", Body(new { memberId = anna, subject = "Your locker", body = "It is ready." })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "whether it is marketing or a service message is said every time");

        var sent = await Json(await club.Owner.PostAsync("/api/communications/send", Body(new
        {
            memberId = anna, category = "Reservation", purpose = "service", subject = "Your locker", body = "Dear {{firstName}}, it is ready.",
        })), "send");
        sent.GetProperty("status").GetString().Should().Be("queued");
        await app.DispatchOutboxAsync();
        app.Mail.Sent.Should().ContainSingle().Which.TextBody.Should().StartWith("Dear Anna, it is ready.");

        var offer = await Json(await club.Owner.PostAsync("/api/communications/send", Body(new
        {
            memberId = anna, category = "Loyalty", purpose = "marketing", subject = "Double points", body = "This week only.",
        })), "send marketing without consent");
        offer.GetProperty("status").GetString().Should().Be("suppressed", "the attempt is recorded, and nothing goes");
        offer.GetProperty("reason").GetString().Should().Contain("has not agreed to marketing by email");

        var logged = await Json(await club.Owner.PostAsync("/api/communications", Body(new { memberId = anna, channel = "SMS", subject = "Texted from the desk phone" })), "log");
        logged.GetProperty("status").GetString().Should().Be("logged");

        var log = (await Json(await club.Owner.GetAsync($"/api/communications?memberId={anna}"), "log")).EnumerateArray().ToList();
        log.Select(r => r.GetProperty("status").GetString()).Should().BeEquivalentTo(["sent", "suppressed", "logged"]);
        app.Mail.Sent.Should().ContainSingle("the marketing offer and the logged text sent nothing");

        (await club.Owner.DeleteAsync($"/api/communications/{logged.GetProperty("id").GetGuid()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await club.Owner.DeleteAsync($"/api/communications/{sent.GetProperty("id").GetGuid()}")).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "what Cadence sent is kept as the record of what went to whom");
    }
}
