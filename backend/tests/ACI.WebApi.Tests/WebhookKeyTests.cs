using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
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

    [SqlServerFact]
    public async Task AKeyStillInPlainTextOnADatabaseFromBeforeIsHashedAtStartAndStillPostsLeads()
    {
        // The start-up step runs its UPDATE through EXEC, behind a check that the plain column
        // is there at all (a database the migrations made has none), so the parse below no
        // longer sees the UPDATE. On a database that still holds a plain key it has to hash it
        // exactly as the app hashes a key it is sent, or that workspace's form stops posting.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SqlServerSchema.CreateLegacyAsync(database);
            var ownerId = Guid.NewGuid();
            var org = Guid.NewGuid();
            const string key = "aci_3f1c9a7e5b2d4c6e8f0a1b3c5d7e9f21";
            await SqlServerApp.ExecuteAsync(database, $@"
                INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc])
                VALUES ('{ownerId}', N'Owner', N'{ownerId:N}@example.com', NULL, 0, SYSUTCDATETIME());
                INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [WebhookApiKey], [WebhookApiKeyCreatedAtUtc])
                VALUES ('{org}', N'Pavillon 46', '{ownerId}', SYSUTCDATETIME(), N'{key}', SYSUTCDATETIME());");

            using var app = SqlServerApp.Start(database);

            (await SqlServerApp.ScalarAsync(database, $"SELECT [WebhookApiKey] FROM [Organizations] WHERE [Id] = '{org}'"))
                .Should().BeNull("the plain copy must not survive");
            (await SqlServerApp.ScalarAsync(database, $"SELECT [WebhookApiKeyHash] FROM [Organizations] WHERE [Id] = '{org}'"))
                .Should().Be(ACI.Application.Services.OrganizationService.HashApiKey(key));

            var form = app.CreateClient();
            form.DefaultRequestHeaders.Add("X-Api-Key", key);
            var posted = await form.PostAsync("/api/webhook/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch" }));
            posted.StatusCode.Should().Be(HttpStatusCode.Created, await posted.Content.ReadAsStringAsync());
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
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
