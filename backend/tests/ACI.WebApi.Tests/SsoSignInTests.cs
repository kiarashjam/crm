using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Single sign-on with Google and Microsoft over HTTP, against a fake provider: the round
/// trip, what the id token must say to be believed, and the session it ends in.
/// </summary>
public class SsoSignInTests
{
    /// <summary>A browser signing on: the app's secret for the hand-back, and the requests it makes.</summary>
    private sealed class Browser
    {
        private readonly SsoApp _app;

        public Browser(SsoApp app)
        {
            _app = app;
            Client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        }

        public HttpClient Client { get; }

        public string Verifier { get; }

        public string Challenge => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

        /// <summary>Starts at the API and returns the provider's sign-in address it was sent to.</summary>
        public async Task<Uri> StartAsync(string provider)
        {
            var res = await Client.GetAsync($"/api/auth/sso/{provider}/start?challenge={Challenge}");
            res.StatusCode.Should().Be(HttpStatusCode.Redirect, await res.Content.ReadAsStringAsync());
            return res.Headers.Location!;
        }

        /// <summary>The provider sending the browser back; returns where the API sent it next.</summary>
        public async Task<Uri> ReturnAsync(string provider, string query)
        {
            var res = await Client.GetAsync($"/api/auth/sso/{provider}/callback?{query}");
            res.StatusCode.Should().Be(HttpStatusCode.Redirect, await res.Content.ReadAsStringAsync());
            return res.Headers.Location!;
        }

        public Task<HttpResponseMessage> CompleteAsync(string code, string? verifier = null) =>
            Client.PostAsync("/api/auth/sso/complete", Body(new { code, verifier = verifier ?? Verifier }));

        /// <summary>
        /// The whole sign-on, with the provider answering with <paramref name="claims"/> (given the
        /// round trip's nonce); returns the app address the browser lands on.
        /// </summary>
        public async Task<Uri> SignOnAsync(string provider, Func<string, Dictionary<string, object>> claims, Func<Dictionary<string, object>, string>? sign = null)
        {
            var authorize = await StartAsync(provider);
            var query = Query(authorize);
            _app.Provider.IdToken = _ => (sign ?? (c => _app.Provider.Sign(c)))(claims(query["nonce"]));
            return await ReturnAsync(provider, $"code=the-code&state={Uri.EscapeDataString(query["state"])}");
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "");

    /// <summary>The app address's fragment, as the app's /auth/sso page reads it.</summary>
    private static Dictionary<string, string> Fragment(Uri landing)
    {
        landing.GetLeftPart(UriPartial.Path).Should().Be($"{SsoApp.AppBase}/auth/sso");
        return landing.Fragment.TrimStart('#').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => Uri.UnescapeDataString(kv[1]));
    }

    private static async Task<JsonDocument> OkJson(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    private static HttpClient As(SsoApp app, string token)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>An account with a password whose address is confirmed, as one in use would be.</summary>
    private static async Task<(HttpClient Client, Guid UserId)> ConfirmedAccountAsync(SsoApp app, string email)
    {
        var (client, userId) = await SignInAsync(app, email);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.FindAsync(userId))!.EmailVerifiedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (client, userId);
    }

