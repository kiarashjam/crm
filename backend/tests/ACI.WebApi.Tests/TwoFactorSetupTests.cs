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
}
