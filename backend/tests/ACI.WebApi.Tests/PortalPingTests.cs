using System.Net;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The daily portal ping: every workspace whose portal is set up is sent the same signed test
/// as "Test the connection", and its answer is kept for the System health page.
/// </summary>
public class PortalPingTests
{
    /// <summary>A portal at every address: Bernina's answers 503, everyone else's takes it.</summary>
    private sealed class Portals : IWebhookTransport
    {
        public readonly List<(Uri Url, IReadOnlyDictionary<string, string> Headers)> Posts = new();

        public Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            lock (Posts) Posts.Add((url, headers));
            return Task.FromResult(url.Host.StartsWith("bernina", StringComparison.Ordinal)
                ? new WebhookPostResult(503, "The receiver answered 503", null)
                : new WebhookPostResult(200, null, "{}"));
        }
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly Portals Hook = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IWebhookTransport>(_ => Hook));
        }
    }

    private static OnboardingSettings Portal(Guid org, string? url, string? secret = "s3cret") => new()
    {
        Id = Guid.NewGuid(), OrganizationId = org, WebhookUrl = url, WebhookSecret = secret, UpdatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task Each_portal_that_is_set_up_is_pinged_and_its_answer_kept()
    {
        await using var app = new App();
        var (alpine, alpineOwner) = await SignInAsync(app, "owner@alpine.example");
        var alpineOrg = await CreateAsync(app, alpineOwner, "Alpine");
        var (bernina, berninaOwner) = await SignInAsync(app, "owner@bernina.example");
        var berninaOrg = await CreateAsync(app, berninaOwner, "Bernina");
        var (_, soloOwner) = await SignInAsync(app, "owner@solo.example");
        var soloOrg = await CreateAsync(app, soloOwner, "Solo");
        await HealthWorld.SaveAsync(app,
            Portal(alpineOrg, "https://alpine-portal.example.com/hook"),
            Portal(berninaOrg, "https://bernina-portal.example.com/hook"),
            // An address with no secret yet: not set up, so not pinged.
            Portal(soloOrg, "https://solo-portal.example.com/hook", secret: null));

        var pings = app.Services.GetRequiredService<IPortalPings>();
        pings.Record(soloOrg, new PortalPingResult(false, DateTime.UtcNow.AddDays(-1), 500, "from before it was taken down"));
        var job = new PortalPingBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(), pings,
            NullLogger<PortalPingBackgroundService>.Instance);

        (await job.PingAllAsync(default)).Should().Be(1, "Bernina's portal did not take it");

        app.Hook.Posts.Select(p => p.Url.Host).Should().BeEquivalentTo("alpine-portal.example.com", "bernina-portal.example.com");
        app.Hook.Posts.Should().OnlyContain(p => p.Headers["X-Cadence-Event"] == "onboarding.ping");
        var ok = pings.For(alpineOrg)!;
        ok.Ok.Should().BeTrue();
        ok.StatusCode.Should().Be(200);
        ok.Error.Should().BeNull();
        ok.AtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        var failed = pings.For(berninaOrg)!;
        failed.Ok.Should().BeFalse();
        failed.StatusCode.Should().Be(503);
        failed.Error.Should().Be("The receiver answered 503");
        pings.For(soloOrg).Should().BeNull("a portal no longer set up has no answer to show");

        // The page shows each workspace its own.
        var mine = await HealthWorld.Ok(await bernina.In(berninaOrg).GetAsync("/api/system-health"));
        mine.GetProperty("portal").GetProperty("ok").GetBoolean().Should().BeFalse();
        mine.GetProperty("portal").GetProperty("error").GetString().Should().Be("The receiver answered 503");
        mine.GetProperty("overall").GetProperty("status").GetString().Should().Be("failing");
        var theirs = await HealthWorld.Ok(await alpine.In(alpineOrg).GetAsync("/api/system-health"));
        theirs.GetProperty("portal").GetProperty("ok").GetBoolean().Should().BeTrue();
        theirs.GetProperty("overall").GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Testing_the_connection_by_hand_is_kept_as_the_last_ping_too()
    {
        await using var app = new App();
        var (bernina, ownerId) = await SignInAsync(app, "owner@bernina.example");
        var org = await CreateAsync(app, ownerId, "Bernina");
        await HealthWorld.SaveAsync(app, Portal(org, "https://bernina-portal.example.com/hook"));

        var answer = await bernina.In(org).PostAsync("/api/onboarding/settings/test", null);
        answer.StatusCode.Should().Be(HttpStatusCode.OK);

        var last = app.Services.GetRequiredService<IPortalPings>().For(org)!;
        last.Ok.Should().BeFalse();
        last.StatusCode.Should().Be(503);
    }
}
