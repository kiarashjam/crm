using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Migrations;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi.Tests;

/// <summary>
/// The price catalogue and the billing on a real SQL Server: the tiers each workspace had,
/// carried into the catalogue by the migration on the path an existing database takes, and
/// the unique keys that make billing, reminders and statements happen once.
/// </summary>
/// <remarks>
/// The in-memory provider runs no SQL and enforces no unique index, so only a real server
/// shows that the carry-over reads the settings as the app does and adds nothing the second
/// time, and that two runs at once bill a period once.
/// </remarks>
public class ClubBillingOnSqlServerTests
{
    /// <summary>The migration before the catalogue's: where an existing database stands before this release.</summary>
    private const string ReleaseBefore = "20261110064019_MoveCampaignsAndMessagesToTables";

    private static string Settings(Guid org, string payload) =>
        $"INSERT INTO [ClubDocuments] ([Id], [OrganizationId], [Kind], [Payload], [UpdatedAtUtc], [Version]) VALUES (NEWID(), '{org}', N'hospitality-settings', N'{payload.Replace("'", "''")}', SYSUTCDATETIME(), NEWID());";

    [SqlServerFact]
    public async Task EachWorkspaceKeepsTheTiersItHad_InOrder_AsTheAppReadsThem_AndASecondRunAddsNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(ReleaseBefore);
            var user = Guid.NewGuid();
            var (custom, plain, francs, broken, messy) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var customSettings = """{"currency":"CHF","tiers":[{"tier":"Founder","monthlyDues":450.5,"initiationFee":5000,"loyaltyMultiplier":3,"benefits":["Everything","A key"]},{"tier":"Junior","monthlyDues":80,"initiationFee":0,"loyaltyMultiplier":1}],"basePointsPerDollar":10}""";
            var messySettings = """{"tiers":[{"tier":"Gold","monthlyDues":300,"loyaltyMultiplier":1.5},{"tier":"gold","monthlyDues":1},{"tier":"","monthlyDues":5},{"tier":"  Silver  ","monthlyDues":-4,"initiationFee":"lots","loyaltyMultiplier":0},"not a tier"]}""";
            var sql = new List<string>
            {
                $@"INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc], [EmailVerifiedAtUtc])
                   VALUES ('{user}', N'Owner', N'owner@example.ch', N'x', 0, SYSUTCDATETIME(), SYSUTCDATETIME());",
            };
            foreach (var (org, currency) in new[] { (custom, "CHF"), (plain, "CHF"), (francs, "EUR"), (broken, "CHF"), (messy, "CHF") })
                sql.Add($@"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [Currency], [StaleDealDays]) VALUES ('{org}', N'Club', '{user}', SYSUTCDATETIME(), N'{currency}', 30);");
            sql.Add(Settings(custom, customSettings));
            sql.Add(Settings(broken, "not json at all"));
            sql.Add(Settings(messy, messySettings));
            await SqlServerApp.ExecuteAsync(database, string.Join("\n", sql));

            // The app starts on it and runs the migrations it has not had, the carry-over among them.
            using (SqlServerApp.Start(database)) { }

            await using (var db = SqlServerSchema.Context(database))
            {
                var tiers = (await db.ClubCatalogItems.IgnoreQueryFilters().Where(i => i.Kind == CatalogKinds.Membership).ToListAsync())
                    .GroupBy(i => i.OrganizationId!.Value)
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).ToList());

                tiers[custom].Select(t => (t.Name, t.Price, t.InitiationFee, t.LoyaltyMultiplier, t.Cadence, t.Currency, t.IsActive))
                    .Should().Equal(("Founder", 450.5m, (decimal?)5000m, (decimal?)3m, "Monthly", "CHF", true), ("Junior", 80m, 0m, 1m, "Monthly", "CHF", true));
                JsonSerializer.Deserialize<string[]>(tiers[custom][0].Benefits!).Should().Equal("Everything", "A key");

                var defaults = new[] { ("Bronze", 49m), ("Silver", 129m), ("Gold", 299m), ("Platinum", 599m) };
                tiers[plain].Select(t => (t.Name, t.Price)).Should().Equal(defaults, "a workspace that saved no settings had the four tiers the page shows");
                tiers[broken].Select(t => (t.Name, t.Price)).Should().Equal(defaults, "unreadable settings list no tiers");
                tiers[francs].Should().OnlyContain(t => t.Currency == "EUR", "in the workspace's currency");
                tiers[plain].Select(t => t.LoyaltyMultiplier).Should().Equal(1m, 1.25m, 1.5m, 2m);

                tiers[messy].Select(t => (t.Name, t.Price, t.InitiationFee, t.LoyaltyMultiplier))
                    .Should().Equal(("Gold", 300m, (decimal?)0m, (decimal?)1.5m), ("Silver", 0m, 0m, 1m));

                // The app reads a settings document by the same rule, for a workspace made later.
                foreach (var (org, payload) in new[] { (custom, customSettings), (messy, messySettings), (broken, "not json at all") })
                    ClubCatalogService.TiersFrom(payload).Select(t => (t.Name, t.Dues, t.Fee, t.Multiplier))
                        .Should().Equal(tiers[org].Select(t => (t.Name, t.Price, t.InitiationFee!.Value, t.LoyaltyMultiplier!.Value)));
            }

            // Run again, even after a club renamed a tier, it adds nothing.
            await SqlServerApp.ExecuteAsync(database, $"UPDATE [ClubCatalogItems] SET [Name] = N'Or' WHERE [OrganizationId] = '{plain}' AND [Name] = N'Gold';");
            var before = await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubCatalogItems]");
            await SqlServerApp.ExecuteAsync(database, AddClubCatalogAndBilling.CarryOverTiersSql);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubCatalogItems]")).Should().Be(before);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubDocuments] WHERE [Kind] = N'hospitality-settings'"))
                .Should().Be(3, "the settings are copied, not moved out");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<System.Text.Json.JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A club on the app, with a member three monthly periods behind on their dues.</summary>
    private static async Task<(HttpClient Client, Guid Org, Guid MemberId)> ClubBehindOnDuesAsync(SqlServerApp app)
    {
        var (client, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var org = (await Ok(await client.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon46" })), "create the club"))
            .GetProperty("id").GetGuid();
        client.In(org);
        var memberId = (await Ok(await client.PostAsync("/api/members", Workspaces.Body(new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();
        var today = DateTime.UtcNow;
        var first = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2);
        await Ok(await client.PostAsync("/api/subscriptions", Workspaces.Body(new
        {
            memberId, name = "Gold dues", amount = 299, cadence = "Monthly", nextChargeAtUtc = first.ToString("yyyy-MM-dd"),
        })), "add subscription");
        await Ok(await client.PutAsync("/api/hospitality/settings", Workspaces.Body(new { billing = new { automaticBilling = true } })), "turn on automatic billing");
        return (client, org, memberId);
    }

    /// <summary>A moment today after 03:00 in Zurich, when the daily run may go.</summary>
    private static DateTime RunTime()
    {
        var zone = ACI.Application.Common.RegionalSettings.ZoneOf("Europe/Zurich");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        return local.TimeOfDay >= TimeSpan.FromHours(3) ? DateTime.UtcNow : DateTime.UtcNow.Add(TimeSpan.FromHours(3) - local.TimeOfDay + TimeSpan.FromMinutes(1));
    }

    [SqlServerFact]
    public async Task TwoServersRunningTheDailyRunAtOnce_TakeTheDayOnce_AndBillEachPeriodOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, org, memberId) = await ClubBehindOnDuesAsync(app);
            var at = RunTime();

            async Task<int> RunAsync()
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubBilling>().RunScheduledAsync(at);
            }
            var ran = await Task.WhenAll(RunAsync(), RunAsync());

            ran.Sum().Should().Be(1, "the day is taken once");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubBillingRuns] WHERE [OrganizationId] = '{org}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [MemberId] = '{memberId}' AND [SubscriptionId] IS NOT NULL"))
                .Should().Be(3, "three periods, each billed once");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TheRunRemindsAndLapsesOnlyWhatAMembersCreditDoesNotCover()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Ok(await client.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon46" })), "create the club"))
                .GetProperty("id").GetGuid();
            client.In(org);
            async Task<Guid> MemberAsync(string first, decimal credit)
            {
                var id = (await Ok(await client.PostAsync("/api/members", Workspaces.Body(new
                {
                    firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
                    houseAccountBalance = credit,
                })), $"add {first}")).GetProperty("id").GetGuid();
                await Ok(await client.PostAsync("/api/charges", Workspaces.Body(new
                {
                    memberId = id, kind = "Dues", description = "Gold dues", amount = 300, dueAtUtc = DateTime.UtcNow.AddDays(-45).ToString("o"),
                })), $"charge {first}");
                return id;
            }
            // Jean's credit covers his dues; Max's covers 100 of them; Lea has none.
            var (jean, max, lea) = (await MemberAsync("Jean", 500), await MemberAsync("Max", 100), await MemberAsync("Lea", 0));
            await Ok(await client.PutAsync("/api/hospitality/settings", Workspaces.Body(new
            {
                billing = new { remindersEnabled = true, reminderDays = new[] { 3 }, lapseEnabled = true, gracePeriodDays = 30 },
            })), "turn on reminders and lapsing");

            using (var scope = app.Services.CreateScope())
                (await scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubBilling>().RunScheduledAsync(RunTime())).Should().Be(1);

            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReminders] WHERE [MemberId] = '{jean}'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReminders] WHERE [MemberId] IN ('{max}', '{lea}') AND [Status] = N'queued'"))
                .Should().Be(2);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [ClubMembers] WHERE [Id] = '{jean}'")).Should().Be("Active");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [Id] IN ('{max}', '{lea}') AND [Status] = N'Lapsed'")).Should().Be(2);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [OverdueCharges] FROM [ClubBillingRuns] WHERE [OrganizationId] = '{org}'")).Should().Be(2);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoBillingRunsStartedByHandAtOnce_BillEachPeriodOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            // Both runs read the subscription before either bills it, every time.
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ACI.Application.Services.ILedgerReadObserver>(gate));
            var (client, _, memberId) = await ClubBehindOnDuesAsync(app);
            gate.Arm("subscriptions");

            var answers = await Task.WhenAll(client.PostAsync("/api/billing/run", null), client.PostAsync("/api/billing/run", null));

            answers.Should().OnlyContain(a => a.IsSuccessStatusCode, "a period billed by the other run is skipped, not a failure");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [MemberId] = '{memberId}' AND [SubscriptionId] IS NOT NULL"))
                .Should().Be(3);
            (await SqlServerApp.ScalarAsync(database,
                    $"SELECT COUNT(*) FROM (SELECT [BillingPeriodStartUtc] FROM [ClubCharges] WHERE [MemberId] = '{memberId}' GROUP BY [BillingPeriodStartUtc] HAVING COUNT(*) > 1) d"))
                .Should().Be(0);
            var member = (await Ok(await client.GetAsync("/api/members"), "members"))[0];
            member.GetProperty("houseAccountBalance").GetDecimal().Should().Be(-897, "three periods of 299 on the account, no more");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
