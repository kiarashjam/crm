using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Once 2FA is on, nothing hands its secret out again. Settings asks only whether it is on.
/// </summary>
public class TwoFactorSetupTests
{
    [Fact]
    public async Task WithTwoFactorOnSetupAndStatusNeverCarryTheSecret()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "owner@club.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var user = await db.Users.FindAsync(userId);
            user!.TwoFactorSecretProtected = protector.Protect("JBSWY3DPEHPK3PXP");
            user.TwoFactorEnabled = true;
            await db.SaveChangesAsync();
        }

        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
        var setupBody = await setup.Content.ReadAsStringAsync();
        setup.StatusCode.Should().Be(HttpStatusCode.OK, setupBody);
        setupBody.Should().NotContain("JBSWY3DPEHPK3PXP");
        using (var doc = JsonDocument.Parse(setupBody))
        {
            doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("secret").ValueKind.Should().Be(JsonValueKind.Null);
        }

        var status = await client.GetStringAsync("/api/auth/2fa/status");
        status.Should().Be("{\"enabled\":true}");
    }

    [Fact]
    public async Task AMistypedCodeOrPasswordInSettingsLeavesThePersonSignedIn()
    {
        // These answered 401, the status of a failed sign-in, and the browser takes any
        // 401 to mean the session is over: one typo while turning 2FA on or off signed
        // the person out.
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "owner@club.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.TwoFactorSecretProtected = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect("JBSWY3DPEHPK3PXP");
            await db.SaveChangesAsync();
        }

        var enable = await client.PostAsync("/api/auth/2fa/enable", Body(new { password = "correct horse battery staple", code = "000000" }));
        enable.StatusCode.Should().Be(HttpStatusCode.BadRequest, await enable.Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            (await db.Users.FindAsync(userId))!.TwoFactorEnabled = true;
            await db.SaveChangesAsync();
        }

        var disable = await client.PostAsync("/api/auth/2fa/disable", Body(new { password = "not my password", code = "000000" }));
        disable.StatusCode.Should().Be(HttpStatusCode.BadRequest, await disable.Content.ReadAsStringAsync());
        var wrongCode = await client.PostAsync("/api/auth/2fa/disable", Body(new { password = "correct horse battery staple", code = "000000" }));
        wrongCode.StatusCode.Should().Be(HttpStatusCode.BadRequest, await wrongCode.Content.ReadAsStringAsync());

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TurningTwoFactorOnNeedsThePassword()
    {
        // It signs out every other session, so with a code alone someone holding a stolen
        // session could turn it on with their own authenticator and lock the owner out.
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "owner@club.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.TwoFactorSecretProtected = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect("JBSWY3DPEHPK3PXP");
            await db.SaveChangesAsync();
        }
        // JBSWY3DPEHPK3PXP, decoded.
        var code = CodeNow([0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0xDE, 0xAD, 0xBE, 0xEF]);

        (await client.PostAsync("/api/auth/2fa/enable", Body(new { code })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the password is required");
        (await client.PostAsync("/api/auth/2fa/enable", Body(new { password = "a guess", code })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a wrong password is refused");
        (await factory.Services.Read(db => db.Users.Single(u => u.Id == userId).TwoFactorEnabled)).Should().BeFalse();

        var right = await client.PostAsync("/api/auth/2fa/enable", Body(new { password = "correct horse battery staple", code }));
        right.IsSuccessStatusCode.Should().BeTrue(await right.Content.ReadAsStringAsync());
        (await factory.Services.Read(db => db.Users.Single(u => u.Id == userId).TwoFactorEnabled)).Should().BeTrue();
    }

    private static string CodeNow(byte[] secret)
    {
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(message);
        var hash = System.Security.Cryptography.HMACSHA1.HashData(secret, message);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("000000");
    }
}
