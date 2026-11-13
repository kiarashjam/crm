using System.Net;
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
/// Staff's side of the member portal (invitations, who has used it), and the club's own portal
/// opening a member's account here for someone it received through a welcome hand-over.
/// </summary>
public class MemberPortalStaffTests
{
    private static async Task SetSenderAsync(CustomWebApplicationFactory factory, Guid org, string? secret = null)
    {
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        db.OnboardingSettings.Add(new OnboardingSettings
        {
            Id = Guid.NewGuid(), OrganizationId = org, SenderName = "Pavillon 46", ReplyTo = "members@pavillon46.ch",
            WebhookUrl = secret == null ? null : "https://portal.pavillon46.ch/api/integrations/cadence/onboarding", WebhookSecret = secret,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnInvitationGoesFromTheClubsSender_InTheMembersLanguage_AndItsLinkSignsThemIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@invite.ch");
        var anna = await MemberAsync(staff, "Anna");
        await Ok(await staff.PutAsync($"/api/members/{anna}", Body(new { preferredLanguage = "fr" })), "Anna reads French");
        await SetSenderAsync(factory, org);

        (await Refused(await staff.PostAsync($"/api/members/{anna}/portal/invite", null), HttpStatusCode.BadRequest, "inviting before the portal is on"))
            .Should().Contain("Portal.NotOn");
        var key = await OpenPortalAsync(staff);
        var use = await Ok(await staff.PostAsync($"/api/members/{anna}/portal/invite", null), "invite Anna");
        use.GetProperty("invitationsSent").GetInt32().Should().Be(1);

        var message = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Portal));
        message.Subject.Should().StartWith("Pavillon46").And.EndWith("votre espace membre", "the member reads French");
        message.FromName.Should().Be("Pavillon 46");
        message.ReplyTo.Should().StartWith("members@pavillon46.ch");
        message.Body.Should().Contain("Bonjour Anna").And.Contain("#token=");
        // The member's communications record the invitation, without the link that opens the account.
        var logged = await factory.Services.Read(db => db.MemberMessages.Single(m => m.MemberId == anna));
        logged.Preview.Should().NotContain("#token=");
        logged.OutboundMessageId.Should().Be(message.Id);

        var token = await LinkTokenAsync(factory, "anna@example.ch");
        var session = await Ok(await RedeemAsync(factory, key, token), "use the invitation");
        session.GetProperty("language").GetString().Should().Be("fr");
        var used = await Ok(await staff.GetAsync($"/api/members/{anna}/portal"), "Anna's use of the portal");
        used.GetProperty("firstSignInAtUtc").ValueKind.Should().Be(JsonValueKind.String);
        used.GetProperty("activeSessions").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task InviteAll_InvitesOnlyThoseNotYetInvited_OneEmailPerMailbox_AndNoFormerMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, _) = await ClubAsync(factory, "owner@invite-all.ch");
        var anna = await MemberAsync(staff, "Anna");
        await MemberAsync(staff, "Ben", "home@example.ch");
        await MemberAsync(staff, "Clara", "home@example.ch");
        await MemberAsync(staff, "Dora", status: "Cancelled");
        await OpenPortalAsync(staff);
        await Ok(await staff.PostAsync($"/api/members/{anna}/portal/invite", null), "invite Anna first");

