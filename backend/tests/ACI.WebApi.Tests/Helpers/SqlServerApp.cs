using ACI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// A test that needs a real SQL Server. The start-up schema work is T-SQL, which
/// neither the in-memory provider nor SQLite can run, so only a real server shows
/// whether a database is actually set up.
/// </summary>
/// <remarks>
/// Set CADENCE_TEST_SQLSERVER to a connection string for a server, without a
/// database name; each test makes its own database and drops it afterwards. CI
/// starts a SQL Server container and sets it. Without it the test is reported as
/// skipped, never as passed.
/// </remarks>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(SqlServerApp.ServerConnectionString))
            Skip = "Needs SQL Server: set CADENCE_TEST_SQLSERVER to a connection string for one.";
    }
}

/// <summary>The real API, started against its own database on the SQL Server in CADENCE_TEST_SQLSERVER.</summary>
public sealed class SqlServerApp : WebApplicationFactory<Program>
{
    public static string? ServerConnectionString => Environment.GetEnvironmentVariable("CADENCE_TEST_SQLSERVER");

    private readonly string _connectionString;

    private SqlServerApp(string connectionString) => _connectionString = connectionString;

    /// <summary>A database name no other test uses, not yet created.</summary>
    public static string NewDatabaseName() => "CadenceTest_" + Guid.NewGuid().ToString("N");

    public static string ConnectionString(string database) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = database }.ConnectionString;

    /// <summary>Start the API against <paramref name="database"/>. Start-up, schema work included, has finished when this returns.</summary>
    public static SqlServerApp Start(string database)
    {
        var app = new SqlServerApp(ConnectionString(database));
        _ = app.Server; // builds and starts the host, which runs the database set-up
        return app;
    }

    /// <summary>Run <paramref name="sql"/> against <paramref name="database"/> ("master" for the server itself).</summary>
    public static async Task<object?> ScalarAsync(string database, string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    public static Task ExecuteAsync(string database, string sql) => ScalarAsync(database, sql);

    /// <summary>Waits for the server to accept connections: a container that has just started takes a few seconds.</summary>
    public static async Task WaitForServerAsync()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await ScalarAsync("master", "SELECT 1");
                return;
            }
            catch (SqlException) when (attempt < 60)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public static async Task DropDatabaseAsync(string database)
    {
        SqlConnection.ClearAllPools();
        await ExecuteAsync("master",
            $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            // As in CustomWebApplicationFactory: never open a real SMTP connection.
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Email:SmtpHost"] = "" });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }
}
