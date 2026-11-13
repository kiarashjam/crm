using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.PortalWorld;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Signing in to the member portal: off until the club turns it on, a link emailed through the
/// outbox that works once and runs out, a member session that only the portal takes (and a staff
/// token the portal never takes), and sessions a member or staff can end.
/// </summary>
public class MemberPortalSignInTests
{
    [Fact]
    public async Task ThePortalIsOffUntilSomeoneWhoManagesSettingsTurnsItOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@portal-off.ch");
        await MemberAsync(staff, "Anna");

        var settings = await Ok(await staff.GetAsync("/api/member-portal/settings"), "read the settings");
        settings.GetProperty("enabled").GetBoolean().Should().BeFalse("the portal is off until the club turns it on");
        settings.GetProperty("portalUrl").ValueKind.Should().Be(JsonValueKind.Null, "it has no address before it was first turned on");

        // Someone on the team without settings.manage cannot turn it on.
        var (desk, deskId) = await SignInAsync(factory, "desk@portal-off.ch");
        await AddMemberAsync(factory, org, deskId, OrgMemberRole.Member);
        await Ok(await staff.PutAsync($"/api/organizations/{org}/members/{deskId}/permissions", Body(new { preset = "front_desk" })), "make them front desk");
        await Refused(await desk.In(org).PutAsync("/api/member-portal/settings", Body(new { enabled = true })), HttpStatusCode.Forbidden, "the front desk");

