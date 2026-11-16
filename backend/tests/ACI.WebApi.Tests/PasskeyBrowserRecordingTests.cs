using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.SignIn;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Real browsers' WebAuthn answers (Fixtures/Passkeys/browser-recordings.json), recorded by
/// another WebAuthn project against its own site and replayed here byte for byte.
/// </summary>
/// <remarks>
/// PasskeyRecordedCeremonyTests replays answers this suite's own software authenticator made,
/// so a mistake shared by that authenticator and the server would pass there. These were made
/// by real authenticators in real browsers (client data in an older browser's shape, a security
/// key's attestation object, an RS256 key), so they show the server reads what browsers send.
/// They come with no registration of their own for the sign-ins, so those credentials are put
/// on the account directly, with the public keys recorded beside them.
/// </remarks>
public class PasskeyBrowserRecordingTests
{
    private const string FixtureName = "Passkeys.browser-recordings.json";

    /// <summary>The app as the recording site was: relying party localhost, served from http://localhost:5000.</summary>
    private sealed class RecordingSiteApp : CustomWebApplicationFactory
    {
        public QueuedChallenges Challenges { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Passkeys:RelyingPartyId"] = "localhost",
                ["Passkeys:Origins"] = "http://localhost:5000",
                ["Email:FrontendBaseUrl"] = "http://localhost:5000",
            }));
            builder.ConfigureTestServices(services => services.AddSingleton<PasskeyChallengeSource>(Challenges));
        }
    }

    private static JsonElement Fixture(string part)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FixtureName)
            ?? throw new InvalidOperationException($"The embedded fixture {FixtureName} is missing.");
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty(part).Clone();
    }

    private static byte[] Bytes(JsonElement element) => SoftwareAuthenticator.FromB64(element.GetString()!);

    /// <summary>The recorded answer with its user handle replaced, or taken out (null). The handle is not signed.</summary>
    private static JsonObject WithUserHandle(JsonElement credential, byte[]? userHandle)
    {
        var answer = JsonNode.Parse(credential.GetRawText())!.AsObject();
        var response = answer["response"]!.AsObject();
        response.Remove("userHandle");
        if (userHandle != null) response["userHandle"] = SoftwareAuthenticator.B64(userHandle);
        return answer;
    }

    private static async Task<JsonDocument> OkJson(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    private static async Task RefusedAsync(HttpResponseMessage res, string code, string because)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{because}: {body}");
        body.Should().Contain(code, because);
    }

    /// <summary>Puts a recorded credential on the account, as its registration would have.</summary>
    private static async Task PutOnAccountAsync(RecordingSiteApp app, Guid userId, JsonElement recording)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserPasskeys.Add(new UserPasskey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CredentialId = Bytes(recording.GetProperty("credential").GetProperty("rawId")),
            PublicKey = Bytes(recording.GetProperty("publicKey")),
            SignCount = recording.GetProperty("signCountBefore").GetInt64(),
            Name = "Recorded key",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>A password sign-in of an account with 2FA on, up to the step token.</summary>
    private static async Task<string> StepTokenAsync(RecordingSiteApp app, string email)
    {
        using var login = await OkJson(await app.CreateClient().PostAsync("/api/auth/login", Body(new { email, password = AuthenticatorApp.Password })));
        return login.RootElement.GetProperty("twoFactorToken").GetString()!;
    }

    /// <summary>The second step with a passkey: options for the step token (handing out the recording's challenge), then the answer.</summary>
    private static async Task<HttpResponseMessage> SecondFactorAsync(RecordingSiteApp app, JsonElement recording, string step, JsonNode answer)
    {
        app.Challenges.Queue.Enqueue(Bytes(recording.GetProperty("challenge")));
        var browser = app.CreateClient();
        using var options = await OkJson(await browser.PostAsync("/api/auth/passkeys/second-factor/options", Body(new { twoFactorToken = step })));
        options.RootElement.GetProperty("options").GetProperty("challenge").GetString().Should().Be(recording.GetProperty("challenge").GetString());
        return await browser.PostAsync("/api/auth/passkeys/second-factor", Body(new
        {
            twoFactorToken = step,
            ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
            credential = answer,
        }));
    }

    private static async Task<HttpResponseMessage> PasswordlessAsync(RecordingSiteApp app, JsonElement recording, JsonNode answer)
    {
        app.Challenges.Queue.Enqueue(Bytes(recording.GetProperty("challenge")));
        var browser = app.CreateClient();
        using var options = await OkJson(await browser.PostAsync("/api/auth/passkeys/sign-in/options", Body(new { })));
        return await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new
        {
            ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
            credential = answer,
        }));
    }

    [Fact]
    public async Task ASecurityKeysRealRegistration_IsAccepted_WithTheKeyAndCounterItReported()
    {
        await using var app = new RecordingSiteApp();
        var recording = Fixture("registration");
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");

        app.Challenges.Queue.Enqueue(Bytes(recording.GetProperty("challenge")));
        using var options = await OkJson(await settings.PostAsync("/api/auth/passkeys/registration/options", Body(new { password = AuthenticatorApp.Password })));
        options.RootElement.GetProperty("options").GetProperty("rp").GetProperty("id").GetString().Should().Be("localhost");
        using var added = await OkJson(await settings.PostAsync("/api/auth/passkeys/registration", Body(new
        {
            ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
            name = "Security key",
            credential = recording.GetProperty("credential"),
        })));

        var stored = await app.Services.Read(db => db.UserPasskeys.Single());
        stored.UserId.Should().Be(userId);
        stored.CredentialId.Should().Equal(Bytes(recording.GetProperty("credential").GetProperty("rawId")));
        stored.PublicKey.Should().Equal(Bytes(recording.GetProperty("publicKey")), "the COSE key as the other project's verifier read it from the same bytes");
        stored.SignCount.Should().Be(recording.GetProperty("signCount").GetInt64());
        stored.IsBackupEligible.Should().BeFalse("a security key keeps its key to itself");
        stored.Aaguid.Should().Be(Guid.Empty, "'none' attestation names no model");
    }

    [Fact]
    public async Task ATouchThatDidNotVerifyThePerson_SignsNobodyInAlone_ButStandsInForTheCode_Once()
    {
        await using var app = new RecordingSiteApp();
        var recording = Fixture("touchOnly");
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AuthenticatorApp.TurnOnAsync(settings);
        await PutOnAccountAsync(app, userId, recording);
        var credential = recording.GetProperty("credential");

        // In place of the password the passkey must have verified her (PIN, fingerprint):
        // with her user handle set, as a passkey of hers would give it, the one thing wrong
        // with this answer is that the key took a touch only.
        await RefusedAsync(await PasswordlessAsync(app, recording, WithUserHandle(credential, userId.ToByteArray())),
            "Passkeys.NotRecognised", "a touch alone is one factor, not a sign-in");
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(77, "a refused answer moves nothing");

        // After her password, the same touch stands in for the code, exactly as recorded.
        using (var done = await OkJson(await SecondFactorAsync(app, recording, await StepTokenAsync(app, "anna@pavillon46.ch"), JsonNode.Parse(credential.GetRawText())!)))
        {
            done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
            AuthenticatorApp.Claim(done.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Password);
        }
        var passkey = await app.Services.Read(db => db.UserPasskeys.Single());
        passkey.SignCount.Should().Be(78);
        passkey.LastUsedAtUtc.Should().NotBeNull();

        // The same answer again presents counter 78 again, as a copy of the key would.
        await RefusedAsync(await SecondFactorAsync(app, recording, await StepTokenAsync(app, "anna@pavillon46.ch"), JsonNode.Parse(credential.GetRawText())!),
            "Passkeys.CounterWentBack", "a counter that does not rise");
    }

    [Fact]
    public async Task AnRs256Answer_IsVerified_AndHeldToWhoseItSaysItIs()
    {
        await using var app = new RecordingSiteApp();
        var recording = Fixture("rs256");
        var (settings, userId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AuthenticatorApp.TurnOnAsync(settings);
        await PutOnAccountAsync(app, userId, recording);
        var credential = recording.GetProperty("credential");

        // As recorded, the answer says it belongs to the account it was made for on the site
        // that recorded it, not to anna, who holds this credential here.
        await RefusedAsync(await SecondFactorAsync(app, recording, await StepTokenAsync(app, "anna@pavillon46.ch"), JsonNode.Parse(credential.GetRawText())!),
            "Passkeys.NotRecognised", "an answer naming someone else");

        // A sign-in that names nobody first must be told whose passkey it is.
        await RefusedAsync(await PasswordlessAsync(app, recording, WithUserHandle(credential, null)),
            "Passkeys.NotRecognised", "a passwordless answer without a user handle");
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(0);

        // Without the handle, as for a credential the browser was asked for by id, the RS256
        // signature itself checks out against the recorded key, and the counter moves to 1.
        using var done = await OkJson(await SecondFactorAsync(app, recording, await StepTokenAsync(app, "anna@pavillon46.ch"), WithUserHandle(credential, null)));
        done.RootElement.GetProperty("user").GetProperty("id").GetGuid().Should().Be(userId);
        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(1);
    }
}
