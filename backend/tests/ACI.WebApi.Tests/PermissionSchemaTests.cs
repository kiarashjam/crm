using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Domain.Common;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The two columns that hold what a member may do, which reach an existing
/// OrganizationMembers table only by a guarded ALTER in Program.cs, and the SQL that asks
/// who sees every deal.
/// </summary>
/// <remarks>
/// A column added to an entity and forgotten in the DDL passes every test on SQLite, then
/// fails every sign-in against the real database: every request reads the membership.
/// </remarks>
public class PermissionSchemaTests
{
    [Theory]
    [InlineData("PermissionPreset", "nvarchar(40)")]
    [InlineData("Permissions", "nvarchar(400)")]
    public void AnExistingOrganizationMembersTableIsGivenTheColumnByAGuardedAlter(string column, string type)
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf($"IF COL_LENGTH(N'dbo.OrganizationMembers', N'{column}') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, $"production has OrganizationMembers without {column}, and only an ALTER gives it one");
        var alter = ddl.IndexOf($"ALTER TABLE [OrganizationMembers] ADD [{column}] {type} NULL", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");

        var model = ClubSchemaTests.Between(ClubSchemaTests.BaselineScript(), "CREATE TABLE [OrganizationMembers]", "CONSTRAINT");
        Regex.IsMatch(model, $@"\[{column}\]\s+{Regex.Escape(type)}\s+NULL").Should().BeTrue($"the model maps {column} as {type} NULL, as the DDL adds it");
    }

    [SqlServerFact]
    public async Task OnSqlServerAnExistingDatabaseIsGivenTheColumns_AndWhoSeesEveryDealIsAnsweredInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            // As production is before this deploy.
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [OrganizationMembers] DROP COLUMN [PermissionPreset]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [OrganizationMembers] DROP COLUMN [Permissions]");

            using (SqlServerApp.Start(database)) { }
            using var app = SqlServerApp.Start(database);
            (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH('OrganizationMembers', 'PermissionPreset')")).Should().NotBeNull();
            (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH('OrganizationMembers', 'Permissions')")).Should().NotBeNull();

            var (owner, ownerId) = await SignInAsync(app, "olga@pavillon46.ch");
            var org = (await Json(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })))).GetProperty("id").GetGuid();
            owner.In(org);
            var people = new Dictionary<string, (HttpClient Client, Guid Id)>();
            foreach (var (name, role, preset, permissions) in new (string, OrgMemberRole, string?, string?)[]
            {
                ("oldmanager", OrgMemberRole.Manager, null, null),
                ("oldmember", OrgMemberRole.Member, null, null),
                ("manager", OrgMemberRole.Manager, "manager", null),
                ("rep", OrgMemberRole.Member, "sales_rep", null),
                ("trusted", OrgMemberRole.Member, "sales_rep", MemberPermissions.Format([MemberPermissions.EditRecords, MemberPermissions.SeeAllDeals])),
            })
            {
                var (client, id) = await SignInAsync(app, $"{name}@pavillon46.ch");
                people[name] = (client.In(org), id);
                await SqlServerApp.ExecuteAsync(database,
                    $"INSERT INTO [OrganizationMembers] ([OrganizationId], [UserId], [Role], [JoinedAtUtc], [PermissionPreset], [Permissions]) " +
                    $"VALUES ('{org}', '{id}', '{role}', SYSUTCDATETIME(), {(preset == null ? "NULL" : $"N'{preset}'")}, {(permissions == null ? "NULL" : $"N'{permissions}'")})");
            }

            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);
            var seeing = await db.OrganizationMembers.Where(m => m.OrganizationId == org).Where(DealVisibility.SeesAllDeals).Select(m => m.UserId).ToListAsync();
            seeing.Should().BeEquivalentTo(new[] { ownerId, people["oldmanager"].Id, people["manager"].Id, people["trusted"].Id });

            // And through the API: a private deal shows only to them.
            var deal = Guid.NewGuid();
            db.Deals.Add(new ACI.Domain.Entities.Deal
            {
                Id = deal, UserId = ownerId, AssigneeId = ownerId, OrganizationId = org, Name = "Board seat", Value = "50000",
                Currency = "CHF", Probability = 50, IsPrivate = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            foreach (var (name, (client, _)) in people)
                (await client.GetAsync($"/api/deals/{deal}")).StatusCode
                    .Should().Be(name is "oldmanager" or "manager" or "trusted" ? HttpStatusCode.OK : HttpStatusCode.NotFound, name);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
