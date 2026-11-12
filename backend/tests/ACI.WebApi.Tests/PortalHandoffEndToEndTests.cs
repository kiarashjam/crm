using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>Runs only against a real portal: CADENCE_E2E_PORTAL and CADENCE_E2E_PORTAL_SECRET.</summary>
public sealed class PortalFactAttribute : FactAttribute
{
    public PortalFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PortalHandoffEndToEndTests.Portal) || string.IsNullOrWhiteSpace(PortalHandoffEndToEndTests.Secret))
            Skip = "Needs a running portal: set CADENCE_E2E_PORTAL to its handoff address (e.g. Pavillon 46's "
                + "http://localhost:5080/api/integrations/cadence/onboarding, started in Development) and "
                + "CADENCE_E2E_PORTAL_SECRET to its CADENCE_ONBOARDING_SECRET.";
    }
}

/// <summary>
/// A deal handed to a real portal and the portal's reports coming back: the whole round
/// trip over HTTP, with nothing faked but the address the outbox dials.
/// </summary>
/// <remarks>
/// The CRM only posts to https addresses on the internet, which a portal on this machine
/// is not. So the workspace saves a stand-in address that passes that check, and the
/// transport dials the local portal in its place; the body, the headers and the
/// signature are the ones production sends. The portal reports to the callback address
/// in the handoff, which here is a small relay into the test host.
/// </remarks>
public class PortalHandoffEndToEndTests
{
    public static string? Portal => Environment.GetEnvironmentVariable("CADENCE_E2E_PORTAL");
    public static string? Secret => Environment.GetEnvironmentVariable("CADENCE_E2E_PORTAL_SECRET");

    private const string StandIn = "https://portal.e2e.example.com/api/integrations/cadence/onboarding";

    /// <summary>The outbox's transport, dialling the local portal for the stand-in address.</summary>
    private sealed class LocalPortal : IWebhookTransport
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public async Task<WebhookPostResult> PostAsync(Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            var target = url.ToString() == StandIn ? new Uri(Portal!) : url;
            using var request = new HttpRequestMessage(HttpMethod.Post, target) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
            try
            {
                using var answer = await Http.SendAsync(request, ct);
                var text = await answer.Content.ReadAsStringAsync(ct);
                return answer.IsSuccessStatusCode
                    ? new WebhookPostResult((int)answer.StatusCode, null, text)
                    : new WebhookPostResult((int)answer.StatusCode, text);
            }
            catch (HttpRequestException ex)
            {
                return new WebhookPostResult(null, ex.Message);
            }
        }
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public required string CallbackBase { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:ApiBaseUrl", CallbackBase);
            builder.ConfigureTestServices(services => services.AddScoped<IWebhookTransport>(_ => new LocalPortal()));
        }
    }

    [PortalFact]
    public async Task ADealIsHandedToTheRealPortal_AndItsReportsComeBack()
    {
        // A real listener the portal can reach, passing its reports into the test host,
        // which is made after it because it needs the relay's address.
        HttpClient? hostClient = null;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var relay = builder.Build();
        relay.MapPost("/api/public/onboarding/{id}/events", async (HttpContext ctx, string id) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            using var forward = new HttpRequestMessage(HttpMethod.Post, $"/api/public/onboarding/{id}/events")
            {
                Content = new StringContent(await reader.ReadToEndAsync(), Encoding.UTF8, "application/json"),
            };
            forward.Headers.TryAddWithoutValidation("Authorization", ctx.Request.Headers.Authorization.ToString());
            using var answer = await hostClient!.SendAsync(forward);
            ctx.Response.StatusCode = (int)answer.StatusCode;
            await ctx.Response.WriteAsync(await answer.Content.ReadAsStringAsync());
        });
        await relay.StartAsync();

        await using var app = new App { CallbackBase = relay.Urls.First() };
        hostClient = app.CreateClient();

        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = StandIn, defaultLanguage = "fr" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // The portal's secret, as an admin would paste the CRM's into the portal.
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            var settings = await db.OnboardingSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == org);
            settings.WebhookSecret = Secret;
            await db.SaveChangesAsync();
        }

        var ping = await JsonDocument.ParseAsync(await (await owner.PostAsync("/api/onboarding/settings/test", null)).Content.ReadAsStreamAsync());
        ping.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue(ping.RootElement.ToString());
        // When the portal says who it sends from (CADENCE_E2E_PORTAL_FROM, its FROM_EMAIL), the test shows it.
        if (Environment.GetEnvironmentVariable("CADENCE_E2E_PORTAL_FROM") is { Length: > 0 } portalFrom)
            ping.RootElement.GetProperty("fromEmail").GetString().Should().Be(portalFrom);

        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var contact = JsonDocument.Parse(await (await owner.PostAsync("/api/contacts", Body(new { name = "Camille Laurent", email }))).Content.ReadAsStringAsync()).RootElement;
        var deal = JsonDocument.Parse(await (await owner.PostAsync("/api/deals", Body(new { name = "Gold membership", value = "4800", contactId = contact.GetProperty("id").GetGuid() }))).Content.ReadAsStringAsync()).RootElement;
        var dealId = deal.GetProperty("id").GetGuid();

        var started = await owner.PostAsync($"/api/deals/{dealId}/onboarding", Body(new
        {
            welcomeMessage = "Nous sommes ravis de vous accueillir.\nÀ très vite au Pavillon.",
            startsOnUtc = "2027-01-15",
            language = "fr",
            emailSubject = "Bienvenue, {{firstName}}",
            emailHtml = "<p>Bonjour {{firstName}},</p><p>{{welcomeMessage}}</p><p>Début : {{startDate}}</p><a href=\"{{setPasswordUrl}}\">Choisir mon mot de passe</a><script>alert(1)</script>",
        }));
        started.StatusCode.Should().Be(HttpStatusCode.Created, await started.Content.ReadAsStringAsync());

        await app.DispatchOutboxAsync();
        (await app.Services.Read(db => db.OutboundMessages.Single(m => m.DealId == dealId).Status)).Should().Be("sent",
            "the portal took the signed handoff");

        // The portal reports in the background: the account, then the email.
        JsonElement handoff = default;
        for (var i = 0; i < 60; i++)
        {
            handoff = JsonDocument.Parse(await (await owner.GetAsync($"/api/deals/{dealId}/onboarding")).Content.ReadAsStringAsync()).RootElement[0];
            var keys = handoff.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();
            if (keys.Contains("email_sent") || keys.Contains("email_failed")) break;
            await Task.Delay(500);
        }
        var steps = handoff.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();
        steps.Should().ContainInOrder("queued", "delivered", "account_created");
        steps.Should().Contain(k => k == "email_sent" || k == "email_failed", "the portal says how the email went, sent or not");
        handoff.GetProperty("externalAccountId").GetString().Should().NotBeNullOrEmpty();

        // Sent again: the portal finds the account it opened, and reports that.
        (await owner.PostAsync($"/api/onboarding/{handoff.GetProperty("id").GetGuid()}/resend", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await app.DispatchOutboxAsync();
        for (var i = 0; i < 60; i++)
        {
            handoff = JsonDocument.Parse(await (await owner.GetAsync($"/api/deals/{dealId}/onboarding")).Content.ReadAsStringAsync()).RootElement[0];
            if (handoff.GetProperty("steps").EnumerateArray().Any(s => s.GetProperty("key").GetString() == "account_existing")) break;
            await Task.Delay(500);
        }
        handoff.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("key").GetString())
            .Should().ContainInOrder("resent", "delivered", "account_existing");

        await relay.StopAsync();
    }
}