        var result = await Ok(await staff.PostAsync("/api/member-portal/invite-all", null), "invite everyone else");
        result.GetProperty("invited").GetInt32().Should().Be(2, "Ben and Clara, in one email to their shared address");
        result.GetProperty("alreadyInvited").GetInt32().Should().Be(1);
        result.GetProperty("remaining").GetInt32().Should().Be(0);
        (await LinkTokensAsync(factory, "home@example.ch")).Should().HaveCount(2);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal && m.To == "dora@example.ch"))).Should().Be(0,
            "a former member is not invited");

        var again = await Ok(await staff.PostAsync("/api/member-portal/invite-all", null), "invite everyone again");
        again.GetProperty("invited").GetInt32().Should().Be(0, "nobody is invited twice by it");
        var usage = (await Ok(await staff.GetAsync("/api/member-portal/members"), "who has used the portal")).EnumerateArray().ToList();
        usage.Should().HaveCount(4);
        usage.Single(u => u.GetProperty("name").GetString() == "Dora Muster").GetProperty("invitationsSent").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task InvitingAndEndingSessionsTakeClubMembers_AndReachNoOtherClubsMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@invite-rights.ch");
        var anna = await MemberAsync(staff, "Anna");
        await OpenPortalAsync(staff);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@invite-rights.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);
        await Refused(await viewer.PostAsync($"/api/members/{anna}/portal/invite", null), HttpStatusCode.Forbidden, "a viewer inviting");
        await Refused(await viewer.PostAsync($"/api/members/{anna}/portal/revoke", null), HttpStatusCode.Forbidden, "a viewer ending sessions");
        await Refused(await viewer.PostAsync("/api/member-portal/invite-all", null), HttpStatusCode.Forbidden, "a viewer inviting everyone");

        var (other, _, _) = await ClubAsync(factory, "owner@invite-rights-other.ch", "Other club");
        await OpenPortalAsync(other);
        await Refused(await other.PostAsync($"/api/members/{anna}/portal/invite", null), HttpStatusCode.NotFound, "another club inviting Anna");
        await Refused(await other.PostAsync($"/api/members/{anna}/portal/revoke", null), HttpStatusCode.NotFound, "another club ending Anna's sessions");
        await Refused(await other.GetAsync($"/api/members/{anna}/portal"), HttpStatusCode.NotFound, "another club reading Anna's use");
        (await Ok(await other.GetAsync("/api/member-portal/members"), "the other club's list")).EnumerateArray().Should().BeEmpty();
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Portal))).Should().Be(0);
    }

    [Fact]
    public async Task TheSettingsSayPlainly_WhenMembersAreAlsoHandedToTheClubsOwnPortal()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, _, org) = await ClubAsync(factory, "owner@two-places.ch");
        (await Ok(await staff.GetAsync("/api/member-portal/settings"), "settings")).GetProperty("handsOverToOwnPortal").GetBoolean().Should().BeFalse();
        await SetSenderAsync(factory, org, secret: WebhookSigning.NewSecret());
        (await Ok(await staff.GetAsync("/api/member-portal/settings"), "settings")).GetProperty("handsOverToOwnPortal").GetBoolean().Should().BeTrue();
    }

    // ── the club's own portal, for a member it received through a hand-over ──────

    private static HttpRequestMessage SignedHandoffRequest(Guid onboardingId, string secret, long? timestamp = null, string? body = null)
    {
        var at = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = body ?? JsonSerializer.Serialize(new { nonce = Guid.NewGuid().ToString("N") });
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/public/portal/handoff/{onboardingId}/link")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookSigning.TimestampHeader, at.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add(WebhookSigning.SignatureHeader, WebhookSigning.Sign(secret, at, json));
        return request;
    }

    [Fact]
    public async Task TheClubsOwnPortal_GetsAOneTimeLinkForItsHandoffsMember_SignedWithTheHandoffSecret_AndUsedOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, owner, org) = await ClubAsync(factory, "owner@handoff-link.ch");
        var camille = await MemberAsync(staff, "Camille");
        var secret = WebhookSigning.NewSecret();
        await SetSenderAsync(factory, org, secret);
        var onboardingId = Guid.NewGuid();
        var lonely = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.DealOnboardings.AddRange(
                new DealOnboarding
                {
                    Id = onboardingId, OrganizationId = org, DealId = Guid.NewGuid(), ClubMemberId = camille, Name = "Camille Muster", Email = "camille@example.ch",
                    Status = OnboardingStatuses.Active, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = owner, UpdatedAtUtc = DateTime.UtcNow,
                },
                new DealOnboarding
                {
                    Id = lonely, OrganizationId = org, DealId = Guid.NewGuid(), Name = "Nobody Added", Email = "nobody@example.ch",
                    Status = OnboardingStatuses.Active, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = owner, UpdatedAtUtc = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }
        var key = await OpenPortalAsync(staff);
        var portal = factory.CreateClient();

        var first = SignedHandoffRequest(onboardingId, secret);
        var replay = new HttpRequestMessage(HttpMethod.Post, first.RequestUri) { Content = new StringContent(await first.Content!.ReadAsStringAsync(), System.Text.Encoding.UTF8, "application/json") };
        foreach (var header in first.Headers) replay.Headers.TryAddWithoutValidation(header.Key, header.Value);

        var link = await Ok(await portal.SendAsync(first), "the club's portal asks for Camille's link");
        var url = link.GetProperty("url").GetString()!;
        url.Should().Contain($"/member-portal/{key}/sign-in?handoff=1#token=");
        var token = Uri.UnescapeDataString(url[(url.IndexOf("#token=", StringComparison.Ordinal) + "#token=".Length)..]);
        var session = await Ok(await RedeemAsync(factory, key, token), "Camille's browser uses it");
        session.GetProperty("firstName").GetString().Should().Be("Camille");
        await Refused(await RedeemAsync(factory, key, token), HttpStatusCode.BadRequest, "the same link again");
        (await factory.Services.Read(db => db.MemberPortalSessions.Single())).Via.Should().Be(MemberPortalSignInWays.Handoff);

        await Refused(await portal.SendAsync(replay), HttpStatusCode.Unauthorized, "the same signed request replayed");
        await Refused(await portal.SendAsync(SignedHandoffRequest(onboardingId, "not-the-secret")), HttpStatusCode.Unauthorized, "a request signed with another secret");
        await Refused(await portal.SendAsync(SignedHandoffRequest(onboardingId, secret, DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds())), HttpStatusCode.Unauthorized, "a stale request");
        await Refused(await portal.SendAsync(SignedHandoffRequest(Guid.NewGuid(), secret)), HttpStatusCode.Unauthorized, "a hand-over nobody made");
        await Refused(await portal.SendAsync(SignedHandoffRequest(lonely, secret)), HttpStatusCode.BadRequest, "a hand-over that added nobody to the club");

        // A new secret retires every request signed with the old one.
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            (await db.OnboardingSettings.SingleAsync()).WebhookSecret = WebhookSigning.NewSecret();
            await db.SaveChangesAsync();
        }
        await Refused(await portal.SendAsync(SignedHandoffRequest(onboardingId, secret)), HttpStatusCode.Unauthorized, "a request signed with the replaced secret");
    }

    [Fact]
    public async Task TheHandoffLink_WaitsForThePortalToBeOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (staff, owner, org) = await ClubAsync(factory, "owner@handoff-closed.ch");
        var camille = await MemberAsync(staff, "Camille");
        var secret = WebhookSigning.NewSecret();
        await SetSenderAsync(factory, org, secret);
        var onboardingId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.DealOnboardings.Add(new DealOnboarding
            {
                Id = onboardingId, OrganizationId = org, DealId = Guid.NewGuid(), ClubMemberId = camille, Name = "Camille Muster", Email = "camille@example.ch",
                Status = OnboardingStatuses.Active, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = owner, UpdatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        (await Refused(await factory.CreateClient().SendAsync(SignedHandoffRequest(onboardingId, secret)), HttpStatusCode.Forbidden, "the portal is off"))
            .Should().Contain(DomainErrors.Portal.OffCode);
        (await factory.Services.Read(db => db.MemberPortalLinks.Count())).Should().Be(0);
    }
}
