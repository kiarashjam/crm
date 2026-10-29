using System.Text.Json;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;

namespace ACI.WebApi.Tests;

/// <summary>
/// Starting the API against a new database, on a real SQL Server.
/// </summary>
/// <remarks>
/// The start-up schema work used to run before the migrations that create the core
/// tables, and its first statement points a foreign key at Users. So a new database,
/// whether missing (docker-compose's first start) or created empty (a new Azure SQL
/// database), failed on that statement on every start and was never set up.
/// </remarks>
public class FreshDatabaseStartupTests
{
    [SqlServerFact]
    public async Task AMissingDatabaseIsCreatedAndSetUp()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = SqlServerApp.NewDatabaseName();
        try
        {
            using (var app = SqlServerApp.Start(database))
            {
                await ShouldBeSetUpAsync(app, database);
            }

            // A second start finds the tables there, so it takes the usual way, and
            // every statement of it has to cope with a database the first one made.
            using (var again = SqlServerApp.Start(database))
            {
                await ShouldBeSetUpAsync(again, database);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnEmptyDatabaseIsSetUp()
    {
        // What the provisioning scripts make: `az sql db create` leaves an empty database.
        await SqlServerApp.WaitForServerAsync();
        var database = SqlServerApp.NewDatabaseName();
        await SqlServerApp.ExecuteAsync("master", $"CREATE DATABASE [{database}]");
        try
        {
            using var app = SqlServerApp.Start(database);
            await ShouldBeSetUpAsync(app, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ANewDatabaseHasNoSignInWithAPublishedPassword()
    {
        // The seeder adds demo@aci.local, whose password is in the repository, to a
        // database with no accounts. Now that a new database is set up at all, a new
        // production deployment would have come up with that sign-in, so it is only
        // added in development.
        await SqlServerApp.WaitForServerAsync();
        var database = SqlServerApp.NewDatabaseName();
        try
        {
            using var app = SqlServerApp.Start(database);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [Users] WHERE [Email] = N'{SeedData.SeedUserEmail}'"))
                .Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Templates] WHERE [IsSystemTemplate] = 1"))
                .Should().NotBe(0, "the starter templates are still seeded");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldBeSetUpAsync(SqlServerApp app, string database)
    {
        var client = app.CreateClient();
        var status = await client.GetStringAsync("/db-status");
        using (var doc = JsonDocument.Parse(status))
            doc.RootElement.GetProperty("status").GetString().Should().Be("ok", status);

        // Tables from the migrations and from the start-up schema work alike.
        foreach (var table in new[] { "Users", "Organizations", "Leads", "Notifications", "LeadStatusEvents", "AppliedDataFixes" })
            (await SqlServerApp.ScalarAsync(database, $"SELECT OBJECT_ID(N'dbo.{table}', N'U')")).Should().NotBeNull(table);

        // And the app works on it: an account can be made and used.
        var (signedIn, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        (await signedIn.GetAsync("/api/auth/me")).IsSuccessStatusCode.Should().BeTrue();
    }
}