    [Fact]
    public async Task AVerifiedGoogleAccountSignsInAsTheAccountWithItsAddress_WithTheSessionAPasswordGives()
    {
        await using var app = new SsoApp();
        var (_, userId) = await ConfirmedAccountAsync(app, "anna@pavillon46.ch");
        var browser = new Browser(app);

        // To Google, with PKCE, a state and a nonce, and the callback registered with Google.
        var authorize = await browser.StartAsync("google");
        authorize.GetLeftPart(UriPartial.Path).Should().Be(FakeOidcProvider.GoogleAuthorize);
        var query = Query(authorize);
        query["client_id"].Should().Be(FakeOidcProvider.GoogleClientId);
        query["response_type"].Should().Be("code");
        query["redirect_uri"].Should().Be($"{SsoApp.ApiBase}/api/auth/sso/google/callback");
        query["scope"].Split(' ').Should().Contain(["openid", "email"]);
        query["code_challenge_method"].Should().Be("S256");
        query["state"].Should().NotBeNullOrEmpty();
        query["nonce"].Should().NotBeNullOrEmpty();

        app.Provider.IdToken = _ => app.Provider.Sign(FakeOidcProvider.GoogleClaims("google-sub-1", "Anna@Pavillon46.ch", query["nonce"]));
        var landing = Fragment(await browser.ReturnAsync("google", $"code=the-code&state={Uri.EscapeDataString(query["state"])}"));

        // The code was redeemed with the verifier whose hash went to Google, and the client secret.
        var redeemed = app.Provider.TokenRequests.Single().Form;
        redeemed["grant_type"].Should().Be("authorization_code");
        redeemed["code"].Should().Be("the-code");
        redeemed["client_secret"].Should().Be(FakeOidcProvider.GoogleClientSecret);
        redeemed["redirect_uri"].Should().Be(query["redirect_uri"]);
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(redeemed["code_verifier"]))).Should().Be(query["code_challenge"]);

        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        done.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
        var token = done.RootElement.GetProperty("token").GetString()!;
        AuthenticatorApp.Claim(token, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);
        AuthenticatorApp.Claim(token, "sstamp").Should().NotBeNull("it is the session token a password gives, stamp and all");

        using var me = await OkJson(await As(app, token).GetAsync("/api/auth/me"));
        me.RootElement.GetProperty("id").GetGuid().Should().Be(userId);

        // Their password keeps working: the account was theirs, and stays as it was.
        (await app.CreateClient().PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = AuthenticatorApp.Password })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnceLinked_TheProvidersAccountFindsThePerson_EvenAfterItsAddressChangesThere()
    {
        await using var app = new SsoApp();
        var (_, userId) = await ConfirmedAccountAsync(app, "anna@pavillon46.ch");
        Fragment(await new Browser(app).SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-sub-1", "anna@pavillon46.ch", n)))
            .Should().ContainKey("code");

        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-sub-1", "anna.keller@gmail.com", n)));
        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
    }

    [Fact]
    public async Task SomeoneNewArrivingBySso_GetsAnAccountWithAConfirmedAddress_AndNoWorkspace()
    {
        await using var app = new SsoApp();
        var (_, ownerId) = await ConfirmedAccountAsync(app, "owner@pavillon46.ch");
        await CreateAsync(app, ownerId, "Pavillon 46");

        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-new", "newcomer@gmail.com", n)));
        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        var token = done.RootElement.GetProperty("token").GetString()!;
        done.RootElement.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeTrue();
        done.RootElement.GetProperty("user").GetProperty("name").GetString().Should().Be("Anna Keller");

        // Where a new sign-up lands: no workspace until someone invites them.
        using var orgs = await OkJson(await As(app, token).GetAsync("/api/organizations"));
        orgs.RootElement.GetArrayLength().Should().Be(0);
        (await app.Services.Read(db => db.OrganizationMembers.Count(m => m.User.Email == "newcomer@gmail.com"))).Should().Be(0);
    }

    [Fact]
    public async Task AnAddressTheProviderDoesNotVouchFor_SignsNobodyIn_AndMakesNoAccount()
    {
        await using var app = new SsoApp();
        await ConfirmedAccountAsync(app, "anna@pavillon46.ch");

        var landing = Fragment(await new Browser(app).SignOnAsync("google",
            n => FakeOidcProvider.GoogleClaims("google-x", "anna@pavillon46.ch", n, verified: false)));

        landing.Should().ContainKey("error").WhoseValue.Should().Be("email_not_verified");
        (await app.Services.Read(db => db.UserExternalLogins.Count())).Should().Be(0);

        var stranger = Fragment(await new Browser(app).SignOnAsync("google",
            n => FakeOidcProvider.GoogleClaims("google-y", "someone@else.ch", n, verified: false)));
        stranger["error"].Should().Be("email_not_verified");
        (await app.Services.Read(db => db.Users.Any(u => u.Email == "someone@else.ch"))).Should().BeFalse();
    }

    [Fact]
    public async Task AnIdTokenForAnotherApp_IsRefused()
    {
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("google", n =>
        {
            var claims = FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n);
            claims["aud"] = "someone-elses-app.apps.googleusercontent.com";
            claims["azp"] = "someone-elses-app.apps.googleusercontent.com";
            return claims;
        }));
        landing["error"].Should().Be("invalid_token");
        (await app.Services.Read(db => db.Users.Any(u => u.Email == "anna@gmail.com"))).Should().BeFalse();
    }

    [Fact]
    public async Task AnExpiredIdToken_IsRefused()
    {
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("google", n =>
        {
            var claims = FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n);
            claims["iat"] = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
            claims["exp"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
            return claims;
        }));
        landing["error"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task AnIdTokenWithoutThisSignInsNonce_IsRefused()
    {
        // A token from another round trip (one an attacker started) cannot finish this one.
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("google",
            _ => FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", "another-sign-ins-nonce")));
        landing["error"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task AnIdTokenSignedWithAKeyTheProviderNeverPublished_IsRefused()
    {
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("google",
            n => FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n),
            claims => app.Provider.Sign(claims, app.Provider.Stranger)));
        landing["error"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task AnIdTokenFromAnotherIssuer_IsRefused()
    {
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("google", n =>
        {
            var claims = FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n);
            claims["iss"] = "https://evil.example";
            return claims;
        }));
        landing["error"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task AStateNobodyIssued_OrOneAlreadyUsed_FindsNothing()
    {
        await using var app = new SsoApp();
        var browser = new Browser(app);

        Fragment(await browser.ReturnAsync("google", "code=the-code&state=made-up")).Should().ContainKey("error").WhoseValue.Should().Be("expired");

        var authorize = Query(await browser.StartAsync("google"));
        app.Provider.IdToken = _ => app.Provider.Sign(FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", authorize["nonce"]));
        var state = Uri.EscapeDataString(authorize["state"]);
        Fragment(await browser.ReturnAsync("google", $"code=the-code&state={state}")).Should().ContainKey("code");

        // The same address again, as a replay: the round trip is over.
        Fragment(await browser.ReturnAsync("google", $"code=the-code&state={state}"))["error"].Should().Be("expired");
        app.Provider.TokenRequests.Should().HaveCount(1, "nothing is redeemed for a state that finds no round trip");

        // A Google round trip cannot be finished at Microsoft's callback.
        var second = Query(await browser.StartAsync("google"));
        Fragment(await browser.ReturnAsync("microsoft", $"code=the-code&state={Uri.EscapeDataString(second["state"])}"))["error"].Should().Be("expired");
    }

    [Fact]
    public async Task ThePersonCancellingAtTheProvider_ComesBackToTheAppSayingSo()
    {
        await using var app = new SsoApp();
        var browser = new Browser(app);
        var authorize = Query(await browser.StartAsync("google"));
        Fragment(await browser.ReturnAsync("google", $"error=access_denied&state={Uri.EscapeDataString(authorize["state"])}"))["error"].Should().Be("cancelled");
        app.Provider.TokenRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task TheHandBackWorksOnce_AndOnlyInTheBrowserThatStarted()
    {
        await using var app = new SsoApp();
        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n)));

        // Someone who got hold of the code, without the browser's secret, gets nothing; nor does the code work after.
        var stolen = await browser.CompleteAsync(landing["code"], verifier: Base64Url(RandomNumberGenerator.GetBytes(32)));
        stolen.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await stolen.Content.ReadAsStringAsync()).Should().Contain("Sso.HandoffNotYours").And.NotContain("\"token\"");
        (await browser.CompleteAsync(landing["code"])).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var again = new Browser(app);
        var second = Fragment(await again.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-1", "anna@gmail.com", n)));
        (await again.CompleteAsync(second["code"])).StatusCode.Should().Be(HttpStatusCode.OK);
        var reused = await again.CompleteAsync(second["code"]);
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await reused.Content.ReadAsStringAsync()).Should().Contain("Sso.HandoffExpired");
    }

    [Fact]
    public async Task TwoFactorSignInStillHolds_AndFinishingWithTheCodeGivesASingleSignOnSession()
    {
        await using var app = new SsoApp();
        var (settings, _) = await ConfirmedAccountAsync(app, "anna@pavillon46.ch");
        var secret = await AuthenticatorApp.TurnOnAsync(settings);

        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-1", "anna@pavillon46.ch", n)));
        using var step = await OkJson(await browser.CompleteAsync(landing["code"]));
        step.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
        step.RootElement.GetProperty("token").ValueKind.Should().Be(JsonValueKind.Null);

        using var done = await OkJson(await app.CreateClient().PostAsync("/api/auth/login/2fa",
            Body(new { twoFactorToken = step.RootElement.GetProperty("twoFactorToken").GetString(), code = AuthenticatorApp.CodeNow(secret) })));
        AuthenticatorApp.Claim(done.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);
    }

    [Fact]
    public async Task AnUnconfirmedAccountWithTheAddress_IsTakenBackByTheAddresssOwner()
    {
        // Someone registered anna's address before she did, chose its password, and never
        // proved the inbox. When anna signs on with Google, that person must not stay in.
        await using var app = new SsoApp();
        var (squatter, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        (await squatter.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-1", "anna@pavillon46.ch", n)));
        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        done.RootElement.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeTrue();

        (await squatter.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "their sessions end");
        (await app.CreateClient().PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = AuthenticatorApp.Password })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "their password goes");

        // Anna can give the account a password of her own, through the link sent to her inbox.
        var anonymous = app.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", Body(new { email = "anna@pavillon46.ch" }));
        (await anonymous.PostAsync("/api/auth/reset-password", Body(new { token = app.ResetToken("anna@pavillon46.ch"), password = "annas own passphrase 2" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsync("/api/auth/login", Body(new { email = "anna@pavillon46.ch", password = "annas own passphrase 2" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MicrosoftsAddressCountsOnlyWhenItsDomainOwnerIsVerified()
    {
        await using var app = new SsoApp();
        var (_, userId) = await ConfirmedAccountAsync(app, "marc@pavillon46.ch");

        // Without xms_edov, a tenant's admin could have typed anyone's address on an account.
        var unverified = Fragment(await new Browser(app).SignOnAsync("microsoft",
            n => FakeOidcProvider.MicrosoftClaims("ms-sub-1", "marc@pavillon46.ch", n, domainVerified: false)));
        unverified["error"].Should().Be("email_not_verified");

        var browser = new Browser(app);
        var authorize = await browser.StartAsync("microsoft");
        authorize.GetLeftPart(UriPartial.Path).Should().Be(FakeOidcProvider.MicrosoftAuthorize);
        var query = Query(authorize);
        query["client_id"].Should().Be(FakeOidcProvider.MicrosoftClientId);
        app.Provider.IdToken = _ => app.Provider.Sign(FakeOidcProvider.MicrosoftClaims("ms-sub-1", "marc@pavillon46.ch", query["nonce"]));
        var landing = Fragment(await browser.ReturnAsync("microsoft", $"code=the-code&state={Uri.EscapeDataString(query["state"])}"));
        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        app.Provider.TokenRequests.Last().Form["client_secret"].Should().Be(FakeOidcProvider.MicrosoftClientSecret);
    }

    [Fact]
    public async Task WithWorkAndSchoolAccountsOnly_APersonalMicrosoftAccountIsRefused()
    {
        await using var app = new SsoApp();
        var landing = Fragment(await new Browser(app).SignOnAsync("microsoft",
            n => FakeOidcProvider.MicrosoftClaims("ms-personal", "marc@outlook.com", n, tenant: "9188040d-6c67-4c5b-b112-36a304b66dad")));
        landing["error"].Should().Be("invalid_token");

        // A token whose issuer names another tenant than its tid is refused too.
        var mismatched = Fragment(await new Browser(app).SignOnAsync("microsoft", n =>
        {
            var claims = FakeOidcProvider.MicrosoftClaims("ms-2", "marc@pavillon46.ch", n);
            claims["tid"] = Guid.NewGuid().ToString();
            return claims;
        }));
        mismatched["error"].Should().Be("invalid_token");
    }

    [Fact]
    public async Task ASingleSignOnSession_EndsLikeAnyOtherWhenTheSecurityStampChanges()
    {
        await using var app = new SsoApp();
        var (settings, _) = await ConfirmedAccountAsync(app, "anna@pavillon46.ch");

        var browser = new Browser(app);
        var landing = Fragment(await browser.SignOnAsync("google", n => FakeOidcProvider.GoogleClaims("google-1", "anna@pavillon46.ch", n)));
        using var done = await OkJson(await browser.CompleteAsync(landing["code"]));
        var sso = As(app, done.RootElement.GetProperty("token").GetString()!);
        (await sso.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Turning 2FA on in another session ends every session from before, this one too.
        await AuthenticatorApp.TurnOnAsync(settings);
        (await sso.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithoutClientIdsNothingIsOffered_AndStartingSendsTheBrowserBackToTheApp()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var providers = JsonDocument.Parse(await client.GetStringAsync("/api/auth/sso/providers"));
        providers.RootElement.GetArrayLength().Should().Be(0);

        var start = await client.GetAsync($"/api/auth/sso/google/start?challenge={new string('a', 43)}");
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);
        start.Headers.Location!.Fragment.Should().Be("#error=not_configured");
    }

    [Fact]
    public async Task StartingWithoutTheAppsChallenge_GoesNowhere()
    {
        await using var app = new SsoApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var providers = JsonDocument.Parse(await client.GetStringAsync("/api/auth/sso/providers")))
            providers.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetString()).Should().Equal("google", "microsoft");

        var start = await client.GetAsync("/api/auth/sso/google/start");
        start.Headers.Location!.ToString().Should().Be($"{SsoApp.AppBase}/auth/sso#error=bad_request");
        (await app.Services.Read(db => db.AuthChallenges.Count())).Should().Be(0);
    }
}
