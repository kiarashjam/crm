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
    public static AppDbContext Open(SqliteConnection connection)
    {
        connection.Open();
        var db = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>The app's model, with SQL Server's nvarchar(max) as SQLite's TEXT. Nothing else differs.</summary>
    private sealed class SqliteAppDbContext : AppDbContext
    {
        public SqliteAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
                if (property.GetColumnType() == "nvarchar(max)") property.SetColumnType("TEXT");
        }
    }
}
