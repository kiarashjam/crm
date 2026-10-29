using System.Net;
using System.Net.Http.Headers;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The token handed out after the password, before the code, is not a session.
/// </summary>
/// <remarks>
/// It is signed with the same key as a session token, so the bearer handler used to
/// accept it on every endpoint: with only the password, someone could skip the code
/// and use the CRM as the account owner for five minutes, and get a full session by
/// calling an endpoint that issues one.
/// </remarks>
public class TwoFactorStepTokenTests
{
    [Fact]
    public async Task TheStepTokenIsRefusedAsABearerToken()
    {
        using var factory = new CustomWebApplicationFactory();
        var (signedIn, userId) = await SignInAsync(factory, "twofactor@example.ch");
        (await signedIn.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        string stepToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            stepToken = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateTwoFactorToken(user!);
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stepToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
