using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace's currency, locale and time zone: what it says it works in, who may
/// change that, and that an existing database gets them with the defaults.
/// </summary>
public class OrganizationSettingsTests
{
    private static async Task<JsonElement> WorkspaceAsSeenBy(HttpClient client, Guid org)
    {
        var res = await client.GetAsync($"/api/organizations/{org}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task AWorkspaceWorksInFrancsInZurichUntilItSaysOtherwise()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);

        var seen = await WorkspaceAsSeenBy(owner, org);
        seen.GetProperty("currency").GetString().Should().Be("CHF");
        seen.GetProperty("locale").GetString().Should().Be("de-CH");
        seen.GetProperty("timeZone").GetString().Should().Be("Europe/Zurich");

        var listed = JsonDocument.Parse(await (await owner.GetAsync("/api/organizations")).Content.ReadAsStringAsync()).RootElement;
        listed[0].GetProperty("currency").GetString().Should().Be("CHF", "the app formats from the list it already loads");
    }

    [Fact]
    public async Task TheOwnerOrAManagerSetsThemAndARepOrAViewerCannot()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (manager, managerId) = await SignInAsync(factory, "manager@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);
        manager.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        (await rep.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "it prices every new deal and moves every report's months");
        (await viewer.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var saved = await manager.PutAsync($"/api/organizations/{org}/settings",
            Body(new { currency = " eur ", locale = "fr-CH", timeZone = "America/New_York" }));
        var body = await saved.Content.ReadAsStringAsync();
        saved.StatusCode.Should().Be(HttpStatusCode.OK, body);
        JsonDocument.Parse(body).RootElement.GetProperty("currency").GetString().Should().Be("EUR");

        var seen = await WorkspaceAsSeenBy(rep, org);
        seen.GetProperty("currency").GetString().Should().Be("EUR");
        seen.GetProperty("locale").GetString().Should().Be("fr-CH");
        seen.GetProperty("timeZone").GetString().Should().Be("America/New_York");

        // A field left out keeps its value.
        (await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { locale = "de-CH" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WorkspaceAsSeenBy(owner, org)).GetProperty("currency").GetString().Should().Be("EUR");
    }

    [Theory]
    [InlineData("EURO", null, null)]
    [InlineData("€", null, null)]
    [InlineData(null, "xx-YY", null)]
    [InlineData(null, "de_CH", null)]
    [InlineData(null, null, "Mars/Olympus_Mons")]
    public async Task WhatIsNotACurrencyALocaleOrAZoneIsRefusedAndNothingChanges(string? currency, string? locale, string? timeZone)
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);

        var res = await owner.PutAsync($"/api/organizations/{org}/settings",
            Body(new { currency = currency ?? "EUR", locale = locale ?? "fr-CH", timeZone = timeZone ?? "Europe/Paris" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
        var seen = await WorkspaceAsSeenBy(owner, org);
        seen.GetProperty("currency").GetString().Should().Be("CHF");
        seen.GetProperty("locale").GetString().Should().Be("de-CH");
        seen.GetProperty("timeZone").GetString().Should().Be("Europe/Zurich");
    }

    /// <summary>
    /// A database from before the columns: on start they are added, and every workspace
    /// already there works in the defaults.
    /// </summary>
    [SqlServerFact]
    public async Task AnExistingDatabaseGetsTheColumnsWithTheDefaults()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid userId;
            using (var app = SqlServerApp.Start(database))
                (_, userId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

            foreach (var column in new[] { "Currency", "Locale", "TimeZone" })
            {
                await SqlServerApp.ExecuteAsync(database, $"ALTER TABLE [Organizations] DROP CONSTRAINT [DF_Organizations_{column}]");
                await SqlServerApp.ExecuteAsync(database, $"ALTER TABLE [Organizations] DROP COLUMN [{column}]");
            }
            var org = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc]) VALUES ('{org}', N'Pavillon46', '{userId}', SYSUTCDATETIME())");

            using (SqlServerApp.Start(database))
            {
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Currency] FROM [Organizations] WHERE [Id] = '{org}'")).Should().Be("CHF");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Locale] FROM [Organizations] WHERE [Id] = '{org}'")).Should().Be("de-CH");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [TimeZone] FROM [Organizations] WHERE [Id] = '{org}'")).Should().Be("Europe/Zurich");
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
