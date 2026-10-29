using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A lead created by the CSV import is someone the workspace already knows, not a
/// new inquiry.
/// </summary>
/// <remarks>
/// The import posts one lead per row, and every row used to be treated as a fresh
/// inbound lead: lead.created automations sent their welcome email and started
/// sequences, an outbound webhook went out per row, and the first-response clock
/// started, so hours later the owners got a "first response is late" alert for
/// each person on a list they had known for years.
/// </remarks>
public class LeadImportOriginTests
{
    /// <summary>The app with an outbound webhook publisher that remembers what it was asked to publish.</summary>
    private sealed class WithWebhooks : CustomWebApplicationFactory
    {
        public readonly List<string> Published = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var webhooks = new Mock<IOutboundWebhookPublisher>();
                webhooks.Setup(w => w.PublishAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .Callback((Guid? _, string name, object _, CancellationToken _) => Published.Add(name))
                    .Returns(Task.CompletedTask);
                services.AddScoped(_ => webhooks.Object);
            });
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<HttpClient> WorkspaceWithANewLeadRuleAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        await Json(await client.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "create_task", config = new { title = "Call the new lead", dueInDays = "1" } } },
        })), "add rule");
        return client;
    }

    private static async Task<int> TaskCountAsync(HttpClient client, Guid leadId) =>
        (await Json(await client.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).GetArrayLength();

    [Fact]
    public async Task AnImportedLeadSetsNothingOff()
    {
        await using var factory = new WithWebhooks();
        var client = await WorkspaceWithANewLeadRuleAsync(factory);

        var lead = await Json(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Dupont", email = "jean@example.ch", status = "New", source = "csv_import", origin = "import",
        })), "import a row");
        var leadId = lead.GetProperty("id").GetGuid();

        (await TaskCountAsync(client, leadId)).Should().Be(0, "the new-lead automation is for new inquiries");
        factory.Published.Should().NotContain("lead.created");
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseDueAtUtc))
            .Should().BeNull("no first-response clock starts for someone already known");
    }

    [Fact]
    public async Task AnImportCanAskForTheAutomations()
    {
        await using var factory = new WithWebhooks();
        var client = await WorkspaceWithANewLeadRuleAsync(factory);

        var lead = await Json(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Dupont", email = "jean@example.ch", status = "New", origin = "import", runAutomations = true,
        })), "import a row");

        (await TaskCountAsync(client, lead.GetProperty("id").GetGuid())).Should().Be(1);
        factory.Published.Should().NotContain("lead.created", "the webhook is for new leads only");
    }

    [Fact]
    public async Task ALeadCreatedTheUsualWayStillSetsEverythingOff()
    {
        await using var factory = new WithWebhooks();
        var client = await WorkspaceWithANewLeadRuleAsync(factory);

        var lead = await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create");
        var leadId = lead.GetProperty("id").GetGuid();

        (await TaskCountAsync(client, leadId)).Should().Be(1);
        factory.Published.Should().Contain("lead.created");
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId)))
            .Should().Match<ACI.Domain.Entities.Lead>(l => l.FirstResponseDueAtUtc != null && l.FirstResponseAtUtc == null);
    }

    [Theory]
    [InlineData("Contacted", null)]
    [InlineData("New", "2024-03-01T09:00:00Z")]
    public async Task ALeadThatArrivesAlreadyWorkedHasHadItsFirstResponse(string status, string? lastContactedAt)
    {
        await using var factory = new WithWebhooks();
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));

        var lead = await Json(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Dupont", email = "jean@example.ch", status, lastContactedAt, origin = "import",
        })), "import a row");
        var leadId = lead.GetProperty("id").GetGuid();

        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseAtUtc)).Should().NotBeNull();
    }

    [Fact]
    public async Task OnlyImportIsAnOrigin()
    {
        await using var factory = new WithWebhooks();
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));

        var res = await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New", origin = "webhook" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
