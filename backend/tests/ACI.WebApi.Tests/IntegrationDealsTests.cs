using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// GET /api/integration/deals and /api/integration/deals/{id}: an organization's deals, as JSON, for a
/// system that links its own records to them with the organization's MCP key. BonApp's admin links
/// each restaurant to its deal this way.
/// </summary>
public class IntegrationDealsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IntegrationDealsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Without_a_key_both_routes_are_refused()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/integration/deals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/integration/deals/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_website_form_key_does_not_open_the_deals()
    {
        var webhookKey = NewKey();
        var seeded = await SeedAsync("Forms only", mcpKey: null, webhookKey: webhookKey);

        (await Get("/api/integration/deals", webhookKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Get($"/api/integration/deals/{seeded.DealId}", webhookKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_list_holds_the_key_s_own_organization_by_name_and_search_narrows_it()
    {
        var key = NewKey();
        var mine = await SeedAsync("BonApp", key);
        await SeedAsync("Elsewhere", NewKey());

        var response = await Get("/api/integration/deals", key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using (var all = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            all.RootElement.GetProperty("organizationId").GetGuid().Should().Be(mine.OrganizationId);
            all.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2, "Elsewhere's deals carry the same names");
            all.RootElement.GetProperty("items").EnumerateArray()
                .Select(d => d.GetProperty("name").GetString())
                .Should().Equal("Café du Lac", "Pizzeria Bella");
        }

        using var found = JsonDocument.Parse(await (await Get("/api/integration/deals?search=bella", key)).Content.ReadAsStringAsync());
        found.RootElement.GetProperty("items").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("id").GetGuid().Should().Be(mine.OtherDealId);
    }

    [Fact]
    public async Task Take_is_kept_between_one_and_fifty()
    {
        var key = NewKey();
        await SeedAsync("BonApp", key);

        using var json = JsonDocument.Parse(await (await Get("/api/integration/deals?take=0", key)).Content.ReadAsStringAsync());

        json.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        json.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task A_deal_comes_with_its_stage_company_value_latest_activity_and_open_tasks()
    {
        var key = NewKey();
        var mine = await SeedAsync("BonApp", key);

        var response = await Get($"/api/integration/deals/{mine.DealId}", key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var deal = json.RootElement.GetProperty("deal");
        deal.GetProperty("id").GetGuid().Should().Be(mine.DealId);
        deal.GetProperty("name").GetString().Should().Be("Café du Lac");
        deal.GetProperty("stage").GetString().Should().Be("Trial");
        deal.GetProperty("companyName").GetString().Should().Be("Café du Lac SA");
        deal.GetProperty("value").GetDecimal().Should().Be(1200m);
        deal.GetProperty("currency").GetString().Should().Be("CHF");
        deal.GetProperty("status").GetString().Should().Be("open");

        var activity = json.RootElement.GetProperty("recentActivity").EnumerateArray().Should().ContainSingle().Subject;
        activity.GetProperty("subject").GetString().Should().Be("Demo done");
        activity.TryGetProperty("body", out _).Should().BeFalse("activity bodies stay in Cadence");

        var task = json.RootElement.GetProperty("openTasks").EnumerateArray().Should().ContainSingle("the completed task is not open").Subject;
        task.GetProperty("title").GetString().Should().Be("Send the trial offer");
        task.GetProperty("status").GetString().Should().Be("todo");
        task.TryGetProperty("notes", out _).Should().BeFalse("task notes stay in Cadence");
    }

    [Fact]
    public async Task A_deal_of_another_organization_is_not_found()
    {
        var key = NewKey();
        await SeedAsync("BonApp", key);
        var other = await SeedAsync("Elsewhere", NewKey());

        (await Get($"/api/integration/deals/{other.DealId}", key)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Get($"/api/integration/deals/{Guid.NewGuid()}", key)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_value_with_no_readable_number_is_null_not_zero()
    {
        var key = NewKey();
        var mine = await SeedAsync("BonApp", key, value: "about two thousand");

        using var json = JsonDocument.Parse(await (await Get($"/api/integration/deals/{mine.DealId}", key)).Content.ReadAsStringAsync());

        json.RootElement.GetProperty("deal").GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static string NewKey() => "aci_" + Guid.NewGuid().ToString("N");

    private Task<HttpResponseMessage> Get(string path, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", key);
        return _factory.CreateClient().SendAsync(request);
    }

    private sealed record Seeded(Guid OrganizationId, Guid DealId, Guid OtherDealId);

    private async Task<Seeded> SeedAsync(string name, string? mcpKey, string? webhookKey = null, string value = "1200")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var otherDealId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Name = "Yann", Email = $"{userId:N}@example.com", CreatedAtUtc = now });
        db.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = name,
            OwnerUserId = userId,
            CreatedAtUtc = now,
            McpApiKeyHash = mcpKey == null ? null : ACI.Application.Services.OrganizationService.HashApiKey(mcpKey),
            McpApiKeyCreatedAtUtc = mcpKey == null ? null : now,
            WebhookApiKeyHash = webhookKey == null ? null : ACI.Application.Services.OrganizationService.HashApiKey(webhookKey),
            WebhookApiKeyCreatedAtUtc = webhookKey == null ? null : now,
        });
        db.Companies.Add(new Company { Id = companyId, UserId = userId, OrganizationId = orgId, Name = "Café du Lac SA", CreatedAtUtc = now });
        db.Deals.Add(new Deal
        {
            Id = dealId, UserId = userId, OrganizationId = orgId, Name = "Café du Lac", Value = value, Currency = "CHF",
            Stage = "Trial", CompanyId = companyId, CreatedAtUtc = now,
        });
        db.Deals.Add(new Deal
        {
            Id = otherDealId, UserId = userId, OrganizationId = orgId, Name = "Pizzeria Bella", Value = "900", Currency = "CHF",
            Stage = "Demo booked", CreatedAtUtc = now,
        });
        db.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, DealId = dealId, Type = "meeting",
            Subject = "Demo done", Body = "private notes", CreatedAtUtc = now,
        });
        db.TaskItems.Add(new TaskItem
        {
            Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, DealId = dealId, Title = "Send the trial offer",
            Notes = "internal", Status = ACI.Domain.Enums.TaskStatus.Todo, Priority = ACI.Domain.Enums.TaskPriority.High,
            DueDateUtc = now.AddDays(2), CreatedAtUtc = now,
        });
        db.TaskItems.Add(new TaskItem
        {
            Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, DealId = dealId, Title = "Book the demo",
            Status = ACI.Domain.Enums.TaskStatus.Completed, Completed = true, Priority = ACI.Domain.Enums.TaskPriority.Medium,
            CreatedAtUtc = now.AddDays(-5), CompletedAtUtc = now.AddDays(-3),
        });
        await db.SaveChangesAsync();
        return new Seeded(orgId, dealId, otherDealId);
    }
}
