using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Invitations and add-by-email go by the address, and anyone can register any
/// address. So an account has to prove it reads the inbox before either can put it
/// in someone's workspace.
/// </summary>
/// <remarks>
/// The scenario: a manager invites newhire@club.ch, who has no account yet. Someone
/// else registers that address first. Before this, they saw the invitation in
/// /api/invites/pending, accepted it by id, and had Member access to every record.
/// </remarks>
public class EmailVerificationTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task ANewAccountStartsUnconfirmed_AndTheEmailedLinkConfirmsItOnce()
    {
        await using var mail = new Mailbox();
        var (client, _) = await SignInAsync(mail, "kia@club.ch");

        (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        mail.Count("verify", "kia@club.ch").Should().Be(1, "registering sends the confirmation link");

        var token = mail.Token("verify", "kia@club.ch");
        var anonymous = mail.CreateClient();
        (await anonymous.PostAsync("/api/auth/verify-email", Body(new { token }))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("emailVerified").GetBoolean().Should().BeTrue();
        (await anonymous.PostAsync("/api/auth/verify-email", Body(new { token }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the link works once");
    }

    [Fact]
    public async Task AnUnconfirmedAccountCannotSeeOrAcceptAnInvitation_EvenWithTheLink()
    {
        await using var mail = new Mailbox();
        var (owner, ownerId) = await SignInAsync(mail, "manager@club.ch");
        var org = await CreateAsync(mail, ownerId, "Pavillon 46");
        var invited = await Json(await owner.In(org).PostAsync($"/api/invites/{org}", Body(new { email = "newhire@club.ch" })));
        var inviteId = invited.GetProperty("id").GetString();

        // Whoever registers the address first.
        var (squatter, _) = await SignInAsync(mail, "newhire@club.ch");

        (await Json(await squatter.GetAsync("/api/invites/pending"))).GetArrayLength()
            .Should().Be(0, "an unconfirmed account is not told who invited the address");

        // The old way in: accept by the id the pending list handed out.
        (await squatter.PostAsync($"/api/invites/{inviteId}/accept", null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "accepting by id is gone; only the emailed token accepts");

        // Even holding the emailed link, an unconfirmed account is refused.
        var res = await squatter.PostAsync("/api/invites/accept", Body(new { token = mail.Token("invite", "newhire@club.ch") }));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, await res.Content.ReadAsStringAsync());

        var members = await Json(await owner.In(org).GetAsync($"/api/organizations/{org}/members"));
        members.GetArrayLength().Should().Be(1, "only the owner");
    }

    [Fact]
    public async Task TheInvitationEmailCarriesATokenThatIsNotStored()
    {
        await using var mail = new Mailbox();
        var (owner, ownerId) = await SignInAsync(mail, "manager@club.ch");
        var org = await CreateAsync(mail, ownerId, "Pavillon 46");
        await Json(await owner.In(org).PostAsync($"/api/invites/{org}", Body(new { email = "newhire@club.ch" })));

        var token = mail.Token("invite", "newhire@club.ch");
        var stored = await mail.Services.Read(db => db.Invites.Single());
        stored.TokenHash.Should().NotBe(token).And.Be(ACI.Application.Common.PasswordResetCrypto.HashRawToken(token));
    }

    [Fact]
    public async Task AddByEmailRefusesAnUnconfirmedAccount_AndTakesItOnceConfirmed()
    {
        await using var mail = new Mailbox();
        var (owner, ownerId) = await SignInAsync(mail, "manager@club.ch");
        var org = await CreateAsync(mail, ownerId, "Pavillon 46");
        await SignInAsync(mail, "newhire@club.ch");

        var refused = await owner.In(org).PostAsync($"/api/organizations/{org}/members", Body(new { email = "newhire@club.ch", role = 3 }));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("has not confirmed its email address");

        await mail.CreateClient().PostAsync("/api/auth/verify-email", Body(new { token = mail.Token("verify", "newhire@club.ch") }));

        var added = await owner.In(org).PostAsync($"/api/organizations/{org}/members", Body(new { email = "newhire@club.ch", role = 3 }));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnUnconfirmedAccountCannotAskToJoin()
    {
        await using var mail = new Mailbox();
        var (_, ownerId) = await SignInAsync(mail, "manager@club.ch");
        var org = await CreateAsync(mail, ownerId, "Pavillon 46");
        var (stranger, _) = await SignInAsync(mail, "stranger@club.ch");

        (await stranger.PostAsync($"/api/joinrequests/{org}", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await mail.CreateClient().PostAsync("/api/auth/verify-email", Body(new { token = mail.Token("verify", "stranger@club.ch") }));
        (await stranger.PostAsync($"/api/joinrequests/{org}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnUnconfirmedAccountStillUsesItsOwnWorkspace()
    {
        await using var mail = new Mailbox();
        var (client, userId) = await SignInAsync(mail, "kia@club.ch");
        var org = await CreateAsync(mail, userId, "Mine");

        var res = await client.In(org).PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@example.ch" }));

        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResendingTheLinkIsCapped()
    {
        await using var mail = new Mailbox();
        var (client, _) = await SignInAsync(mail, "kia@club.ch");

        // Registration sent the first; two more fit in the window, then 429.
        (await client.PostAsync("/api/auth/verify-email/resend", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/api/auth/verify-email/resend", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/api/auth/verify-email/resend", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        mail.Count("verify", "kia@club.ch").Should().Be(3);

        // Only the latest link works.
        var latest = mail.Token("verify", "kia@club.ch");
        (await mail.CreateClient().PostAsync("/api/auth/verify-email", Body(new { token = latest }))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResettingThePasswordConfirmsTheAddress()
    {
        // The reset link went to the inbox, which is what confirmation proves. It is how
        // the real owner takes back an address somebody else registered first.
        await using var mail = new Mailbox();
        var (client, _) = await SignInAsync(mail, "newhire@club.ch");
        var anonymous = mail.CreateClient();
        (await anonymous.PostAsync("/api/auth/forgot-password", Body(new { email = "newhire@club.ch" }))).StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await anonymous.PostAsync("/api/auth/reset-password",
            Body(new { token = mail.Token("reset", "newhire@club.ch"), password = "a brand new passphrase" }));
        reset.StatusCode.Should().Be(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());

        var login = await Json(await anonymous.PostAsync("/api/auth/login", Body(new { email = "newhire@club.ch", password = "a brand new passphrase" })));
        login.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }
}
