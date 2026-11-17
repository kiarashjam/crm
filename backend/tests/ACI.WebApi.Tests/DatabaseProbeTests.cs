using System.Diagnostics;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// Start-up asks whether its database exists. A missing one has to answer at once:
/// EF's own check logs in to it, SqlClient retries that failed login after 10 s, and on
/// a busy server the retry timed out with an error EF did not read as "missing", so a
/// first start in development failed (FreshDatabaseStartupTests, on CI).
/// </summary>
public class DatabaseProbeTests
{
    private static AppDbContext On(string database) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);

    [SqlServerFact]
    public async Task AMissingDatabaseIsMissing_WithoutWaitingForARetry()
    {
        await SqlServerApp.WaitForServerAsync();
        await using var db = On(SqlServerApp.NewDatabaseName());

        var clock = Stopwatch.StartNew();
        (await DatabaseProbe.ExistsAsync(db.Database)).Should().BeFalse();
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(9), "SqlClient's retry of the failed login waits 10 s");
    }

    [SqlServerFact]
    public async Task AnExistingDatabaseExists()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await using var db = On(database);
            (await DatabaseProbe.ExistsAsync(db.Database)).Should().BeTrue();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AnotherProviderIsAskedItsOwnWay()
    {
        // The in-memory provider the API tests start on has no relational creator: as before,
        // start-up reports that as its database error rather than probing SQL Server.
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var ask = () => DatabaseProbe.ExistsAsync(db.Database);
        await ask.Should().ThrowAsync<InvalidOperationException>();
    }
}
