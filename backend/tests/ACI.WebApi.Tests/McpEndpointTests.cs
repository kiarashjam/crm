using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

public class McpEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/mcp", Json("""{"jsonrpc":"2.0","id":1,"method":"initialize"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Initialize_WithOrganizationKey_ReturnsServerInfo()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        await SeedOrganizationAsync(key);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await client.PostAsync("/mcp", Json("""
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}
            """));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("cadence");
        document.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString().Should().Be("2025-03-26");
    }

    [Fact]
    public async Task ToolsList_IncludesSearch()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        await SeedOrganizationAsync(key);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var response = await client.PostAsync("/mcp", Json("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("search_crm");
        body.Should().Contain("organization_snapshot");
    }

    [Fact]
    public async Task InitializedNotification_ReturnsAccepted()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        await SeedOrganizationAsync(key);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await client.PostAsync("/mcp", Json("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Search_OnEmptyOrganization_ReturnsNoMatches()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        var orgName = "Org " + Guid.NewGuid().ToString("N")[..8];
        await SeedOrganizationAsync(key, orgName);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await client.PostAsync("/mcp", Json("""
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"search_crm","arguments":{"query":"nobody-home"}}}
            """));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("No matches");
        body.Should().Contain(orgName);
    }

    [Fact]
    public async Task MalformedJson_ReturnsParseError()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        await SeedOrganizationAsync(key);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await client.PostAsync("/mcp", Json("{"));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("-32700");
    }

    [Fact]
    public async Task Claude_WithOrganizationKey_ReadsOnlyThatOrganization()
    {
        var keyA = "aci_" + Guid.NewGuid().ToString("N");
        var keyB = "aci_" + Guid.NewGuid().ToString("N");
        var leadA = "Shared-" + Guid.NewGuid().ToString("N")[..8];
        var leadB = "Shared-" + Guid.NewGuid().ToString("N")[..8];
        await SeedOrganizationAsync(keyA, "Org A", leadA);
        await SeedOrganizationAsync(keyB, "Org B", leadB);

        var client = _factory.CreateClient();
        var response = await client.SendAsync(ClaudeRequest(keyA, """
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"list_leads","arguments":{"query":"Shared"}}}
            """));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("MCP-Protocol-Version").Should().BeTrue();
        body.Should().Contain(leadA);
        body.Should().NotContain(leadB);
        body.Should().Contain("\"isError\":false");
    }

    [Fact]
    public async Task Claude_Preflight_AllowsClaudeOrigin()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        request.Headers.TryAddWithoutValidation("Origin", "https://claude.ai");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type,mcp-protocol-version");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("https://claude.ai");
    }

    [Fact]
    public async Task Claude_HandshakeThenReadsOrganizationRecords()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        var otherKey = "aci_" + Guid.NewGuid().ToString("N");
        var records = await SeedFullOrganizationAsync(key, "Northwind");
        await SeedFullOrganizationAsync(otherKey, "Contoso");

        var client = _factory.CreateClient();

        var init = await client.SendAsync(ClaudeRequest(key, """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"claude","version":"1"}}}
            """));
        var initBody = await init.Content.ReadAsStringAsync();
        init.StatusCode.Should().Be(HttpStatusCode.OK);
        initBody.Should().Contain("\"name\":\"cadence\"");
        initBody.Should().Contain("\"protocolVersion\":\"2025-03-26\"");

        var tools = await client.SendAsync(ClaudeRequest(key, """
            {"jsonrpc":"2.0","id":2,"method":"tools/list"}
            """));
        var toolsBody = await tools.Content.ReadAsStringAsync();
        tools.StatusCode.Should().Be(HttpStatusCode.OK);
        toolsBody.Should().Contain("list_leads");
        toolsBody.Should().Contain("list_contacts");
        toolsBody.Should().Contain("list_deals");
        toolsBody.Should().Contain("list_tasks");
        toolsBody.Should().Contain("get_lead");
        toolsBody.Should().Contain("search_crm");
        toolsBody.Should().Contain("organization_snapshot");

        var initialized = await client.SendAsync(ClaudeRequest(key, """
            {"jsonrpc":"2.0","method":"notifications/initialized"}
            """));
        initialized.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var leads = await CallText(client, key, "list_leads", "{}");
        leads.Should().Contain(records.LeadName);
        leads.Should().NotContain("Contoso");

        var lead = await CallText(client, key, "get_lead", $$"""{"id":"{{records.LeadId}}"}""");
        lead.Should().Contain(records.LeadName);
        lead.Should().Contain(records.LeadEmail);

        var contacts = await CallText(client, key, "list_contacts", "{}");
        contacts.Should().Contain(records.ContactName);

        var deals = await CallText(client, key, "list_deals", "{}");
        deals.Should().Contain(records.DealName);

        var tasks = await CallText(client, key, "list_tasks", "{}");
        tasks.Should().Contain(records.TaskTitle);

        var search = await CallText(client, key, "search_crm", $$"""{"query":"{{records.LeadName}}"}""");
        search.Should().Contain(records.LeadName);

        var snapshot = await CallText(client, key, "organization_snapshot", "{}");
        snapshot.Should().Contain("Northwind");
        snapshot.Should().Contain("Leads:");
        snapshot.Should().Contain("Tasks:");
    }

    [Fact]
    public async Task Claude_AcceptsToolArgumentsAsJsonString()
    {
        var key = "aci_" + Guid.NewGuid().ToString("N");
        var records = await SeedFullOrganizationAsync(key, "StringArgs Org");

        var client = _factory.CreateClient();
        var escaped = records.LeadName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var payload = "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tools/call\",\"params\":{\"name\":\"search_crm\",\"arguments\":\"{\\\"query\\\":\\\"" + escaped + "\\\"}\"}}";
        var response = await client.SendAsync(ClaudeRequest(key, payload));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain(records.LeadName);
    }

    private static HttpRequestMessage ClaudeRequest(string apiKey, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = Json(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-03-26");
        request.Headers.TryAddWithoutValidation("Origin", "https://claude.ai");
        return request;
    }

    private async Task<string> CallText(HttpClient client, string apiKey, string tool, string argumentsJson)
    {
        var payload = "{\"jsonrpc\":\"2.0\",\"id\":\"" + tool + "\",\"method\":\"tools/call\",\"params\":{\"name\":\"" + tool + "\",\"arguments\":" + argumentsJson + "}}";
        var response = await client.SendAsync(ClaudeRequest(apiKey, payload));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeFalse(body);
        return document.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private sealed record SeededRecords(
        Guid LeadId,
        string LeadName,
        string LeadEmail,
        string ContactName,
        string DealName,
        string TaskTitle);

    private Task SeedOrganizationAsync(string apiKey, string? name = null, string? leadName = null) =>
        SeedFullOrganizationAsync(apiKey, name ?? "Test Org", leadName);

    private async Task<SeededRecords> SeedFullOrganizationAsync(string apiKey, string name, string? leadName = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Name = "Ada",
            Email = $"{userId:N}@example.com",
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = name,
            OwnerUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            WebhookApiKey = apiKey,
            WebhookApiKeyCreatedAtUtc = DateTime.UtcNow,
        });

        var leadId = Guid.NewGuid();
        var resolvedLeadName = leadName ?? $"Lead {Guid.NewGuid():N}"[..16];
        var leadEmail = $"{leadId:N}@example.com";
        var contactName = $"Contact {Guid.NewGuid():N}"[..18];
        var dealName = $"Deal {Guid.NewGuid():N}"[..16];
        var taskTitle = $"Task {Guid.NewGuid():N}"[..16];

        db.Leads.Add(new Lead
        {
            Id = leadId,
            UserId = userId,
            OrganizationId = orgId,
            Name = resolvedLeadName,
            Email = leadEmail,
            Status = "New",
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            Name = contactName,
            Email = $"{Guid.NewGuid():N}@example.com",
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Deals.Add(new Deal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            Name = dealName,
            Value = "25000",
            Currency = "USD",
            Stage = "Qualified",
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.TaskItems.Add(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = orgId,
            Title = taskTitle,
            Status = ACI.Domain.Enums.TaskStatus.Todo,
            Priority = ACI.Domain.Enums.TaskPriority.High,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return new SeededRecords(leadId, resolvedLeadName, leadEmail, contactName, dealName, taskTitle);
    }

    private static StringContent Json(string body) =>
        new(body, Encoding.UTF8, "application/json");
}
