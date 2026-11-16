using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// An authenticator app for tests: turns two-factor sign-in on as Settings does, and shows
/// the code of the moment (RFC 6238: SHA-1, 30 seconds, 6 digits).
/// </summary>
public static class AuthenticatorApp
{
    public const string Password = "correct horse battery staple";

    /// <summary>Turns 2FA on for the client's account and returns the secret the app holds.</summary>
    /// <remarks>Turning it on ends every earlier session; the client carries on with the new token.</remarks>
    public static async Task<string> TurnOnAsync(HttpClient client)
    {
        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupBody = await setup.Content.ReadAsStringAsync();
        setup.IsSuccessStatusCode.Should().BeTrue(setupBody);
        string secret;
        using (var doc = JsonDocument.Parse(setupBody))
            secret = doc.RootElement.GetProperty("secret").GetString()!;

        var enabled = await client.PostAsync("/api/auth/2fa/enable", Body(new { password = Password, code = CodeNow(secret) }));
        var enabledBody = await enabled.Content.ReadAsStringAsync();
        enabled.IsSuccessStatusCode.Should().BeTrue(enabledBody);
        using (var doc = JsonDocument.Parse(enabledBody))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("token").GetString());
        return secret;
    }

    public static string CodeNow(string base32Secret)
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

    /// <summary>The claim <paramref name="type"/> of a session token, read without checking it.</summary>
    public static string? Claim(string token, string type) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == type)?.Value;

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
