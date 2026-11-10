using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Two-factor sign-in over HTTP, end to end: turned on in Settings with the secret the
/// server hands out, then the password and the code at sign-in.
/// </summary>
/// <remarks>
/// The service tests check each step against fakes. These go through the controllers,
/// the bearer handler and the attempt limiter as the browser does, so a step that issues
/// a session too early, or a code that is never really checked, shows here.
/// </remarks>
public class TwoFactorLoginTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task OnceTurnedOnSigningInTakesTheCodeAndOnlyTheRightCodeGivesASession()
    {
        using var factory = new CustomWebApplicationFactory();
        const string email = "twostep@club.ch";
        var (settings, _) = await SignInAsync(factory, email);
        var secret = await TurnOnAsync(settings);

        // The password alone gives no session: only a token for the next step.
        var browser = factory.CreateClient();
        using var login = await JsonOf(await browser.PostAsync("/api/auth/login", Body(new { email, password = Password })));
        login.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
        login.RootElement.GetProperty("token").ValueKind.Should().Be(JsonValueKind.Null);
        login.RootElement.GetProperty("user").ValueKind.Should().Be(JsonValueKind.Null);
        var step = login.RootElement.GetProperty("twoFactorToken").GetString();
        step.Should().NotBeNullOrEmpty();

        // A wrong code gives nothing.
        var wrong = await browser.PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = OtherThan(CodeNow(secret)) }));
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await wrong.Content.ReadAsStringAsync());
        (await wrong.Content.ReadAsStringAsync()).Should().NotContain("\"token\"");

        // The right one gives a session that works.
        using var done = await JsonOf(await browser.PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = CodeNow(secret) })));
        done.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
        var session = factory.CreateClient();
        session.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", done.RootElement.GetProperty("token").GetString());
        using var me = await JsonOf(await session.GetAsync("/api/auth/me"));
        me.RootElement.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task FiveWrongCodesLockTheCodeStepEvenForTheRightCode()
    {
        // Six digits are a million guesses. Without a limit, the token from one correct
        // password could be tried against every code inside its five minutes.
        using var factory = new CustomWebApplicationFactory();
        const string email = "guessed@club.ch";
        var (settings, _) = await SignInAsync(factory, email);
        var secret = await TurnOnAsync(settings);

        var browser = factory.CreateClient();
        using var login = await JsonOf(await browser.PostAsync("/api/auth/login", Body(new { email, password = Password })));
        var step = login.RootElement.GetProperty("twoFactorToken").GetString();
        var wrongCode = OtherThan(CodeNow(secret));
        for (var attempt = 0; attempt < 5; attempt++)
            (await browser.PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = wrongCode })))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var locked = await browser.PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = CodeNow(secret) }));
        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, await locked.Content.ReadAsStringAsync());
        (await locked.Content.ReadAsStringAsync()).Should().NotContain("\"token\"");
    }

    [Fact]
    public async Task TurnedOffWithThePasswordAndACodeSigningInNeedsNoCodeAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        const string email = "offagain@club.ch";
        var (settings, _) = await SignInAsync(factory, email);
        var secret = await TurnOnAsync(settings);

        var off = await settings.PostAsync("/api/auth/2fa/disable", Body(new { password = Password, code = CodeNow(secret) }));
        off.StatusCode.Should().Be(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());

        using var login = await JsonOf(await factory.CreateClient().PostAsync("/api/auth/login", Body(new { email, password = Password })));
        login.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeFalse();
        login.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>Turns 2FA on as Settings does, and returns the secret an authenticator app would hold.</summary>
    private static async Task<string> TurnOnAsync(HttpClient client)
    {
        string secret;
        using (var setup = await JsonOf(await client.PostAsync("/api/auth/2fa/setup", null)))
        {
            setup.RootElement.GetProperty("enabled").GetBoolean().Should().BeFalse();
            secret = setup.RootElement.GetProperty("secret").GetString()!;
        }
        secret.Should().NotBeNullOrEmpty();

        using var enabled = await JsonOf(await client.PostAsync("/api/auth/2fa/enable", Body(new { password = Password, code = CodeNow(secret) })));
        // Turning it on ends every earlier session; carry on with the token it returns.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", enabled.RootElement.GetProperty("token").GetString());
        return secret;
    }

    private static async Task<JsonDocument> JsonOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"{response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    // A six-digit code that is not this one. One from a neighbouring 30-second window is
    // accepted too, so a different value is wrong but for a one-in-half-a-million chance.
    private static string OtherThan(string code) => ((int.Parse(code) + 500_000) % 1_000_000).ToString("000000");

    // The code an authenticator app shows now for a Base32 secret (RFC 6238: SHA-1, 30 s, 6 digits).
    private static string CodeNow(string base32Secret)
    {
        var key = Base32(base32Secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(message);
        var hash = System.Security.Cryptography.HMACSHA1.HashData(key, message);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("000000");
    }

    private static byte[] Base32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits < 8) continue;
            bytes.Add((byte)(buffer >> (bits - 8)));
            bits -= 8;
        }
        return bytes.ToArray();
    }
}
