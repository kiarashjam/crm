using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every session says how it began (SignInMethods), and keeps saying it when it is
/// finished with a code or replaced because 2FA was turned on or off.
/// </summary>
/// <remarks>
/// A workspace that requires single sign-on reads this. A session that began with single
/// sign-on and lost the fact on the way would be refused by the very workspace it was for.
/// </remarks>
public class SignInMethodTests
{
    [Fact]
    public async Task APasswordSignInsSessionSaysItBeganWithThePassword()
    {
        using var factory = new CustomWebApplicationFactory();
        await SignInAsync(factory, "pw@club.ch");

        var login = await factory.CreateClient().PostAsync("/api/auth/login", Body(new { email = "pw@club.ch", password = AuthenticatorApp.Password }));
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("token").GetString()!;

        AuthenticatorApp.Claim(token, SignInMethods.ClaimType).Should().Be(SignInMethods.Password);
    }

    [Fact]
    public async Task FinishingWithTheCodeASignInThatBeganWithSingleSignOn_GivesASingleSignOnSession()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "sso-2fa@club.ch");
        var secret = await AuthenticatorApp.TurnOnAsync(client);

        string step;
        using (var scope = factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(userId);
            step = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTwoFactorToken(user!, SignInMethods.Sso);
        }

        var done = await factory.CreateClient().PostAsync("/api/auth/login/2fa", Body(new { twoFactorToken = step, code = AuthenticatorApp.CodeNow(secret) }));
        var body = await done.Content.ReadAsStringAsync();
        done.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        AuthenticatorApp.Claim(doc.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);
    }

    [Fact]
    public async Task TurningTwoFactorOnAndOffInASingleSignOnSession_KeepsItOne()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "sso-session@club.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(userId);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user!, SignInMethods.Sso));
        }

        var secret = await AuthenticatorApp.TurnOnAsync(client);
        AuthenticatorApp.Claim(client.DefaultRequestHeaders.Authorization!.Parameter!, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);

        var off = await client.PostAsync("/api/auth/2fa/disable", Body(new { password = AuthenticatorApp.Password, code = AuthenticatorApp.CodeNow(secret) }));
        var body = await off.Content.ReadAsStringAsync();
        off.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        AuthenticatorApp.Claim(doc.RootElement.GetProperty("token").GetString()!, SignInMethods.ClaimType).Should().Be(SignInMethods.Sso);
    }

    [Fact]
    public void ASessionWithoutTheClaimReadsAsAPasswordSignIn()
    {
        // Every token issued before the claim existed began with a password.
        SignInMethods.Of(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())).Should().Be(SignInMethods.Password);
        SignInMethods.Of(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(SignInMethods.ClaimType, "made-up")]))).Should().Be(SignInMethods.Password);
    }
}
