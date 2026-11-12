using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The Version column of leads, deals, contacts and companies: made with the tables by the
/// baseline migration on a new database, and added by the start-up DDL on one from before.
/// </summary>
/// <remarks>
/// The four tables already exist in production, so there the column arrives only by a
/// guarded ALTER. Missing, every query on them fails; nullable or without a default, a row
/// written during that deploy by an instance still running the previous code would have no
/// version. A new database never runs code from before the column, so it needs no default.
/// </remarks>
public class VersionSchemaTests
{
    public static readonly TheoryData<string> Tables = new() { "Leads", "Deals", "Contacts", "Companies" };

    [Theory]
    [MemberData(nameof(Tables))]
    public void AnExistingTableIsGivenItsVersionByAGuardedAlter(string table)
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf($"IF COL_LENGTH(N'dbo.{table}', N'Version') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, $"an existing database has {table} without Version, and only an ALTER gives it one");
        var alter = ddl.IndexOf(
            $"ALTER TABLE [{table}] ADD [Version] uniqueidentifier NOT NULL CONSTRAINT [DF_{table}_Version] DEFAULT NEWID();",
            guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");
    }

    [Fact]
    public void TheModelMapsTheVersionAsEfWouldWriteTheColumn()
    {
        var script = ClubSchemaTests.BaselineScript();
        foreach (var table in new[] { "Leads", "Deals", "Contacts", "Companies" })
            ClubSchemaTests.Between(script, $"CREATE TABLE [{table}]", "CONSTRAINT [PK_")
                .Should().Contain("[Version] uniqueidentifier NOT NULL", $"the DDL adds {table}.Version as the model maps it");
    }

    [SqlServerFact]
    public async Task ANewDatabaseHasAVersionOnEveryEditableRecord()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database))
                await ShouldHaveTheVersionsAsync(database, withDeployDefault: false);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnExistingDatabaseIsGivenTheVersionsOnStartAndItsRowsTakeOne()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (var app = SqlServerApp.Start(database))
            {
                var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
                var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
                created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
                owner.In(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid());
                foreach (var name in new[] { "Anna", "Ben" })
                {
                    (await owner.PostAsync("/api/leads", Body(new { name, email = $"{name}@example.ch", source = "Website", status = "New" }))).EnsureSuccessStatusCode();
                    (await owner.PostAsync("/api/deals", Body(new { name, amount = 1000 }))).EnsureSuccessStatusCode();
                    (await owner.PostAsync("/api/companies", Body(new { name }))).EnsureSuccessStatusCode();
                }
            }

            // As production is before this deploy.
            foreach (var table in new[] { "Leads", "Deals", "Contacts", "Companies" })
            {
                await SqlServerApp.DropColumnsAsync(database, table, "Version");
            }

            using (SqlServerApp.Start(database))
            {
                await ShouldHaveTheVersionsAsync(database, withDeployDefault: true);
                foreach (var table in new[] { "Leads", "Deals", "Contacts", "Companies" })
                {
                    (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [{table}] WHERE [Version] = '00000000-0000-0000-0000-000000000000'"))
                        .Should().Be(0, $"every {table} row already there takes a version");
                }
            }

            // And a third start finds it all there already.
            using (SqlServerApp.Start(database))
                await ShouldHaveTheVersionsAsync(database, withDeployDefault: true);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldHaveTheVersionsAsync(string database, bool withDeployDefault)
    {
        foreach (var table in new[] { "Leads", "Deals", "Contacts", "Companies" })
        {
            (await SqlServerApp.ScalarAsync(database,
                $"SELECT COUNT(*) FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id " +
                $"WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'Version' AND t.name = N'uniqueidentifier' AND c.is_nullable = 0"))
                .Should().Be(1, $"{table}.Version is a NOT NULL uniqueidentifier");
            if (withDeployDefault)
                (await SqlServerApp.ScalarAsync(database,
                    $"SELECT COUNT(*) FROM sys.default_constraints WHERE name = N'DF_{table}_Version' AND parent_object_id = OBJECT_ID(N'dbo.{table}')"))
                    .Should().Be(1, $"a row written by an older instance during a deploy still gets a {table} version");
        }
    }
}
