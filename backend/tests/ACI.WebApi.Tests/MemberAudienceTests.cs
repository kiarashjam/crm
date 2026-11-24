using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// One audience of club members, worked out by the server for campaigns and event invitations
/// alike: tiers and statuses, tags, custom fields (a club's "Interests"), a saved Members view,
/// and habits from the club's own records, with who is left out and why.
/// </summary>
/// <remarks>
/// Campaigns could only choose members by tier and status, so nobody could write to "members
/// interested in wine", or to those who came to the last two tastings.
/// </remarks>
public class MemberAudienceTests
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

        public async Task SeedAsync(Action<ACI.Infrastructure.Persistence.AppDbContext> change)
        {
            using var scope = Services.CreateScope();
            await using var db = scope.ServiceProvider.EveryWorkspace();
            change(db);
            await db.SaveChangesAsync();
        }
    }

    private sealed record Club(HttpClient Owner, Guid OwnerId, Guid Org);

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Club> ClubAsync(CustomWebApplicationFactory app, string email = "olga@pavillon46.ch")
    {
        var (owner, ownerId) = await SignInAsync(app, email);
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        return new Club(owner, ownerId, org);
    }

    private static async Task<Guid> MemberAsync(Club club, string first, string? email = null, string tier = "Gold", string status = "Active", bool consent = true)
    {
        var id = (await Json(await club.Owner.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier, status,
        })), $"add {first}")).GetProperty("id").GetGuid();
        if (consent)
            await Json(await club.Owner.PostAsync($"/api/members/{id}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })), $"{first} agrees");
        return id;
    }

    private static async Task<Guid> Post(HttpClient client, string url, object body) =>
        (await Json(await client.PostAsync(url, Body(body)), $"POST {url}")).GetProperty("id").GetGuid();

    private static async Task Interests(Club club, Guid memberId, params string[] interests) =>
        (await club.Owner.PutAsync("/api/custom-fields/values", Body(new { entityType = "member", recordId = memberId, values = new { interests } })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task Tag(Club club, Guid memberId, params Guid[] tags) =>
        (await club.Owner.PutAsync("/api/tags/records", Body(new { entityType = "member", recordId = memberId, tagIds = tags })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    private static Task<HttpResponseMessage> AudienceAsync(Club club, object segment, string purpose = "marketing") =>
        club.Owner.PostAsync("/api/campaigns/audience", Body(new { segment, purpose }));

    private static async Task<JsonElement> CountAsync(Club club, object segment, string purpose = "marketing") =>
        await Json(await AudienceAsync(club, segment, purpose), "count the audience");

    /// <summary>The first names of those an audience reaches.</summary>
    private static List<string> Reached(JsonElement audience) =>
        audience.GetProperty("recipients").EnumerateArray().Select(r => r.GetProperty("name").GetString()!.Split(' ')[0]).OrderBy(n => n).ToList();

    /// <summary>A club with an "Interests" multiselect, a VIP tag, and four members who agreed to marketing.</summary>
    private static async Task<(Club Club, Guid Anna, Guid Bruno, Guid Clara, Guid Dario, Guid Vip)> WorldAsync(App app)
    {
        var club = await ClubAsync(app);
        await Post(club.Owner, "/api/custom-fields", new { entityType = "member", label = "Interests", type = "multiselect", options = new[] { "Wine", "Art", "Golf" } });
        var vip = await Post(club.Owner, "/api/tags", new { name = "VIP" });
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno", tier: "Silver");
        var clara = await MemberAsync(club, "Clara");
        var dario = await MemberAsync(club, "Dario");
        await Interests(club, anna, "Wine");
        await Interests(club, bruno, "Art");
        await Interests(club, clara, "Wine", "Golf");
        await Tag(club, anna, vip);
        await Tag(club, dario, vip);
        return (club, anna, bruno, clara, dario, vip);
    }

    [Fact]
    public async Task InterestsTagsAndTiersChooseTheMembers_AndTogetherAllMustHold()
    {
        await using var app = new App();
        var (club, _, _, _, _, vip) = await WorldAsync(app);

        Reached(await CountAsync(club, new { fields = new[] { "interests:any:Wine" } })).Should().Equal("Anna", "Clara");
        Reached(await CountAsync(club, new { fields = new[] { "interests:any:Art|Golf" } })).Should().Equal(new[] { "Bruno", "Clara" }, "a multiselect matches any of the options chosen");
        Reached(await CountAsync(club, new { tags = new[] { vip } })).Should().Equal("Anna", "Dario");
        Reached(await CountAsync(club, new { tiers = new[] { "Silver" } })).Should().Equal("Bruno");
        Reached(await CountAsync(club, new { fields = new[] { "interests:any:Wine" }, tags = new[] { vip } })).Should().Equal(new[] { "Anna" }, "every filter given must hold");
        Reached(await CountAsync(club, new { fields = new[] { "interests:any:Wine" }, tiers = new[] { "Gold" }, statuses = new[] { "Active" } })).Should().Equal("Anna", "Clara");
        var everyone = await CountAsync(club, new { });
        everyone.GetProperty("willReceive").GetInt32().Should().Be(4);
        everyone.GetProperty("chosen").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task HabitsFromTheClubsOwnRecordsChooseTheMembers()
    {
        await using var app = new App();
        var (club, anna, bruno, clara, dario, _) = await WorldAsync(app);

        // Two tastings that have started: Anna came to both, Bruno to the first, Clara had a seat and did not come.
        async Task<Guid> Started(string name) => (await Json(await club.Owner.PostAsync("/api/events", Body(new
        {
            name, startAtUtc = DateTime.UtcNow.AddHours(-2), endAtUtc = DateTime.UtcNow.AddHours(1), capacity = 0, status = "Published",
        })), "add event")).GetProperty("id").GetGuid();
        async Task Came(Guid eventId, Guid memberId, string status)
        {
            var place = await Json(await club.Owner.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId, ticketTier = "Member" })), "register");
            await Json(await club.Owner.PutAsync($"/api/events/{eventId}/attendees/{place.GetProperty("id").GetString()}", Body(new { status })), status);
        }
        var first = await Started("Tasting I");
        var second = await Started("Tasting II");
        await Came(first, anna, "CheckedIn");
        await Came(first, bruno, "CheckedIn");
        await Came(first, clara, "NoShow");
        await Came(second, anna, "CheckedIn");
        // Anna visited three times this week; Bruno once, six weeks ago; Clara and Dario never.
        await app.SeedAsync(db =>
        {
            foreach (var daysAgo in new[] { 1, 2, 3 })
                db.ClubVisits.Add(new ClubVisit { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = anna, MemberName = "Anna Keller", Venue = "Bar", VisitedAtUtc = DateTime.UtcNow.AddDays(-daysAgo) });
            db.ClubVisits.Add(new ClubVisit { Id = Guid.NewGuid(), OrganizationId = club.Org, MemberId = bruno, MemberName = "Bruno Keller", Venue = "Bar", VisitedAtUtc = DateTime.UtcNow.AddDays(-42) });
        });

        Reached(await CountAsync(club, new { attendedEventIds = new[] { first } })).Should().Equal(new[] { "Anna", "Bruno" }, "a no-show did not come");
        Reached(await CountAsync(club, new { attendedEventIds = new[] { Guid.NewGuid() } })).Should().BeEmpty("an event that is not the club's was attended by nobody");
        Reached(await CountAsync(club, new { minEvents = 2, eventMonths = 6 })).Should().Equal("Anna");
        Reached(await CountAsync(club, new { minVisits = 2, visitDays = 30 })).Should().Equal("Anna");
        Reached(await CountAsync(club, new { minVisits = 1, visitDays = 60 })).Should().Equal("Anna", "Bruno");
        Reached(await CountAsync(club, new { notVisitedDays = 30 })).Should().Equal("Bruno", "Clara", "Dario");
        Reached(await CountAsync(club, new { notVisitedDays = 30, fields = new[] { "interests:any:Wine" } })).Should().Equal(new[] { "Clara" }, "a habit and an interest together");

        (await AudienceAsync(club, new { minEvents = 2 })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "came to at least … events needs its months");
        (await AudienceAsync(club, new { notVisitedDays = 5000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        dario.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ASavedMembersViewChoosesItsMembers_AndAColleaguesPrivateViewIsRefused()
    {
        await using var app = new App();
        var (club, _, _, _, _, vip) = await WorldAsync(app);
        var (colleague, colleagueId) = await SignInAsync(app, "luc@pavillon46.ch");
        await AddMemberAsync(app, club.Org, colleagueId, OrgMemberRole.Manager);
        colleague.In(club.Org);

        // As the Members list saves it: its own query parameters, a tier among them.
        var wineGold = await Post(club.Owner, "/api/saved-views", new
        {
            list = "members", name = "Wine lovers on Gold", shared = false,
            query = new Dictionary<string, object> { ["field"] = new[] { "interests:any:Wine" }, ["tier"] = "Gold" },
        });
        var audience = await CountAsync(club, new { viewId = wineGold });
        Reached(audience).Should().Equal("Anna", "Clara");
        audience.GetProperty("view").GetProperty("name").GetString().Should().Be("Wine lovers on Gold");
        Reached(await CountAsync(club, new { viewId = wineGold, tags = new[] { vip } })).Should().Equal(new[] { "Anna" }, "a view is a start, and the other filters narrow it");

        var mine = await Post(colleague, "/api/saved-views", new
        {
            list = "members", name = "Luc's VIPs", shared = false, query = new Dictionary<string, object> { ["tag"] = new[] { vip.ToString() } },
        });
        var refused = await AudienceAsync(club, new { viewId = mine });
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound, "a view the person may not see is refused, saying nothing of it");
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Audience.View.NotFound");
        (await club.Owner.PostAsync("/api/campaigns", Body(new { name = "VIP night", purpose = "marketing", subject = "VIP night", body = "Come", segment = new { viewId = mine } })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "nor can a campaign be saved for it");

        await Json(await colleague.PutAsync($"/api/saved-views/{mine}", Body(new { shared = true })), "share the view");
        Reached(await CountAsync(club, new { viewId = mine })).Should().Equal("Anna", "Dario");
    }

    [Fact]
    public async Task EachReasonAMemberIsLeftOutIsCounted_AndTheyAddUp()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna", "anna@example.ch");
        await MemberAsync(club, "Lapsed", status: "Lapsed");
        var erased = await MemberAsync(club, "Erased");
        var nomail = await MemberAsync(club, "Nomail");
        await MemberAsync(club, "Noconsent", consent: false);
        await MemberAsync(club, "Bounced", "bounced@example.ch");
        await MemberAsync(club, "Stopped", "stopped@example.ch");
        await MemberAsync(club, "Twin", "ANNA@example.ch");
        await app.SeedAsync(db =>
        {
            db.ClubMembers.Single(m => m.Id == erased).ErasedAtUtc = DateTime.UtcNow;
            db.ClubMembers.Single(m => m.Id == nomail).Email = "not an address";
            db.EmailSuppressions.Add(new EmailSuppression { Id = Guid.NewGuid(), OrganizationId = club.Org, Email = "bounced@example.ch", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = DateTime.UtcNow });
        });
        await Json(await club.Owner.PostAsync("/api/contacts", Body(new { name = "Stopped Keller", email = "Stopped@Example.ch", doNotContact = true })), "a contact marked do not contact");

        var a = await CountAsync(club, new { statuses = new[] { "Active" } });
        a.GetProperty("chosen").GetInt32().Should().Be(8);
        a.GetProperty("statusExcluded").GetInt32().Should().Be(1);
        a.GetProperty("erased").GetInt32().Should().Be(1);
        a.GetProperty("noEmail").GetInt32().Should().Be(1);
        a.GetProperty("noConsent").GetInt32().Should().Be(1);
        a.GetProperty("suppressed").GetInt32().Should().Be(1);
        a.GetProperty("doNotContact").GetInt32().Should().Be(1);
        a.GetProperty("duplicates").GetInt32().Should().Be(1, "one email per address: Anna joined first");
        a.GetProperty("willReceive").GetInt32().Should().Be(1);
        Reached(a).Should().Equal("Anna");
        a.GetProperty("leftOut").EnumerateArray().Select(p => p.GetProperty("reason").GetString())
            .Should().BeEquivalentTo(["status", "erased", "no_email", "no_consent", "suppressed", "do_not_contact", "duplicate"]);
        // The figures the editor has always shown read as before.
        a.GetProperty("matching").GetInt32().Should().Be(7);
        a.GetProperty("withAddress").GetInt32().Should().Be(5);
        a.GetProperty("agreed").GetInt32().Should().Be(4);

        // A service message goes whatever they said about marketing, and do not contact is about marketing.
        var service = await CountAsync(club, new { statuses = new[] { "Active" } }, "service");
        service.GetProperty("noConsent").GetInt32().Should().Be(0);
        service.GetProperty("doNotContact").GetInt32().Should().Be(0);
        service.GetProperty("willReceive").GetInt32().Should().Be(3, "Anna, Noconsent and Stopped; the bounced address stops service mail too");
        anna.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ACampaignSavedWithOnlyTiersAndStatusesReachesExactlyTheMembersItAlwaysDid()
    {
        await using var app = new App();
        var club = await ClubAsync(app);
        var anna = await MemberAsync(club, "Anna");
        var bruno = await MemberAsync(club, "Bruno", consent: false);
        var clara = await MemberAsync(club, "Clara", tier: "Silver");
        var dario = await MemberAsync(club, "Dario", status: "Lapsed");
        var elsa = await MemberAsync(club, "Elsa", "ANNA@example.ch");
        // A campaign as one was stored before audiences had more than tiers and statuses.
        var id = Guid.NewGuid();
        await app.SeedAsync(db => db.Campaigns.Add(new Campaign
        {
            Id = id, OrganizationId = club.Org, Name = "Autumn gala", Purpose = ConsentPurposes.Marketing, Subject = "The gala", Body = "Join us",
            Segment = "{\"tiers\":[\"Gold\"],\"statuses\":[\"Active\"]}", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, Version = Guid.NewGuid(),
        }));

        var read = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read the campaign");
        read.GetProperty("segment").GetProperty("tiers").EnumerateArray().Select(t => t.GetString()).Should().Equal("Gold");
        read.GetProperty("segment").GetProperty("statuses").EnumerateArray().Select(t => t.GetString()).Should().Equal("Active");
        var counted = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}/audience"), "who it reaches");
        var asBefore = await Json(await club.Owner.GetAsync("/api/campaigns/audience?tiers=Gold&statuses=Active&purpose=marketing"), "the older count");
        foreach (var figure in new[] { "matching", "withAddress", "agreed", "willReceive" })
            counted.GetProperty(figure).GetInt32().Should().Be(asBefore.GetProperty(figure).GetInt32(), figure);
        (counted.GetProperty("matching").GetInt32(), counted.GetProperty("agreed").GetInt32(), counted.GetProperty("willReceive").GetInt32()).Should().Be((3, 2, 1));

        (await club.Owner.PostAsync($"/api/campaigns/{id}/send", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await app.RunSchedulerAsync();
        var recipients = (await Json(await club.Owner.GetAsync($"/api/campaigns/{id}/recipients"), "recipients")).EnumerateArray()
            .ToDictionary(r => r.GetProperty("memberId").GetGuid(), r => r.GetProperty("status").GetString());
        // Gold and Active: Anna (who gets it), Bruno (no consent) and Elsa (Anna's address), as it always did.
        recipients.Should().BeEquivalentTo(new Dictionary<Guid, string?> { [anna] = "queued", [bruno] = "suppressed", [elsa] = "skipped" });
        recipients.Keys.Should().NotContain([clara, dario]);
    }

    [Fact]
    public async Task ACampaignForAnInterestEmailsOnlyThoseMembers_AndKeepsItsSegment()
    {
        await using var app = new App();
        var (club, anna, bruno, clara, _, _) = await WorldAsync(app);
        var created = await Json(await club.Owner.PostAsync("/api/campaigns", Body(new
        {
            name = "Wine tasting", purpose = "marketing", category = "Event", subject = "A tasting", body = "Dear {{firstName}}, come taste.",
            segment = new { statuses = new[] { "Active" }, fields = new[] { "interests:any:Wine" } },
        })), "create the campaign");
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("segment").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).Should().Equal("interests:any:Wine");

        (await club.Owner.PostAsync($"/api/campaigns/{id}/send", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        await app.RunSchedulerAsync();
        var recipients = (await Json(await club.Owner.GetAsync($"/api/campaigns/{id}/recipients"), "recipients")).EnumerateArray()
            .Select(r => r.GetProperty("memberId").GetGuid()).ToList();
        recipients.Should().BeEquivalentTo([anna, clara]);
        recipients.Should().NotContain(bruno);
    }

    [Fact]
    public async Task ACampaignKeepsItsSavedViewAsItReadWhenSaved_ForTheSchedulerToSendLater()
    {
        await using var app = new App();
        var (club, anna, _, clara, _, _) = await WorldAsync(app);
        var view = await Post(club.Owner, "/api/saved-views", new
        {
            list = "members", name = "Wine lovers", shared = false, query = new Dictionary<string, object> { ["field"] = new[] { "interests:any:Wine" } },
        });
        var created = await Json(await club.Owner.PostAsync("/api/campaigns", Body(new
        {
            name = "Wine tasting", purpose = "marketing", subject = "A tasting", body = "Come taste.",
            segment = new { viewId = view }, scheduledAtUtc = DateTime.UtcNow.AddMinutes(10),
        })), "schedule the campaign");
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("segment").GetProperty("view").GetProperty("fields").EnumerateArray().Select(f => f.GetString()).Should().Equal("interests:any:Wine");

        // The view is deleted meanwhile; the campaign goes to whom it was saved for.
        (await club.Owner.DeleteAsync($"/api/saved-views/{view}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await app.SeedAsync(db => db.Campaigns.Single(c => c.Id == id).ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1));
        (await app.RunSchedulerAsync()).Should().Be(1);
        var sent = await Json(await club.Owner.GetAsync($"/api/campaigns/{id}"), "read the campaign");
        sent.GetProperty("status").GetString().Should().Be("Sent");
        (await Json(await club.Owner.GetAsync($"/api/campaigns/{id}/recipients"), "recipients")).EnumerateArray()
            .Select(r => r.GetProperty("memberId").GetGuid()).Should().BeEquivalentTo([anna, clara]);
    }

    [Fact]
    public async Task AnotherWorkspacesMembersAreNeverInTheAudience()
    {
        await using var app = new App();
        var (club, _, _, _, _, _) = await WorldAsync(app);
        var other = await ClubAsync(app, "zoe@other.ch");
        await MemberAsync(other, "Zoe");

        Reached(await CountAsync(other, new { })).Should().Equal("Zoe");
        Reached(await CountAsync(club, new { })).Should().NotContain("Zoe");
        (await AudienceAsync(other, new { fields = new[] { "interests:any:Wine" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        Reached(await CountAsync(other, new { fields = new[] { "interests:any:Wine" } })).Should().Equal(new[] { "Zoe" }, "a field the workspace does not have narrows nothing, as on the Members list");
    }
}
