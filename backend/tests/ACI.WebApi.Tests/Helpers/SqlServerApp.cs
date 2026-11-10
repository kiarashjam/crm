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
    private readonly string _environment;
    private readonly Action<IServiceCollection>? _services;

    private SqlServerApp(string connectionString, string environment, Action<IServiceCollection>? services)
    {
        _connectionString = connectionString;
        _environment = environment;
        _services = services;
    }

    /// <summary>A database name no other test uses, not yet created.</summary>
    public static string NewDatabaseName() => "CadenceTest_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// A new, empty database no other test uses, as the provisioning scripts leave one.
    /// Outside development the API sets up an empty database but never creates a missing one.
    /// </summary>
    public static async Task<string> NewDatabaseAsync()
    {
        var database = NewDatabaseName();
        await ExecuteAsync("master", $"CREATE DATABASE [{database}]");
        return database;
    }

    public static string ConnectionString(string database) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = database }.ConnectionString;

    /// <summary>Start the API against <paramref name="database"/>. Start-up, schema work included, has finished when this returns.</summary>
    /// <param name="services">Services to put in place of the app's, such as a gate that holds two requests at the same point.</param>
    public static SqlServerApp Start(string database, string environment = "Testing", Action<IServiceCollection>? services = null)
    {
        var app = new SqlServerApp(ConnectionString(database), environment, services);
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

    /// <summary>
    /// Drops <paramref name="columns"/> from <paramref name="table"/>, with any default on
    /// them, as a database from before they were added has it. A default has a name of SQL
    /// Server's choosing on a database the migrations made and the old DDL's on one it made,
    /// so it is found by its column.
    /// </summary>
    public static Task DropColumnsAsync(string database, string table, params string[] columns)
    {
        var names = string.Join(", ", columns.Select(c => $"N'{c}'"));
        return ExecuteAsync(database, $@"
            DECLARE @drop nvarchar(max) = N'';
            SELECT @drop = @drop + N'ALTER TABLE [{table}] DROP CONSTRAINT ' + QUOTENAME(d.[name]) + N';'
            FROM sys.default_constraints d
            JOIN sys.columns c ON c.[object_id] = d.[parent_object_id] AND c.[column_id] = d.[parent_column_id]
            WHERE d.[parent_object_id] = OBJECT_ID(N'dbo.{table}') AND c.[name] IN ({names});
            EXEC sp_executesql @drop;
            ALTER TABLE [{table}] DROP COLUMN {string.Join(", ", columns.Select(c => $"[{c}]"))};");
    }

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
        builder.UseEnvironment(_environment);
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
            _services?.Invoke(services);
        });
    }
}
