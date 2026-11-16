using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>Runs only when CADENCE_RECORD_PASSKEY_FIXTURES is set: it rewrites the recorded ceremonies.</summary>
public sealed class RecordingFactAttribute : FactAttribute
{
    public RecordingFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CADENCE_RECORD_PASSKEY_FIXTURES")))
            Skip = "Records the passkey fixtures again: set CADENCE_RECORD_PASSKEY_FIXTURES=1.";
    }
}

/// <summary>
/// Recorded WebAuthn ceremonies (Fixtures/Passkeys/ceremonies.json), replayed byte for byte: a
/// registration and the sign-ins after it, as a browser posted them, each with the challenge the
/// server handed out and what the server must answer.
/// </summary>
/// <remarks>
/// The answers were made once by SoftwareAuthenticator with the key the file keeps, and are fixed
/// from then on, so a change to how the server reads or checks them (the library, its settings,
/// the counter rule) shows here as a failure against bytes nobody regenerated.
/// </remarks>
public class PasskeyRecordedCeremonyTests
{
    private const string FixtureName = "Passkeys.ceremonies.json";

    private static JsonDocument Fixture()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FixtureName)
            ?? throw new InvalidOperationException($"The embedded fixture {FixtureName} is missing.");
        return JsonDocument.Parse(stream);
    }

    private static byte[] Bytes(JsonElement element) => SoftwareAuthenticator.FromB64(element.GetString()!);

    [Fact]
    public async Task TheRecordedRegistrationAndSignInsAreAnsweredAsRecorded()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        root.GetProperty("rpId").GetString().Should().Be(PasskeyApp.RpId);
        root.GetProperty("origin").GetString().Should().Be(PasskeyApp.Origin);

        await using var app = new PasskeyApp();
        // The person the recording was made for: the user handle in the answers is their id.
        var user = new User
        {
            Id = root.GetProperty("userId").GetGuid(),
            Name = "Anna Keller",
            Email = "anna@pavillon46.ch",
            CreatedAtUtc = DateTime.UtcNow,
            EmailVerifiedAtUtc = DateTime.UtcNow,
        };
        var session = app.CreateClient();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
            // An account without a password, just signed in: it may add a passkey without one.
            session.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user, SignInMethods.Sso));
        }

        var registration = root.GetProperty("registration");
        app.Challenges.Queue.Enqueue(Bytes(registration.GetProperty("challenge")));
        using (var options = JsonDocument.Parse(await (await session.PostAsync("/api/auth/passkeys/registration/options", Body(new { }))).Content.ReadAsStringAsync()))
        {
            options.RootElement.GetProperty("options").GetProperty("challenge").GetString().Should().Be(registration.GetProperty("challenge").GetString());
            var added = await session.PostAsync("/api/auth/passkeys/registration", Body(new
            {
                ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
                name = "Recorded key",
                credential = registration.GetProperty("credential"),
            }));
            added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        }
        var stored = await app.Services.Read(db => db.UserPasskeys.Single());
        stored.UserId.Should().Be(user.Id);
        SoftwareAuthenticator.B64(stored.CredentialId).Should().Be(root.GetProperty("credentialId").GetString());

        foreach (var signIn in root.GetProperty("signIns").EnumerateArray())
        {
            app.Challenges.Queue.Enqueue(Bytes(signIn.GetProperty("challenge")));
            var browser = app.CreateClient();
            using var options = JsonDocument.Parse(await (await browser.PostAsync("/api/auth/passkeys/sign-in/options", Body(new { }))).Content.ReadAsStringAsync());
            var answer = await browser.PostAsync("/api/auth/passkeys/sign-in", Body(new
            {
                ceremonyId = options.RootElement.GetProperty("ceremonyId").GetString(),
                credential = signIn.GetProperty("credential"),
            }));
            var body = await answer.Content.ReadAsStringAsync();
            var why = signIn.GetProperty("about").GetString();
            switch (signIn.GetProperty("expect").GetString())
            {
                case "session":
                    answer.StatusCode.Should().Be(HttpStatusCode.OK, $"{why}: {body}");
                    using (var done = JsonDocument.Parse(body))
                        AuthenticatorApp.Claim(done.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Passkey);
                    break;
                case "counter-went-back":
                    answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized, why);
                    body.Should().Contain("Passkeys.CounterWentBack", why);
                    break;
                case "not-recognised":
                    answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized, why);
                    body.Should().Contain("Passkeys.NotRecognised", why);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown expectation in the fixture: {signIn}");
            }
        }

        (await app.Services.Read(db => db.UserPasskeys.Single().SignCount)).Should().Be(2, "the counter the last accepted sign-in reported");
    }

    [RecordingFact]
    public void RecordTheCeremoniesAgain()
    {
        var userId = Guid.NewGuid();
        var key = new SoftwareAuthenticator(PasskeyApp.RpId, PasskeyApp.Origin);
        JsonElement Options(byte[] challenge) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            challenge = SoftwareAuthenticator.B64(challenge),
            user = new { id = SoftwareAuthenticator.B64(userId.ToByteArray()) },
        })).RootElement;

        var registrationChallenge = RandomNumberGenerator.GetBytes(32);
        var registration = key.Register(Options(registrationChallenge));

        JsonObject SignIn(string about, string expect, uint signCount, Func<JsonElement, JsonObject>? answer = null)
        {
            var challenge = RandomNumberGenerator.GetBytes(32);
            var options = Options(challenge);
            return new JsonObject
            {
                ["about"] = about,
                ["expect"] = expect,
                ["challenge"] = SoftwareAuthenticator.B64(challenge),
                ["credential"] = (answer ?? (o => key.Assert(o, signCount)))(options),
            };
        }

        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var file = new JsonObject
        {
            ["about"] = "WebAuthn ceremonies for PasskeyRecordedCeremonyTests, made by SoftwareAuthenticator (ES256, none attestation). Recorded again with CADENCE_RECORD_PASSKEY_FIXTURES=1; the key below is a test key and signs nothing else.",
            ["rpId"] = PasskeyApp.RpId,
            ["origin"] = PasskeyApp.Origin,
            ["userId"] = userId.ToString(),
            ["credentialId"] = key.CredentialIdText,
            ["privateKeyPkcs8"] = Convert.ToBase64String(key.Key.ExportPkcs8PrivateKey()),
            ["registration"] = new JsonObject
            {
                ["challenge"] = SoftwareAuthenticator.B64(registrationChallenge),
                ["credential"] = registration,
            },
            ["signIns"] = new JsonArray(
                SignIn("the first sign-in, counter 1", "session", 1),
                SignIn("the next, counter 2", "session", 2),
                SignIn("a copy of the key presenting counter 2 again", "counter-went-back", 2),
                SignIn("an answer signed by another key", "not-recognised", 3, o => key.Assert(o, 3, signWith: stranger))),
        };

        File.WriteAllText(FixturePath(), file.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            // Readable base64 and quotes: the file is test data, never served to a browser.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n");
    }

    private static string FixturePath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "Passkeys", "ceremonies.json");
}
