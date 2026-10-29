using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A session token lives a day. Before this, someone who reset their password because
/// another person had it left that person signed in until the token ran out. A reset
/// or a change to 2FA now ends every session issued before it.
/// </summary>
public class SessionRevocationTests
{
    /// <summary>Base32 "JBSWY3DPEHPK3PXP": the bytes of "Hello!" then DE AD BE EF.</summary>
    private const string Secret = "JBSWY3DPEHPK3PXP";
    private static readonly byte[] SecretBytes = [0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0xDE, 0xAD, 0xBE, 0xEF];

    /// <summary>The authenticator app's code for now (RFC 6238: SHA-1, 30 seconds, 6 digits).</summary>
    private static string CodeNow()
    {
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(message);
        var hash = HMACSHA1.HashData(SecretBytes, message);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("000000");
    }

    private static async Task<string> TokenFrom(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private static HttpClient As(CustomWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task APasswordResetSignsOutEverySessionFromBeforeIt()
    {
        await using var mail = new Mailbox();
        var (stolen, _) = await SignInAsync(mail, "kia@club.ch");
        (await stolen.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = mail.CreateClient();
        await anonymous.PostAsync("/api/auth/forgot-password", Body(new { email = "kia@club.ch" }));
        (await anonymous.PostAsync("/api/auth/reset-password",
            Body(new { token = mail.Token("reset", "kia@club.ch"), password = "a brand new passphrase" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await stolen.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stolen.GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var fresh = await TokenFrom(await anonymous.PostAsync("/api/auth/login",
            Body(new { email = "kia@club.ch", password = "a brand new passphrase" })));
        (await As(mail, fresh).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AStampCachedBeforeAChangeDoesNotRefuseTheTokenIssuedAfterIt()
    {
        // The change happens on another instance of the API (or a read here raced it), so
        // this instance still holds the stamp from before. The person who just turned 2FA
        // on, or reset their password, was refused with 401 and signed out by the browser.
        await using var factory = new CustomWebApplicationFactory();
        var (before, userId) = await SignInAsync(factory, "kia@club.ch");
        (await before.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK, "this caches the stamp");

        string after;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.SecurityStamp = "changed-on-another-instance";
            await db.SaveChangesAsync();
            after = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user);
        }

        (await As(factory, after).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await As(factory, after).GetAsync("/api/contacts")).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TurningTwoFactorOnOrOffSignsOutOtherSessions_AndKeepsThisOne()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (here, userId) = await SignInAsync(factory, "kia@club.ch");
        var elsewhere = As(factory, here.DefaultRequestHeaders.Authorization!.Parameter!);
        (await elsewhere.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            // Setup done, not yet confirmed with a code.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.TwoFactorSecretProtected = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect(Secret);
            await db.SaveChangesAsync();
        }

        var afterEnable = await TokenFrom(await here.PostAsync("/api/auth/2fa/enable", Body(new { password = "correct horse battery staple", code = CodeNow() })));

        (await elsewhere.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var current = As(factory, afterEnable);
        (await current.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK, "the session that turned it on carries on");

        var afterDisable = await TokenFrom(await current.PostAsync("/api/auth/2fa/disable",
            Body(new { password = "correct horse battery staple", code = CodeNow() })));

        (await current.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await As(factory, afterDisable).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
