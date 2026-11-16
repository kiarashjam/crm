using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The member portal on a real SQL Server: its migration on a database with resources already in
/// it, a sign-in link that eight requests race to use, and two first sign-ins of one member.
/// </summary>
/// <remarks>
/// The in-memory provider has no unique indexes and no row counts on a delete, so what keeps a
/// link single-use and one record of a member's use of the portal only shows on a real server.
/// </remarks>
public class MemberPortalOnSqlServerTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<object?> Value(string database, string sql) => SqlServerApp.ScalarAsync(database, sql);

    [SqlServerFact]
    public async Task TheMigrationAddsThePortalsTables_AndEveryResourceThereWasStaysOutOfThePortal()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var ours = migrations.Single(m => m.EndsWith("_AddMemberPortal", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(ours) - 1]);

            // A resource the club had before the release.
            var resource = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database, $@"
                INSERT INTO [ClubResources] ([Id], [OrganizationId], [Name], [Kind], [Capacity], [BookingMode], [DepositAmount], [NoShowFee], [Currency], [Active], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc], [EditVersion], [Version])
                VALUES ('{resource}', '{Guid.NewGuid()}', N'Spa suite', N'Spa', 2, N'Exclusive', 0, 0, N'CHF', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NEWID(), NEWID())");

            await SqlServerSchema.MigrateAloneAsync(database);

            (await Value(database, $"SELECT [MemberBooking] FROM [ClubResources] WHERE [Id] = '{resource}'")).Should().Be("off",
                "no resource is offered in the member portal until the club says so");
            foreach (var table in new[] { "MemberPortalSettings", "MemberPortalAccounts", "MemberPortalSessions", "MemberPortalLinks" })
                (await Value(database, $"SELECT COUNT(*) FROM sys.tables WHERE name = '{table}'")).Should().Be(1, $"{table} should exist");
            (await Value(database, "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_MemberPortalLinks_TokenHash' AND is_unique = 1")).Should().Be(1);
            (await Value(database, "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_MemberPortalAccounts_OrganizationId_MemberId' AND is_unique = 1")).Should().Be(1);
            (await Value(database, "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_MemberPortalSettings_PortalKey' AND is_unique = 1")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<(SqlServerApp App, string Database, string Key)> ClubAsync()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        var app = SqlServerApp.Start(database);
        var (staff, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var org = (await Ok(await staff.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
        staff.In(org);
        await Ok(await staff.PostAsync("/api/members", Body(new { firstName = "Anna", lastName = "Muster", email = "anna@example.ch", tier = "Gold", status = "Active" })), "add Anna");
        var settings = await Ok(await staff.PutAsync("/api/member-portal/settings", Body(new { enabled = true })), "turn the portal on");
        var url = settings.GetProperty("portalUrl").GetString()!;
        return (app, database, Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]));
    }

    private static async Task<IReadOnlyList<string>> TokensAsync(string database) =>
        Regex.Matches((await Value(database, "SELECT TOP 1 [Body] FROM [OutboundMessages] WHERE [Source] = 'portal' ORDER BY [CreatedAtUtc] DESC") as string) ?? "",
                @"#token=([A-Za-z0-9_\-%]+)")
            .Select(m => Uri.UnescapeDataString(m.Groups[1].Value))
            .ToList();

    [SqlServerFact]
    public async Task OfEightRequestsRacingWithOneLink_OneSignsIn()
    {
        var (app, database, key) = await ClubAsync();
        try
        {
            (await app.CreateClient().PostAsync($"/api/public/portal/{key}/sign-in-link", Body(new { email = "anna@example.ch" }))).StatusCode.Should().Be(HttpStatusCode.Accepted);
            var token = (await TokensAsync(database)).Single();

            var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                app.CreateClient().PostAsync($"/api/public/portal/{key}/sign-in", Body(new { token })))));

            answers.Count(a => a.StatusCode == HttpStatusCode.OK).Should().Be(1, "a link opens one session, whoever uses it first");
            answers.Count(a => a.StatusCode == HttpStatusCode.BadRequest).Should().Be(7);
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalSessions]")).Should().Be(1);
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalLinks]")).Should().Be(0);
        }
        finally
        {
            app.Dispose();
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoFirstSignInsAtOnce_KeepOneRecordOfTheMembersUse()
    {
        var (app, database, key) = await ClubAsync();
        try
        {
            var client = app.CreateClient();
            for (var i = 0; i < 2; i++)
                (await client.PostAsync($"/api/public/portal/{key}/sign-in-link", Body(new { email = "anna@example.ch" }))).StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalLinks]")).Should().Be(2);
            var all = new List<string>();
            foreach (var body in await SqlServerAllBodiesAsync(database))
                all.AddRange(Regex.Matches(body, @"#token=([A-Za-z0-9_\-%]+)").Select(m => Uri.UnescapeDataString(m.Groups[1].Value)));
            all.Should().HaveCount(2);

            var answers = await Task.WhenAll(all.Select(token => Task.Run(() =>
                app.CreateClient().PostAsync($"/api/public/portal/{key}/sign-in", Body(new { token })))));
            answers.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.OK, "each link is its own");
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalSessions]")).Should().Be(2);
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalAccounts]")).Should().Be(1, "one record of a member's use, made by whichever came first");
            (await Value(database, "SELECT COUNT(*) FROM [MemberPortalAccounts] WHERE [FirstSignInAtUtc] IS NOT NULL")).Should().Be(1);
        }
        finally
        {
            app.Dispose();
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<IReadOnlyList<string>> SqlServerAllBodiesAsync(string database)
    {
        var joined = await Value(database, "SELECT STRING_AGG(CAST([Body] AS nvarchar(max)), N'\u0001') FROM [OutboundMessages] WHERE [Source] = 'portal'") as string;
        return (joined ?? "").Split('\u0001', StringSplitOptions.RemoveEmptyEntries);
    }
}
