using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Passkeys over HTTP, against a software authenticator: adding one in Settings, signing in with
/// it, using it in place of the authenticator code, the signature counter, and removing it.
/// </summary>
public class PasskeyTests
{
    private static async Task<JsonDocument> OkJson(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    /// <summary>A ceremony's id and the options the browser hands navigator.credentials.</summary>
    private static async Task<(string CeremonyId, JsonElement Options)> CeremonyAsync(HttpClient client, string path, object? body = null)
    {
        using var doc = await OkJson(await client.PostAsync(path, Body(body ?? new { })));
        return (doc.RootElement.GetProperty("ceremonyId").GetString()!, doc.RootElement.GetProperty("options").Clone());
    }

    /// <summary>Adds a passkey to the client's account as Settings does; returns the authenticator holding it.</summary>
    private static async Task<SoftwareAuthenticator> AddPasskeyAsync(PasskeyApp app, HttpClient client, string name = "MacBook", bool synced = false)
    {
        var (ceremony, options) = await CeremonyAsync(client, "/api/auth/passkeys/registration/options", new { password = AuthenticatorApp.Password });
        var key = app.NewAuthenticator();
        key.Synced = synced;
        using var added = await OkJson(await client.PostAsync("/api/auth/passkeys/registration", Body(new { ceremonyId = ceremony, name, credential = key.Register(options) })));
        added.RootElement.GetProperty("name").GetString().Should().Be(name);
        return key;
    }

    private static async Task<HttpResponseMessage> SignInWithAsync(PasskeyApp app, Func<JsonElement, JsonObject> answer)
    {
        var browser = app.CreateClient();
        var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/sign-in/options");
        return await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new { ceremonyId = ceremony, credential = answer(options) }));
    }

    [Fact]
    public async Task AddedInSettings_APasskeySignsInWithoutThePasswordAndWithoutTheCode()
    {
        await using var app = new PasskeyApp();
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AuthenticatorApp.TurnOnAsync(settings);
        var key = await AddPasskeyAsync(app, settings);

        // The options ask for a discoverable passkey that verifies its user, with ES256 or RS256.
        var (_, options) = await CeremonyAsync(settings, "/api/auth/passkeys/registration/options", new { password = AuthenticatorApp.Password });
        options.GetProperty("rp").GetProperty("id").GetString().Should().Be(PasskeyApp.RpId);
        options.GetProperty("authenticatorSelection").GetProperty("requireResidentKey").GetBoolean().Should().BeTrue();
        options.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString().Should().Be("required");
        options.GetProperty("pubKeyCredParams").EnumerateArray().Select(p => p.GetProperty("alg").GetInt32()).Should().Equal(-7, -257);
        options.GetProperty("excludeCredentials").EnumerateArray().Select(c => c.GetProperty("id").GetString()).Should().Equal(key.CredentialIdText);

        using var done = await OkJson(await SignInWithAsync(app, o => key.Assert(o)));
        done.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse("a passkey that verified its user is both factors");
        var token = done.RootElement.GetProperty("token").GetString()!;
        AuthenticatorApp.Claim(token, SignInMethods.ClaimType).Should().Be(SignInMethods.Passkey);
        done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);

        var session = app.CreateClient();
        session.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var listed = await OkJson(await session.GetAsync("/api/auth/passkeys"));
        var passkey = listed.RootElement.EnumerateArray().Single();
        passkey.GetProperty("name").GetString().Should().Be("MacBook");
        passkey.GetProperty("lastUsedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(1);
    }

    [Fact]
    public async Task SigningInWithAPasskeyNeedsTheDeviceToVerifyThePerson()
    {
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, settings);

        (await SignInWithAsync(app, o => key.Assert(o, userVerified: false))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddingAPasskeyTakesThePassword()
    {
        // With a stolen session alone, someone could otherwise add a way in of their own.
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");

        var none = await settings.PostAsync("/api/auth/passkeys/registration/options", Body(new { }));
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await none.Content.ReadAsStringAsync()).Should().Contain("Passkeys.PasswordRequired");

        var wrong = await settings.PostAsync("/api/auth/passkeys/registration/options", Body(new { password = "not my password 1" }));
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadAsStringAsync()).Should().Contain("Auth.WrongPassword");
        (await app.Services.Read(db => db.AuthChallenges.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AnAccountWithoutAPassword_AddsOneOnlyRightAfterSigningIn()
    {
        await using var app = new PasskeyApp();
        var user = new User { Id = Guid.NewGuid(), Name = "Marc", Email = "marc@pavillon46.ch", CreatedAtUtc = DateTime.UtcNow, EmailVerifiedAtUtc = DateTime.UtcNow };
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var client = app.CreateClient();
        using (var scope = app.Services.CreateScope())
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user, SignInMethods.Sso));

        (await client.PostAsync("/api/auth/passkeys/registration/options", Body(new { }))).StatusCode.Should().Be(HttpStatusCode.OK);

        app.Clock.Ahead = TimeSpan.FromMinutes(20);
        var stale = await client.PostAsync("/api/auth/passkeys/registration/options", Body(new { }));
        stale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await stale.Content.ReadAsStringAsync()).Should().Contain("Passkeys.RecentSignInRequired");
    }

    [Fact]
    public async Task ACounterThatDoesNotRise_IsRefusedAsACopiedKey()
    {
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, settings);

        (await SignInWithAsync(app, o => key.Assert(o, signCount: 5))).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var copied in new uint[] { 3, 5, 0 })
        {
            var refused = await SignInWithAsync(app, o => key.Assert(o, signCount: copied));
            refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"a counter of {copied} after 5");
            (await refused.Content.ReadAsStringAsync()).Should().Contain("Passkeys.CounterWentBack");
        }
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(5, "a refused sign-in moves nothing");

        (await SignInWithAsync(app, o => key.Assert(o, signCount: 6))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASyncedPasskeyThatAlwaysReportsZero_KeepsWorking()
    {
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, settings, synced: true);

        (await SignInWithAsync(app, o => key.Assert(o, signCount: 0))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SignInWithAsync(app, o => key.Assert(o, signCount: 0))).StatusCode.Should().Be(HttpStatusCode.OK);
        using var listed = await OkJson(await settings.GetAsync("/api/auth/passkeys"));
        listed.RootElement.EnumerateArray().Single().GetProperty("synced").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WrongSignaturesAreRefused_AndFiveOfThemStopTheAccountsPasskeysForAWhile()
    {
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, settings);
        using var forger = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        for (var i = 0; i < 5; i++)
            (await SignInWithAsync(app, o => key.Assert(o, signWith: forger))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var locked = await SignInWithAsync(app, o => key.Assert(o));
        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "the same allowance as a password");
        (await locked.Content.ReadAsStringAsync()).Should().NotContain("\"token\"");
    }

    [Fact]
    public async Task AnAnswerForAnotherSite_OrToAChallengeAlreadyUsed_IsRefused()
    {
        await using var app = new PasskeyApp();
        var (settings, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, settings);

        (await SignInWithAsync(app, o => key.Assert(o, origin: "https://evil.example"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var browser = app.CreateClient();
        var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/sign-in/options");
        (await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new { ceremonyId = ceremony, credential = key.Assert(options) }))).StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new { ceremonyId = ceremony, credential = key.Assert(options) }));
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await replay.Content.ReadAsStringAsync()).Should().Contain("Passkeys.CeremonyExpired");
    }

    [Fact]
    public async Task WhereTwoFactorIsOn_APasskeyStandsInForTheCode_AndTheSessionKeepsHowItBegan()
    {
        await using var app = new PasskeyApp();
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AuthenticatorApp.TurnOnAsync(settings);
        var key = await AddPasskeyAsync(app, settings);

        var browser = app.CreateClient();
        using var login = await OkJson(await browser.PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = AuthenticatorApp.Password })));
        var step = login.RootElement.GetProperty("twoFactorToken").GetString()!;

        var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/second-factor/options", new { twoFactorToken = step });
        options.GetProperty("allowCredentials").EnumerateArray().Select(c => c.GetProperty("id").GetString()).Should().Equal(key.CredentialIdText);
        using var done = await OkJson(await browser.PostAsync("/api/auth/passkeys/second-factor",
            Body(new { twoFactorToken = step, ceremonyId = ceremony, credential = key.Assert(options, userVerified: false) })));
        AuthenticatorApp.Claim(done.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Password);

        // After single sign-on, the session it gives is a single sign-on one.
        string ssoStep;
        using (var scope = app.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(userId);
            ssoStep = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTwoFactorToken(user!, SignInMethods.Sso);
        }
        var (ssoCeremony, ssoOptions) = await CeremonyAsync(browser, "/api/auth/passkeys/second-factor/options", new { twoFactorToken = ssoStep });
        using var ssoDone = await OkJson(await browser.PostAsync("/api/auth/passkeys/second-factor",
            Body(new { twoFactorToken = ssoStep, ceremonyId = ssoCeremony, credential = key.Assert(ssoOptions) })));
        AuthenticatorApp.Claim(ssoDone.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);
    }

    [Fact]
    public async Task TheSecondFactorTakesTheirOwnPasskey_AndWrongOnesCountAgainstTheCodesAllowance()
    {
        await using var app = new PasskeyApp();
        var (anna, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var secret = await AuthenticatorApp.TurnOnAsync(anna);
        await AddPasskeyAsync(app, anna);
        var (marc, _) = await SignInAsync(app, "marc@pavillon46.ch");
        var marcsKey = await AddPasskeyAsync(app, marc);

        var browser = app.CreateClient();
        using var login = await OkJson(await browser.PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = AuthenticatorApp.Password })));
        var step = login.RootElement.GetProperty("twoFactorToken").GetString()!;

        for (var i = 0; i < 5; i++)
        {
            var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/second-factor/options", new { twoFactorToken = step });
            (await browser.PostAsync("/api/auth/passkeys/second-factor", Body(new { twoFactorToken = step, ceremonyId = ceremony, credential = marcsKey.Assert(options) })))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "marc's passkey is not anna's second factor");
        }

        (await browser.PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = AuthenticatorApp.CodeNow(secret) })))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "one allowance for the second step, whichever way it is tried");
    }

    [Fact]
    public async Task ARemovedPasskeySignsNobodyIn_AndOnlyItsOwnerCanRemoveIt()
    {
        await using var app = new PasskeyApp();
        var (anna, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = await AddPasskeyAsync(app, anna);
        var (marc, _) = await SignInAsync(app, "marc@pavillon46.ch");
        var passkeyId = await app.Services.Read(db => db.UserPasskeys.Single().Id);

        (await marc.DeleteAsync($"/api/auth/passkeys/{passkeyId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anna.DeleteAsync($"/api/auth/passkeys/{passkeyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var listed = await OkJson(await anna.GetAsync("/api/auth/passkeys"));
        listed.RootElement.GetArrayLength().Should().Be(0);

        var refused = await SignInWithAsync(app, o => key.Assert(o));
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Passkeys.NotRecognised");
    }

    /// <summary>
    /// Adds a passkey and turns 2FA on for the client's account, in an app that keeps the reset
    /// links it mails (passkeys are on there for its address, https://app.cadence.test).
    /// </summary>
    private static async Task<SoftwareAuthenticator> AddPasskeyAndTwoFactorAsync(HttpClient client)
    {
        var (ceremony, options) = await CeremonyAsync(client, "/api/auth/passkeys/registration/options", new { password = AuthenticatorApp.Password });
        var key = new SoftwareAuthenticator(PasskeyApp.RpId, PasskeyApp.Origin);
        (await OkJson(await client.PostAsync("/api/auth/passkeys/registration", Body(new { ceremonyId = ceremony, name = "Theirs", credential = key.Register(options) })))).Dispose();
        await AuthenticatorApp.TurnOnAsync(client);
        return key;
    }

    private static async Task ResetPasswordThroughTheInboxAsync(SsoApp app, string email, string password)
    {
        var anonymous = app.CreateClient();
        (await anonymous.PostAsync("/api/auth/forgot-password", Body(new { email }))).StatusCode.Should().Be(HttpStatusCode.OK);
        var reset = await anonymous.PostAsync("/api/auth/reset-password", Body(new { token = app.ResetToken(email), password }));
        reset.StatusCode.Should().Be(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TakingBackAnAddressWithTheResetLink_RemovesThePasskeysAndTwoFactorWhoeverRegisteredItFirstAdded()
    {
        // Someone registered anna's address before she did and never proved the inbox. The
        // password was theirs, so they could add a passkey and turn 2FA on. Anna takes the
        // account back with the reset link sent to her inbox: their passkey, which signs in
        // without any password, must not let them back in beside her.
        await using var app = new SsoApp();
        var (squatter, _) = await SignInAsync(app, "anna@pavillon46.ch");
        var theirKey = await AddPasskeyAndTwoFactorAsync(squatter);

        await ResetPasswordThroughTheInboxAsync(app, "anna@pavillon46.ch", "annas own passphrase 2");

        var browser = app.CreateClient();
        var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/sign-in/options");
        var refused = await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new { ceremonyId = ceremony, credential = theirKey.Assert(options) }));
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await refused.Content.ReadAsStringAsync());
        (await refused.Content.ReadAsStringAsync()).Should().NotContain("\"token\"");
        (await app.Services.Read(db => db.UserPasskeys.Count())).Should().Be(0);

        // Their authenticator app is not hers either: her new password alone signs her in.
        using var login = await OkJson(await browser.PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = "annas own passphrase 2" })));
        login.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
        login.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AResetOfAnAccountWhoseAddressWasConfirmed_KeepsItsPasskeysAndTwoFactor()
    {
        // Someone who proved their address and only forgot the password keeps the rest of their way in.
        await using var app = new SsoApp();
        var (anna, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.FindAsync(userId))!.EmailVerifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var key = await AddPasskeyAndTwoFactorAsync(anna);

        await ResetPasswordThroughTheInboxAsync(app, "anna@pavillon46.ch", "annas own passphrase 2");

        var browser = app.CreateClient();
        var (ceremony, options) = await CeremonyAsync(browser, "/api/auth/passkeys/sign-in/options");
        using (await OkJson(await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new { ceremonyId = ceremony, credential = key.Assert(options) })))) { }
        using var login = await OkJson(await browser.PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = "annas own passphrase 2" })));
        login.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AReadOnlyMemberManagesTheirOwnPasskeys()
    {
        // Their own sign-in is theirs to change, as their password and 2FA are.
        await using var app = new PasskeyApp();
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        var (viewer, viewerId) = await SignInAsync(app, "viewer@pavillon46.ch");
        await AddMemberAsync(app, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        await AddPasskeyAsync(app, viewer);
        (await viewer.DeleteAsync($"/api/auth/passkeys/{await app.Services.Read(db => db.UserPasskeys.Single().Id)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>The app served from a plain-http address that is not this machine: passkeys cannot work there.</summary>
    private sealed class PlainHttpApp : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Email:FrontendBaseUrl"] = "http://app.example.ch" }));
        }
    }

    [Fact]
    public async Task WhereBrowsersWouldRefuseThem_PasskeysAreOff_AndSaySo()
    {
        await using var off = new PlainHttpApp();
        var client = off.CreateClient();
        using (var options = JsonDocument.Parse(await client.GetStringAsync("/api/auth/sign-in-options")))
            options.RootElement.GetProperty("passkeys").GetBoolean().Should().BeFalse();
        (await client.PostAsync("/api/auth/passkeys/sign-in/options", Body(new { }))).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        await using var app = new PasskeyApp();
        using var on = JsonDocument.Parse(await app.CreateClient().GetStringAsync("/api/auth/sign-in-options"));
        on.RootElement.GetProperty("passkeys").GetBoolean().Should().BeTrue();
        on.RootElement.GetProperty("sso").GetArrayLength().Should().Be(0);
    }
}
