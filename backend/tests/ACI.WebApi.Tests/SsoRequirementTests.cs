using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace that requires single sign-on (Organization.SsoRequired, the owner's setting)
/// refuses its members' sessions that began with a password or a passkey, for the members its
/// email domains cover, everywhere its data is reached.
/// </summary>
public class SsoRequirementTests
{
    private sealed record Club(SsoApp App, Guid Org, Guid Other, HttpClient Owner, Guid OwnerId, Guid AnnaId);

    /// <summary>
    /// Pavillon 46, owned by the owner, with anna@pavillon46.ch as a manager; anna also belongs
    /// to another club that requires nothing. Every address is confirmed.
    /// </summary>
    private static async Task<Club> ClubAsync()
    {
        var app = new SsoApp();
        var (owner, ownerId) = await ConfirmedAsync(app, "owner@pavillon46.ch");
        var (_, annaId) = await ConfirmedAsync(app, "anna@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        await AddMemberAsync(app, org, annaId, OrgMemberRole.Manager);
        var (_, otherOwnerId) = await ConfirmedAsync(app, "someone@elsewhere.ch");
        var other = await CreateAsync(app, otherOwnerId, "Other club");
        await AddMemberAsync(app, other, annaId, OrgMemberRole.Member);
        return new Club(app, org, other, owner.In(org), ownerId, annaId);
    }

    private static async Task<(HttpClient Client, Guid UserId)> ConfirmedAsync(SsoApp app, string email)
    {
        var (client, userId) = await SignInAsync(app, email);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.FindAsync(userId))!.EmailVerifiedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (client, userId);
    }

    /// <summary>A session for <paramref name="userId"/> that began with <paramref name="method"/>, as the server issues it.</summary>
    private static HttpClient SessionOf(SsoApp app, Guid userId, string method)
    {
        using var scope = app.Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Find(userId)!;
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user, method));
        return client;
    }

    private static async Task<HttpClient> PasswordSessionAsync(SsoApp app, string email)
    {
        var res = await app.CreateClient().PostAsync("/api/auth/login", Body(new { email, password = AuthenticatorApp.Password }));
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, "signing in itself works: the account is shared with the person's other workspaces");
        using var doc = JsonDocument.Parse(body);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("token").GetString());
        return client;
    }

    private static async Task RequireAsync(Club club, bool required, params string[] domains)
    {
        var res = await club.Owner.PutAsync($"/api/organizations/{club.Org}/sso", Body(new { required, domains }));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
    }

    private static async Task ShouldBeRefusedAsync(HttpResponseMessage res, Guid org)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        using var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("Sso.Required");
        problem.RootElement.GetProperty("organizationId").GetGuid().Should().Be(org);
        problem.RootElement.GetProperty("detail").GetString().Should().Contain("Pavillon 46").And.Contain("Google or Microsoft");
    }

    [Fact]
    public async Task APasswordSessionOfAMemberItHolds_IsRefusedByTheWorkspace_AndKeepsWorkingInTheirOthers()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        await RequireAsync(club, true, "pavillon46.ch");

        var anna = await PasswordSessionAsync(club.App, "anna@pavillon46.ch");
        await ShouldBeRefusedAsync(await anna.In(club.Org).GetAsync("/api/contacts"), club.Org);
        await ShouldBeRefusedAsync(await anna.In(club.Org).PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@example.ch" })), club.Org);
        (await anna.In(club.Other).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Her list of workspaces says which one asks for single sign-on.
        using var orgs = JsonDocument.Parse(await anna.In(club.Org).GetStringAsync("/api/organizations"));
        var byId = orgs.RootElement.EnumerateArray().ToDictionary(o => o.GetProperty("id").GetGuid());
        byId[club.Org].GetProperty("ssoSignInNeeded").GetBoolean().Should().BeTrue();
        byId[club.Org].GetProperty("ssoRequired").GetBoolean().Should().BeTrue();
        byId[club.Other].GetProperty("ssoSignInNeeded").GetBoolean().Should().BeFalse();

        // Her own account stays hers to use, so she can sign in again the right way.
        (await anna.In(club.Org).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anna.In(club.Org).GetAsync("/api/invites/pending")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SigningInWithSingleSignOn_OpensTheWorkspace()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        await RequireAsync(club, true, "pavillon46.ch");

        // The whole round trip, as anna's browser does it.
        var browser = club.App.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var verifier = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var authorize = (await browser.GetAsync($"/api/auth/sso/google/start?challenge={challenge}")).Headers.Location!;
        var query = authorize.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(kv => kv[0], kv => Uri.UnescapeDataString(kv[1]));
        club.App.Provider.IdToken = _ => club.App.Provider.Sign(FakeOidcProvider.GoogleClaims("anna-google", "anna@pavillon46.ch", query["nonce"]));
        var landing = (await browser.GetAsync($"/api/auth/sso/google/callback?code=c&state={Uri.EscapeDataString(query["state"])}")).Headers.Location!;
        var code = Uri.UnescapeDataString(landing.Fragment["#code=".Length..]);
        var done = await browser.PostAsync("/api/auth/sso/complete", Body(new { code, verifier }));
        using var session = JsonDocument.Parse(await done.Content.ReadAsStringAsync());

        var anna = club.App.CreateClient();
        anna.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.RootElement.GetProperty("token").GetString());
        (await anna.In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anna.In(club.Org).GetAsync($"/api/organizations/{club.Org}/members")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var orgs = JsonDocument.Parse(await anna.GetStringAsync("/api/organizations"));
        orgs.RootElement.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == club.Org)
            .GetProperty("ssoSignInNeeded").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task APasskeySessionIsRefusedToo()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        await RequireAsync(club, true, "pavillon46.ch");

        await ShouldBeRefusedAsync(await SessionOf(club.App, club.AnnaId, SignInMethods.Passkey).In(club.Org).GetAsync("/api/contacts"), club.Org);
        (await SessionOf(club.App, club.AnnaId, SignInMethods.Sso).In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ItTakesEffectAtOnceOnSessionsAlreadySignedIn_AndEndsWhenTurnedOff()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        var anna = await PasswordSessionAsync(club.App, "anna@pavillon46.ch");
        (await anna.In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK, "this caches the workspace's policy");

        await RequireAsync(club, true, "pavillon46.ch");
        await ShouldBeRefusedAsync(await anna.In(club.Org).GetAsync("/api/contacts"), club.Org);

        await RequireAsync(club, false, "pavillon46.ch");
        (await anna.In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheOwnerIsNeverHeldToIt_SoABrokenProviderCannotLockTheWorkspaceOut()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        await RequireAsync(club, true);

        var owner = await PasswordSessionAsync(club.App, "owner@pavillon46.ch");
        (await owner.In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.In(club.Org).GetAsync($"/api/organizations/{club.Org}/sso")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.In(club.Org).PutAsync($"/api/organizations/{club.Org}/sso", Body(new { required = false })))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the owner can always turn it off again");
    }

    [Fact]
    public async Task MembersOutsideTheListedDomains_KeepSigningInAsBefore_AndWithoutDomainsEveryMemberButTheOwnerIsHeld()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        var (_, accountantId) = await ConfirmedAsync(club.App, "books@treuhand-zurich.ch");
        await AddMemberAsync(club.App, club.Org, accountantId, OrgMemberRole.Member);
        await RequireAsync(club, true, "pavillon46.ch");

        var accountant = await PasswordSessionAsync(club.App, "books@treuhand-zurich.ch");
        (await accountant.In(club.Org).GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var view = JsonDocument.Parse(await club.Owner.GetStringAsync($"/api/organizations/{club.Org}/sso")))
        {
            var members = view.RootElement.GetProperty("members").EnumerateArray().ToDictionary(m => m.GetProperty("email").GetString()!);
            members["anna@pavillon46.ch"].GetProperty("applies").GetBoolean().Should().BeTrue();
            members["books@treuhand-zurich.ch"].GetProperty("applies").GetBoolean().Should().BeFalse();
            members["owner@pavillon46.ch"].GetProperty("applies").GetBoolean().Should().BeFalse();
            view.RootElement.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).Should().Equal("pavillon46.ch");
        }

        await RequireAsync(club, true);
        await ShouldBeRefusedAsync(await accountant.In(club.Org).GetAsync("/api/contacts"), club.Org);
    }

    [Fact]
    public async Task AMemberItHoldsCannotReachTheWorkspaceThroughItsOwnAddressesEither()
    {
        // Settings, members, keys and invitations name the workspace in the route, not the
        // header. An MCP key made here would read the whole CRM without any session at all.
        var club = await ClubAsync();
        await using var _ = club.App;
        await RequireAsync(club, true, "pavillon46.ch");
        var anna = await PasswordSessionAsync(club.App, "anna@pavillon46.ch");
        anna.In(club.Other);

        await ShouldBeRefusedAsync(await anna.GetAsync($"/api/organizations/{club.Org}/members"), club.Org);
        await ShouldBeRefusedAsync(await anna.GetAsync($"/api/organizations/{club.Org}/email/suppressions"), club.Org);
        await ShouldBeRefusedAsync(await anna.PostAsync($"/api/webhook/organizations/{club.Org}/generate-mcp-key", null), club.Org);
        await ShouldBeRefusedAsync(await anna.PostAsync($"/api/invites/{club.Org}", Body(new { email = "new@pavillon46.ch" })), club.Org);
        await ShouldBeRefusedAsync(await anna.GetAsync($"/api/invites/organization/{club.Org}"), club.Org);
        await ShouldBeRefusedAsync(await anna.GetAsync($"/api/joinrequests/organization/{club.Org}"), club.Org);

        // Named through one of its invitations or join requests.
        var invite = await club.Owner.PostAsync($"/api/invites/{club.Org}", Body(new { email = "guest@example.ch" }));
        invite.StatusCode.Should().Be(HttpStatusCode.OK, await invite.Content.ReadAsStringAsync());
        var inviteId = await club.App.Services.Read(db => db.Invites.Single(i => i.OrganizationId == club.Org).Id);
        await ShouldBeRefusedAsync(await anna.PostAsync($"/api/invites/{inviteId}/resend", null), club.Org);

        var (outsider, _) = await ConfirmedAsync(club.App, "asks@example.ch");
        (await outsider.PostAsync($"/api/joinrequests/{club.Org}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var requestId = await club.App.Services.Read(db => db.JoinRequests.Single(r => r.OrganizationId == club.Org).Id);
        await ShouldBeRefusedAsync(await anna.PostAsync($"/api/joinrequests/{requestId}/accept", null), club.Org);
        (await club.App.Services.Read(db => db.OrganizationMembers.Count(m => m.OrganizationId == club.Org))).Should().Be(2, "nobody was let in");
    }

    [Fact]
    public async Task OnlyTheOwnerChangesIt_AndOnlyToDomainsThatAreDomains()
    {
        var club = await ClubAsync();
        await using var _ = club.App;
        var anna = SessionOf(club.App, club.AnnaId, SignInMethods.Sso).In(club.Org);

        var byManager = await anna.PutAsync($"/api/organizations/{club.Org}/sso", Body(new { required = true }));
        byManager.StatusCode.Should().Be(HttpStatusCode.Forbidden, await byManager.Content.ReadAsStringAsync());
        (await anna.GetAsync($"/api/organizations/{club.Org}/sso")).StatusCode.Should().Be(HttpStatusCode.OK, "a manager can see it");

        var bad = await club.Owner.PutAsync($"/api/organizations/{club.Org}/sso", Body(new { required = true, domains = new[] { "not a domain" } }));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadAsStringAsync()).Should().Contain("Sso.InvalidDomain");

        using var cleaned = JsonDocument.Parse(await (await club.Owner.PutAsync($"/api/organizations/{club.Org}/sso",
            Body(new { required = true, domains = new[] { "@Pavillon46.CH", "pavillon46.ch", "café.ch" } }))).Content.ReadAsStringAsync());
        cleaned.RootElement.GetProperty("domains").EnumerateArray().Select(d => d.GetString()).Should().Equal("pavillon46.ch", "xn--caf-dma.ch");
        (await club.App.Services.Read(db => db.Organizations.Single(o => o.Id == club.Org).SsoRequired)).Should().BeTrue();
    }

    [Fact]
    public async Task ItCannotBeTurnedOnWhileTheServerOffersNoProvider()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon 46");

        var res = await owner.In(org).PutAsync($"/api/organizations/{org}/sso", Body(new { required = true }));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Sso.NoProvider");
        (await factory.Services.Read(db => db.Organizations.Single(o => o.Id == org).SsoRequired)).Should().BeFalse();
    }

    [Fact]
    public void TheRequirementHoldsMembersByTheirAddressesDomain()
    {
        SsoRequirement.Holds(true, "pavillon46.ch", isOwner: false, "Anna@Pavillon46.CH").Should().BeTrue();
        SsoRequirement.Holds(true, "pavillon46.ch", isOwner: false, "anna@staff.pavillon46.ch").Should().BeFalse("only the domains listed");
        SsoRequirement.Holds(true, "pavillon46.ch", isOwner: true, "owner@pavillon46.ch").Should().BeFalse();
        SsoRequirement.Holds(true, null, isOwner: false, "anyone@gmail.com").Should().BeTrue();
        SsoRequirement.Holds(false, null, isOwner: false, "anyone@gmail.com").Should().BeFalse();
        SsoRequirement.Normalize(["localhost"]).IsFailure.Should().BeTrue();
        SsoRequirement.Normalize(["123.45"]).IsFailure.Should().BeTrue();
        SsoRequirement.Normalize([]).Value.Should().BeNull();
    }
}
