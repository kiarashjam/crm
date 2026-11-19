using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Monitoring;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Settings › System health's live status (GET /api/system-health/sites): CadenceE's own checks,
/// and the member portal's /health read from the portal's own address, kept for 30 seconds, and
/// "Unreachable" when it does not answer; for those who manage the workspace's settings only.
/// </summary>
public class SystemHealthSitesTests
{
    private const string Handoffs = "https://portal.example.com/api/integrations/cadence/onboarding";

    /// <summary>The member portal, answering as it is told to.</summary>
    private sealed class Portal : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => Json(HttpStatusCode.OK, """{"status":"Healthy","checks":[]}""");
        public readonly List<Uri> Asked = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Asked) Asked.Add(request.RequestUri!);
            return Task.FromResult(Answer(request));
        }

        public static HttpResponseMessage Json(HttpStatusCode code, string body) =>
            new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly Portal Portal = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(SiteStatus.ClientName).ConfigurePrimaryHttpMessageHandler(() => Portal);
                // The SQL check would wait for a server the in-memory tests do not have.
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    foreach (var sql in options.Registrations.Where(r => r.Name == "database").ToList()) options.Registrations.Remove(sql);
                });
            });
        }
    }

    private static async Task<JsonElement> Sites(HttpClient client)
    {
        var res = await client.GetAsync("/api/system-health/sites");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        res.Headers.CacheControl?.NoStore.Should().BeTrue();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid Org)> WorkspaceAsync(App app, bool portal)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        if (portal)
            (await owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = Handoffs }))).StatusCode.Should().Be(HttpStatusCode.OK);
        return (owner, org);
    }

    private static JsonElement Site(JsonElement sites, string key) =>
        sites.GetProperty("sites").EnumerateArray().Single(s => s.GetProperty("key").GetString() == key);

    [Fact]
    public async Task CadenceEsOwnChecksAreThere_AndNoPortalWithoutOne()
    {
        await using var app = new App();
        var (owner, _) = await WorkspaceAsync(app, portal: false);

        var sites = await Sites(owner);

        sites.GetProperty("sites").GetArrayLength().Should().Be(1, "no portal is set up");
        var cadence = Site(sites, "cadence");
        cadence.GetProperty("name").GetString().Should().Be("CadenceE");
        cadence.GetProperty("url").GetString().Should().EndWith("/health");
        cadence.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        cadence.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Contain(new[] { "startup", "jobs", "outbox" });
        app.Portal.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task ThePortalsHealthIsReadFromItsOwnAddress_AndKeptForHalfAMinute()
    {
        await using var app = new App();
        app.Portal.Answer = _ => Portal.Json(HttpStatusCode.OK, """
            {"status":"Degraded","totalDurationMs":12,"checks":[
              {"name":"storage","status":"Healthy","description":"Members read from Azure Table storage.","durationMs":9,"data":{}},
              {"name":"configuration","status":"Degraded","description":"Missing: LEADS_WEBHOOK_API_KEY.","durationMs":0,"data":{"missing":["LEADS_WEBHOOK_API_KEY"]}}]}
            """);
        var (owner, _) = await WorkspaceAsync(app, portal: true);

        var portal = Site(await Sites(owner), "portal");

        portal.GetProperty("name").GetString().Should().Be("portal.example.com");
        portal.GetProperty("url").GetString().Should().Be("https://portal.example.com/health");
        portal.GetProperty("status").GetString().Should().Be("Degraded");
        portal.GetProperty("responseMs").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        var checks = portal.GetProperty("checks").EnumerateArray().ToList();
        checks.Select(c => (c.GetProperty("name").GetString(), c.GetProperty("status").GetString()))
            .Should().Equal(("storage", "Healthy"), ("configuration", "Degraded"));
        checks[1].GetProperty("description").GetString().Should().Be("Missing: LEADS_WEBHOOK_API_KEY.");
        app.Portal.Asked.Should().ContainSingle().Which.Should().Be(new Uri("https://portal.example.com/health"));

        await Sites(owner);
        app.Portal.Asked.Should().HaveCount(1, "its answer is kept for 30 seconds");
    }

    [Fact]
    public async Task APortalThatDoesNotAnswerIsUnreachable()
    {
        await using var app = new App();
        app.Portal.Answer = _ => throw new HttpRequestException("No such host is known.");
        var (owner, _) = await WorkspaceAsync(app, portal: true);

        var portal = Site(await Sites(owner), "portal");

        portal.GetProperty("status").GetString().Should().Be("Unreachable");
        portal.GetProperty("error").GetString().Should().Be("It could not be reached.");
        portal.GetProperty("responseMs").ValueKind.Should().Be(JsonValueKind.Null);
        portal.GetProperty("checks").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task APortalAnsweringAnErrorPageIsUnhealthy_WithItsCode()
    {
        await using var app = new App();
        app.Portal.Answer = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("<html>Service Unavailable</html>", Encoding.UTF8, "text/html") };
        var (owner, _) = await WorkspaceAsync(app, portal: true);

        var portal = Site(await Sites(owner), "portal");

        portal.GetProperty("status").GetString().Should().Be("Unhealthy");
        portal.GetProperty("error").GetString().Should().Be("It answered 503.");
    }

    [Fact]
    public async Task OnlyThoseWhoManageTheWorkspacesSettingsSeeIt()
    {
        await using var app = new App();
        var (_, org) = await WorkspaceAsync(app, portal: true);
        var (seller, sellerId) = await SignInAsync(app, "seller@pavillon46.ch");
        await AddMemberAsync(app, org, sellerId, OrgMemberRole.Member);

        (await seller.In(org).GetAsync("/api/system-health/sites")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await app.CreateClient().GetAsync("/api/system-health/sites")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        app.Portal.Asked.Should().BeEmpty("nobody who may not see it made the portal be asked");
    }
}