        var key = await OpenPortalAsync(staff);
        var club = await Ok(await factory.CreateClient().GetAsync($"/api/public/portal/{key}"), "the sign-in page");
        club.GetProperty("open").GetBoolean().Should().BeTrue();
        club.GetProperty("clubName").GetString().Should().Be("Pavillon46");
        await Refused(await factory.CreateClient().GetAsync("/api/public/portal/no-such-key"), HttpStatusCode.NotFound, "an address nobody has");
    }

    [Fact]
    public async Task AMemberSignsInWithAnEmailedLink_ThatWorksOnce_FromTheClubsSender()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@link-once.ch");
        await MemberAsync(staff, "Anna");
        // Welcome & portal's sender: the portal's emails go under this name, replies to this address.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            db.OnboardingSettings.Add(new OnboardingSettings
            {
                Id = Guid.NewGuid(), OrganizationId = org, SenderName = "Pavillon 46 Members", ReplyTo = "members@pavillon46.ch", UpdatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var key = await OpenPortalAsync(staff);

        (await AskForLinkAsync(factory, key, "  ANNA@example.ch ")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var message = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Portal));
        message.FromName.Should().Be("Pavillon 46 Members");
        message.ReplyTo.Should().StartWith("members@pavillon46.ch");
        message.Purpose.Should().Be(ConsentPurposes.Service, "a sign-in link is what the membership brings, not marketing");
        message.Body.Should().Contain("/member-portal/").And.Contain("#token=", "the secret rides in the fragment, which no server logs");
        var stored = await factory.Services.Read(db => db.MemberPortalLinks.Single());
        var token = await LinkTokenAsync(factory, "anna@example.ch");
        stored.TokenHash.Should().NotBe(token, "only the secret's hash is kept");

        var session = await Ok(await RedeemAsync(factory, key, token), "use the link");
        session.GetProperty("firstName").GetString().Should().Be("Anna");
        var member = MemberClient(factory, session.GetProperty("token").GetString()!);
        (await Ok(await member.GetAsync("/api/portal/me"), "read their account")).GetProperty("firstName").GetString().Should().Be("Anna");

        var again = await Refused(await RedeemAsync(factory, key, token), HttpStatusCode.BadRequest, "the same link a second time");
        again.Should().Contain(DomainErrors.Portal.LinkInvalidCode);
        (await factory.Services.Read(db => db.MemberPortalLinks.Count())).Should().Be(0, "using the link is what deletes it");
        var use = await factory.Services.Read(db => db.MemberPortalAccounts.Single());
        use.FirstSignInAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AnAddressThatIsNoMembersGetsTheSameAnswer_AndNothingIsSent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@stranger.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);

        (await AskForLinkAsync(factory, key, "stranger@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal))).Should().Be(0);
        await Refused(await AskForLinkAsync(factory, key, "not an address"), HttpStatusCode.BadRequest, "something that is no address");
    }

    [Fact]
    public async Task ACoupleSharingAMailboxGetsOneEmail_WithALinkForEach()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@couple.ch");
        await MemberAsync(staff, "Anna", "home@example.ch");
        await MemberAsync(staff, "Ben", "home@example.ch");
        var key = await OpenPortalAsync(staff);

        (await AskForLinkAsync(factory, key, "home@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal))).Should().Be(1);
        var tokens = await LinkTokensAsync(factory, "home@example.ch");
        tokens.Should().HaveCount(2);
        var names = new List<string>();
        foreach (var token in tokens)
            names.Add((await Ok(await RedeemAsync(factory, key, token), "use a link")).GetProperty("firstName").GetString()!);
        names.Should().BeEquivalentTo(["Anna", "Ben"]);
    }

    [Fact]
    public async Task ALinkThatRanOut_OrIsAnotherClubs_IsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@ran-out.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var (other, _, _) = await ClubAsync(factory, "owner@other-club.ch", "Other club");
        var otherKey = await OpenPortalAsync(other);

        (await AskForLinkAsync(factory, key, "anna@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var token = await LinkTokenAsync(factory, "anna@example.ch");
        await Refused(await RedeemAsync(factory, otherKey, token), HttpStatusCode.BadRequest, "Anna's link at another club's portal");

        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var link = await db.MemberPortalLinks.SingleAsync();
            link.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await Refused(await RedeemAsync(factory, key, token), HttpStatusCode.BadRequest, "a link that ran out");
        await Refused(await RedeemAsync(factory, key, "made-up"), HttpStatusCode.BadRequest, "a made-up link");
    }

    [Fact]
    public async Task TooManyLinksForOneAddress_AreRefused_WhetherOrNotItIsAMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@too-many.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);

        foreach (var address in new[] { "anna@example.ch", "nobody@example.ch" })
        {
            for (var i = 0; i < 3; i++)
                (await AskForLinkAsync(factory, key, address)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            await Refused(await AskForLinkAsync(factory, key, address), HttpStatusCode.TooManyRequests, $"a fourth link for {address}");
        }
    }

    [Fact]
    public async Task AStaffTokenNeverReachesThePortal_AndAMemberTokenReachesNothingElse()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@two-keys.ch");
        var anna = await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var member = await SignInMemberAsync(factory, key, "anna@example.ch");

        // The club's owner, with every permission there is, is no member of the portal.
        await Refused(await staff.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "a staff token on the portal");
        await Refused(await staff.GetAsync("/api/portal/account"), HttpStatusCode.Unauthorized, "a staff token on the portal's money");

        // A member token names the club, yet opens none of the team's endpoints, with or without its header.
        member.DefaultRequestHeaders.Add("X-Organization-Id", org.ToString());
        foreach (var path in new[] { "/api/members", $"/api/members/{anna}/consent", "/api/charges", $"/api/members/{anna}/statement?from=2026-01-01&to=2026-12-31", "/api/member-portal/settings", "/api/organizations" })
            await Refused(await member.GetAsync(path), HttpStatusCode.Unauthorized, $"a member token on {path}");
        await Refused(await member.PostAsync($"/api/members/{anna}/portal/invite", Body(new { })), HttpStatusCode.Unauthorized, "a member token inviting");
        member.DefaultRequestHeaders.Remove("X-Organization-Id");
        (await Ok(await member.GetAsync("/api/portal/me"), "the member's own account")).GetProperty("memberId").GetGuid().Should().Be(anna);
    }

    [Fact]
    public async Task SigningOutEndsThatSession_AndSigningOutEverywhereEndsThemAll()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@sign-out.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var phone = await SignInMemberAsync(factory, key, "anna@example.ch");
        var laptop = await SignInMemberAsync(factory, key, "anna@example.ch");
        var tablet = await SignInMemberAsync(factory, key, "anna@example.ch");

        (await phone.PostAsync("/api/portal/sign-out", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Refused(await phone.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "the session signed out");
        await Ok(await laptop.GetAsync("/api/portal/me"), "another device stays signed in");

        (await Ok(await laptop.PostAsync("/api/portal/sign-out-everywhere", null), "sign out everywhere")).GetProperty("ended").GetInt32().Should().Be(2);
        await Refused(await laptop.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "this device");
        await Refused(await tablet.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "every other device");
    }

    [Fact]
    public async Task StaffEndAMembersSessions_AndTheirLinksNotYetUsedStopWorking()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@revoke.ch");
        var anna = await MemberAsync(staff, "Anna");
        await MemberAsync(staff, "Ben");
        var key = await OpenPortalAsync(staff);
        var annaSession = await SignInMemberAsync(factory, key, "anna@example.ch");
        var benSession = await SignInMemberAsync(factory, key, "ben@example.ch");
        (await AskForLinkAsync(factory, key, "anna@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var unused = await LinkTokenAsync(factory, "anna@example.ch");

        var use = await Ok(await staff.PostAsync($"/api/members/{anna}/portal/revoke", null), "end Anna's sessions");
        use.GetProperty("activeSessions").GetInt32().Should().Be(0);
        await Refused(await annaSession.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "Anna's session");
        await Refused(await RedeemAsync(factory, key, unused), HttpStatusCode.BadRequest, "Anna's link not yet used");
        await Ok(await benSession.GetAsync("/api/portal/me"), "Ben stays signed in");
    }

    [Fact]
    public async Task AnErasedMemberCannotSignIn_AndTheirSessionStopsAnswering()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@erased.ch");
        var anna = await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var session = await SignInMemberAsync(factory, key, "anna@example.ch");
        (await AskForLinkAsync(factory, key, "anna@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await LinkTokenAsync(factory, "anna@example.ch");

        // What an erasure leaves for the books: the row, renamed, marked erased.
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var row = await db.ClubMembers.SingleAsync(m => m.Id == anna);
            row.ErasedAtUtc = DateTime.UtcNow;
            row.FirstName = "Erased member 1";
            await db.SaveChangesAsync();
        }
        await Refused(await session.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "an erased member's session");
        await Refused(await RedeemAsync(factory, key, link), HttpStatusCode.BadRequest, "an erased member's link");
        var before = await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal));
        (await AskForLinkAsync(factory, key, "anna@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal))).Should().Be(before, "nobody is sent a link for an erased member");
    }

    [Fact]
    public async Task WithTheClubTurnedOff_ThePortalRefusesLikeTheClubsOtherEndpoints()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@club-off.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var session = await SignInMemberAsync(factory, key, "anna@example.ch");

        await Ok(await staff.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })), "turn the club off");
        (await Refused(await session.GetAsync("/api/portal/me"), HttpStatusCode.Forbidden, "the portal with the club off")).Should().Contain(DomainErrors.Modules.OffCode);
        (await Refused(await AskForLinkAsync(factory, key, "anna@example.ch"), HttpStatusCode.Forbidden, "a link with the club off")).Should().Contain(DomainErrors.Modules.OffCode);
        (await Ok(await factory.CreateClient().GetAsync($"/api/public/portal/{key}"), "the sign-in page")).GetProperty("open").GetBoolean().Should().BeFalse();
        await Refused(await staff.GetAsync("/api/member-portal/settings"), HttpStatusCode.Forbidden, "staff's portal settings with the club off");

        await Ok(await staff.PutAsync($"/api/organizations/{org}/modules", Body(new { club = true })), "turn the club on again");
        await Ok(await session.GetAsync("/api/portal/me"), "the session answers again");
    }

    [Fact]
    public async Task WithTheClubTurnedOff_AnOrganizationHeaderTheMemberAdds_DoesNotOpenThePortalAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@club-off-header.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var session = await SignInMemberAsync(factory, key, "anna@example.ch");
        // Another club, with its club module on.
        var (_, _, otherOrg) = await ClubAsync(factory, "owner@club-on-header.ch", "Other club");

        await Ok(await staff.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })), "turn the club off");
        // The header is checked only for staff (OrganizationAccessMiddleware): a member's session
        // names their club, and whatever workspace a header names, the answer is their club's.
        foreach (var named in new[] { otherOrg, Guid.NewGuid() })
        {
            session.DefaultRequestHeaders.Remove("X-Organization-Id");
            session.DefaultRequestHeaders.Add("X-Organization-Id", named.ToString());
            (await Refused(await session.GetAsync("/api/portal/me"), HttpStatusCode.Forbidden, $"the portal with the club off, naming {named}"))
                .Should().Contain(DomainErrors.Modules.OffCode);
            (await Refused(await session.GetAsync("/api/portal/account"), HttpStatusCode.Forbidden, $"the account with the club off, naming {named}"))
                .Should().Contain(DomainErrors.Modules.OffCode);
        }
    }

    [Fact]
    public async Task TurningThePortalOffSignsEveryoneOut_AndClosesTheSignInPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@portal-closed.ch");
        await MemberAsync(staff, "Anna");
        var key = await OpenPortalAsync(staff);
        var session = await SignInMemberAsync(factory, key, "anna@example.ch");
        (await AskForLinkAsync(factory, key, "anna@example.ch")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await LinkTokenAsync(factory, "anna@example.ch");

        await Ok(await staff.PutAsync("/api/member-portal/settings", Body(new { enabled = false })), "turn the portal off");
        await Refused(await session.GetAsync("/api/portal/me"), HttpStatusCode.Unauthorized, "a session from before");
        await Refused(await RedeemAsync(factory, key, link), HttpStatusCode.BadRequest, "a link from before");
        (await Refused(await AskForLinkAsync(factory, key, "anna@example.ch"), HttpStatusCode.Forbidden, "asking for a link")).Should().Contain(DomainErrors.Portal.OffCode);

        await OpenPortalAsync(staff);
        var fresh = await SignInMemberAsync(factory, key, "anna@example.ch");
        await Ok(await fresh.GetAsync("/api/portal/me"), "signing in afresh, at the same address, once it is on again");
    }

    [Fact]
    public async Task SettingsAreSavedFromTheVersionRead_AndAStaleCopyIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@settings-version.ch");
        await OpenPortalAsync(staff);
        var read = await Ok(await staff.GetAsync("/api/member-portal/settings"), "read");
        var version = read.GetProperty("version").GetGuid();

        await Ok(await staff.PutAsync("/api/member-portal/settings", Body(new { memberCancelHours = 48, version })), "save from what was read");
        (await Refused(await staff.PutAsync("/api/member-portal/settings", Body(new { memberCancelHours = 6, version })), HttpStatusCode.Conflict, "save from a copy read before"))
            .Should().Contain("Portal.Settings.Changed.Conflict");
        await Refused(await staff.PutAsync("/api/member-portal/settings", Body(new { memberCancelHours = 1000 })), HttpStatusCode.BadRequest, "a cut-off past thirty days");
    }
}
