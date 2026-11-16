using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.SignIn;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Single sign-on and passkeys on a real SQL Server: the migration's tables and unique keys, a
/// sign-in step taken by one of several requests at once, the signature counter saved with the
/// old value as its concurrency token, and two sign-ons racing to make one new account.
/// </summary>
/// <remarks>
/// The in-memory provider has no unique indexes and no conditional UPDATE or DELETE, so what
/// keeps a step single-use, a counter from being saved twice and a new person from getting two
/// accounts only shows on a real server.
/// </remarks>
public class SignInOnSqlServerTests
{
    private const string AppOrigin = "https://app.cadence.test";
    private const string RpId = "app.cadence.test";

    private static async Task RunAsync(Func<SqlServerApp, FakeOidcProvider, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var provider = new FakeOidcProvider();
            using var app = SqlServerApp.Start(database, services: services =>
            {
                services.Configure<PasskeySettings>(o =>
                {
                    o.RelyingPartyId = RpId;
                    o.Origins = AppOrigin;
                });
                services.Configure<SingleSignOnSettings>(o =>
                {
                    o.Google.ClientId = FakeOidcProvider.GoogleClientId;
                    o.Google.ClientSecret = FakeOidcProvider.GoogleClientSecret;
                    o.ApiBaseUrl = "https://api.cadence.test";
                });
                services.Configure<EmailSettings>(o => o.FrontendBaseUrl = AppOrigin);
                services.AddHttpClient(OidcMetadataSource.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => provider);
            });
            await test(app, provider);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<JsonDocument> OkJson(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Dictionary<string, string> Parts(string text, char separator) =>
        text.TrimStart(separator).Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "");

    [SqlServerFact]
    public Task APasskeySignsIn_AndOneCounterValueIsNeverSavedTwice() => RunAsync(async (app, _) =>
    {
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        var key = new SoftwareAuthenticator(RpId, AppOrigin);
        using (var options = await OkJson(await settings.PostAsync("/api/auth/passkeys/registration/options", Body(new { password = AuthenticatorApp.Password }))))
        {
            using var added = await OkJson(await settings.PostAsync("/api/auth/passkeys/registration", Body(new
            {
                ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
                name = "MacBook",
                credential = key.Register(options.RootElement.GetProperty("options")),
            })));
        }

        // Signing in finds the passkey by its credential id (varbinary) and saves the counter it reported.
        var browser = app.CreateClient();
        using (var options = await OkJson(await browser.PostAsync("/api/auth/passkeys/sign-in/options", Body(new { }))))
        {
            using var done = await OkJson(await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new
            {
                ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
                credential = key.Assert(options.RootElement.GetProperty("options")),
            })));
            done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        }
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(1);

        // Two sign-ins that both read counter 1 (a copied key used at the same moment as the
        // real one): the save that comes second finds the counter moved and changes nothing.
        using var firstScope = app.Services.CreateScope();
        using var secondScope = app.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seenFirst = await first.UserPasskeys.SingleAsync();
        var seenSecond = await second.UserPasskeys.SingleAsync();
        seenFirst.SignCount = 2;
        seenSecond.SignCount = 2;
        await first.SaveChangesAsync();
        var late = async () => await second.SaveChangesAsync();
        await late.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // One authenticator's credential is one passkey, whoever tries to add it again.
        var (_, marcId) = await SignInAsync(app, "marc@pavillon46.ch");
        first.UserPasskeys.Add(new UserPasskey
        {
            Id = Guid.NewGuid(), UserId = marcId, CredentialId = key.CredentialId, PublicKey = [1, 2, 3], Name = "Copy", CreatedAtUtc = DateTime.UtcNow,
        });
        var copy = async () => await first.SaveChangesAsync();
        await copy.Should().ThrowAsync<DbUpdateException>();
    });

    [SqlServerFact]
    public Task ASignInStepIsTakenByOneOfManyRequestsAtOnce() => RunAsync(async (app, _) =>
    {
        // Many rounds, because the race only sometimes ends in a deadlock: the server then
        // picks one request as the victim, and that request must be refused like any other
        // loser, not fail. One round alone passed most of the time even before that was so.
        for (var round = 0; round < Rounds; round++)
        {
            var secret = $"the-secret-only-the-browser-holds-{round:D4}-0123456789";
            using (var scope = app.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IAuthChallenges>()
                    .CreateAsync(AuthChallengePurposes.SsoHandoff, secret, userId: null, payload: "{}", TimeSpan.FromMinutes(2));

            var takes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IAuthChallenges>().TakeAsync(AuthChallengePurposes.SsoHandoff, secret);
            })));

            takes.Count(step => step != null).Should().Be(1, $"a step is finished once, by whichever request deletes it (round {round})");
        }
        (await app.Services.Read(db => db.AuthChallenges.Count())).Should().Be(0);
    });

    private const int Rounds = 40;

    [SqlServerFact]
    public Task TwoSignOnsForOneNewPersonAtOnce_MakeOneAccountWithOneLink() => RunAsync(async (app, provider) =>
    {
        // Two tabs start signing on; Google answers both for the same new account.
        var trips = new List<(HttpClient Browser, string Verifier, string State)>();
        var nonces = new Dictionary<string, string>();
        for (var i = 0; i < 2; i++)
        {
            var browser = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var start = await browser.GetAsync($"/api/auth/sso/google/start?challenge={challenge}");
            start.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var query = Parts(start.Headers.Location!.Query, '?');
            nonces[$"code-{i}"] = query["nonce"];
            trips.Add((browser, verifier, query["state"]));
        }
        provider.IdToken = form => provider.Sign(FakeOidcProvider.GoogleClaims("google-new-person", "new.member@pavillon46.ch", nonces[form["code"]]));

        var landings = await Task.WhenAll(trips.Select((trip, i) => Task.Run(async () =>
        {
            var back = await trip.Browser.GetAsync($"/api/auth/sso/google/callback?code=code-{i}&state={Uri.EscapeDataString(trip.State)}");
            back.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return Parts(back.Headers.Location!.Fragment, '#');
        })));

        var signedIn = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            landings[i].Should().ContainKey("code", "both sign-ons end signed in, the second finding what the first made");
            using var done = await OkJson(await trips[i].Browser.PostAsync("/api/auth/sso/complete", Body(new { code = landings[i]["code"], verifier = trips[i].Verifier })));
            signedIn.Add(done.RootElement.GetProperty("user").GetProperty("id").GetGuid());
        }
        signedIn.Distinct().Should().ContainSingle();
        (await app.Services.Read(db => db.Users.Count(u => u.Email == "new.member@pavillon46.ch"))).Should().Be(1);
        (await app.Services.Read(db => db.UserExternalLogins.Count())).Should().Be(1);
    });
}
