using ACI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The app's model on SQLite: a real relational database, with the foreign keys and
/// unique indexes the in-memory provider ignores, for tests that need them.
/// </summary>
public static class SqliteDb
{
    /// <summary>
    /// The app's context, filtered to a workspace like the app's own: it reads none until
    /// it acts for one (AppDbContext.ActFor), as a request or job does.
    /// </summary>
    /// <remarks>Several contexts can share one connection, as concurrent requests share one database.</remarks>
    public static AppDbContext Open(SqliteConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        var db = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>A context on the same database that reads every workspace, to see what was saved where.</summary>
    public static AppDbContext OpenEveryWorkspace(SqliteConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        var db = new SqliteEveryWorkspaceDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// SQL Server's nvarchar(max) as SQLite's TEXT, and its N'…' text in an index filter (the
    /// money ledger's "one void per entry") as plain '…'. Nothing else differs.
    /// </summary>
    private static void AsSqlite(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            if (property.GetColumnType() == "nvarchar(max)") property.SetColumnType("TEXT");
        foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetIndexes()))
            if (index.GetFilter() is { } filter && filter.Contains("N'", StringComparison.Ordinal))
                index.SetFilter(filter.Replace("N'", "'", StringComparison.Ordinal));
    }

    /// <summary>The app's model, on SQLite.</summary>
    private sealed class SqliteAppDbContext : AppDbContext
    {
        public SqliteAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            AsSqlite(modelBuilder);
        }
    }

    /// <summary>The app's model on SQLite, without its workspace filters.</summary>
    private sealed class SqliteEveryWorkspaceDbContext : AppDbContext
    {
        public SqliteEveryWorkspaceDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            AsSqlite(modelBuilder);
            foreach (var type in modelBuilder.Model.GetEntityTypes())
                type.SetQueryFilter(null);
        }
    }
}
