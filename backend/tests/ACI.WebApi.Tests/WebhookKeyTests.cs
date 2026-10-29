using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Persistence;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The lead webhook key is shown once and stored as a hash, like the MCP key. It used to
/// sit in plain text in the Organizations table and came back on every page load.
/// </summary>
public class WebhookKeyTests
{
    [Fact]
    public async Task TheKeyIsShownOnceAndOnlyItsHashIsKept()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@club.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon 46");

        var res = await owner.In(org).PostAsync($"/api/webhook/organizations/{org}/generate-key", null);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        string key;
        using (var doc = JsonDocument.Parse(body)) key = doc.RootElement.GetProperty("apiKey").GetString()!;
        key.Should().StartWith("aci_");

        var stored = await factory.Services.Read(db => db.Organizations.Single(o => o.Id == org));
        stored.WebhookApiKeyHash.Should().Be(ACI.Application.Services.OrganizationService.HashApiKey(key)).And.NotContain(key);

        var info = await owner.In(org).GetStringAsync($"/api/webhook/organizations/{org}");
        info.Should().Contain("\"hasApiKey\":true").And.NotContain(key);

        var form = factory.CreateClient();
        form.DefaultRequestHeaders.Add("X-Api-Key", key);
        (await form.PostAsync("/api/webhook/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the key still posts leads");

        var wrong = factory.CreateClient();
        wrong.DefaultRequestHeaders.Add("X-Api-Key", stored.WebhookApiKeyHash);
        (await wrong.PostAsync("/api/webhook/leads", Body(new { name = "Eve", email = "eve@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the stored hash is not a key");
    }

    [Fact]
    public void TheStartUpStepThatHashesOldKeysIsValidTSql()
    {
        // It runs against SQL Server at start-up, which these tests do not have; a syntax
        // error there would stop every existing webhook sender until someone noticed.
        new TSql160Parser(initialQuotedIdentifiers: true)
            .Parse(new StringReader(LegacyWebhookKeys.HashInPlaceSql), out var errors);

        errors.Should().BeEmpty();
        LegacyWebhookKeys.HashInPlaceSql.Should().Contain("[WebhookApiKey] = NULL", "the plain copy must not survive");
    }
}
