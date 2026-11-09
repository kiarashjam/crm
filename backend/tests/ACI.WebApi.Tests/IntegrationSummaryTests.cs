using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// GET /api/integration/summary: one organization's figures, as JSON, for a system linked to it
/// with the organization's MCP key (the Atmosclear BMS links its projects this way).
/// </summary>
public class IntegrationSummaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IntegrationSummaryTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Without_a_key_it_is_refused()
    {
        var response = await _factory.CreateClient().GetAsync("/api/integration/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_key_or_the_website_form_key_is_refused()
    {
        var webhookKey = "aci_" + Guid.NewGuid().ToString("N");
        await SeedAsync("Forms only", mcpKey: null, webhookKey: webhookKey);

        foreach (var key in new[] { webhookKey, "aci_" + Guid.NewGuid().ToString("N") })
        {
            var response = await Get(key);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"key {key[..8]}… must not open the summary");
        }
    }

    [Fact]
    public async Task The_key_reads_its_own_organization_only()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        var otherKey = "aci_" + Guid.NewGuid().ToString("N");
        var mine = await SeedAsync("Northwind", key);
        await SeedAsync("Contoso", otherKey, dealValue: "999999");

        var response = await Get(key);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        root.GetProperty("organizationId").GetGuid().Should().Be(mine.OrganizationId);
        root.GetProperty("organizationName").GetString().Should().Be("Northwind");
        root.GetProperty("leads").GetProperty("total").GetInt32().Should().Be(1);
        root.GetProperty("tasks").GetProperty("total").GetInt32().Should().Be(1);
        root.GetProperty("deals").GetProperty("activeDealsCount").GetInt32().Should().Be(1);
        root.GetProperty("deals").GetProperty("pipelineValue").GetDecimal().Should().Be(25000m, "Contoso's 999,999 is not Northwind's");
        root.GetProperty("pipelineByStage").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("dealCount").GetInt32().Should().Be(1);

        var activity = root.GetProperty("recentActivity").EnumerateArray().Should().ContainSingle().Subject;
        activity.GetProperty("subject").GetString().Should().Be(mine.ActivitySubject);
        activity.TryGetProperty("body", out _).Should().BeFalse("activity bodies stay in Cadence");
    }

    private Task<HttpResponseMessage> Get(string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/integration/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return _factory.CreateClient().SendAsync(request);
    }

    private sealed record Seeded(Guid OrganizationId, string ActivitySubject);

    private async Task<Seeded> SeedAsync(string name, string? mcpKey, string? webhookKey = null, string dealValue = "25000")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var subject = $"Call {Guid.NewGuid():N}"[..14];
        db.Users.Add(new User { Id = userId, Name = "Ada", Email = $"{userId:N}@example.com", CreatedAtUtc = DateTime.UtcNow });
        db.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = name,
            OwnerUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            McpApiKeyHash = mcpKey == null ? null : ACI.Application.Services.OrganizationService.HashApiKey(mcpKey),
            McpApiKeyCreatedAtUtc = mcpKey == null ? null : DateTime.UtcNow,
            WebhookApiKeyHash = webhookKey == null ? null : ACI.Application.Services.OrganizationService.HashApiKey(webhookKey),
            WebhookApiKeyCreatedAtUtc = webhookKey == null ? null : DateTime.UtcNow,
        });
        db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Name = "Lead", Email = $"{Guid.NewGuid():N}@example.com", Status = "New", CreatedAtUtc = DateTime.UtcNow });
        db.Deals.Add(new Deal { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Name = "Roof", Value = dealValue, Currency = "USD", Stage = "Qualified", CreatedAtUtc = DateTime.UtcNow });
        db.TaskItems.Add(new TaskItem { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Title = "Follow up", Status = ACI.Domain.Enums.TaskStatus.Todo, Priority = ACI.Domain.Enums.TaskPriority.Medium, CreatedAtUtc = DateTime.UtcNow });
        db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Type = "call", Subject = subject, Body = "private notes", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return new Seeded(orgId, subject);
    }
}
