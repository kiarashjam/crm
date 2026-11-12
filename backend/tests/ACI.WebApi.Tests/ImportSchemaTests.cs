using System.Text.RegularExpressions;
using ACI.WebApi.Tests.Helpers;

namespace ACI.WebApi.Tests;

/// <summary>
/// The import tables and columns, which only the hand-written DDL in Program.cs creates.
/// </summary>
/// <remarks>
/// A column added to an entity and forgotten in the DDL compiles and passes every test on
/// the in-memory provider, then fails every query against the real database. Leads,
/// Contacts and Companies already exist in production, so their batch id arrives only by
/// a guarded ALTER; ImportBatches is new everywhere.
/// </remarks>
public class ImportSchemaTests
{
    public static readonly TheoryData<string> Tables = new() { "Leads", "Contacts", "Companies" };

    [Fact]
    public void TheImportBatchesTableIsCreatedAsEfWouldWriteIt()
    {
        var block = ClubSchemaTests.Between(ClubSchemaTests.ProgramDdl(), "CREATE TABLE [ImportBatches]", ");");
        var expected = ClubSchemaTests.Between(ClubSchemaTests.EfCreateScript(), "CREATE TABLE [ImportBatches]", "CONSTRAINT");

        var columns = 0;
        foreach (var line in expected.Split('\n'))
        {
            var m = Regex.Match(line.Trim(), @"^\[(?<col>\w+)\]\s+(?<type>[\w(),]+)\s+(?<null>NOT NULL|NULL)");
            if (!m.Success) continue;
            columns++;
            var column = m.Groups["col"].Value;
            var found = Regex.Match(block, $@"\[{Regex.Escape(column)}\]\s+(?<type>[\w(),]+)\s+(?<null>NOT NULL|NULL)");
            found.Success.Should().BeTrue($"the DDL must create ImportBatches.{column}, which the model maps");
            found.Groups["type"].Value.Should().BeEquivalentTo(m.Groups["type"].Value, $"the DDL and the model disagree about the type of ImportBatches.{column}");
            found.Groups["null"].Value.Should().Be(m.Groups["null"].Value, $"the DDL and the model disagree about whether ImportBatches.{column} is nullable");
        }
        columns.Should().BeGreaterThan(10, "the EF script should have been read");
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void AnExistingTableIsGivenTheBatchIdByAGuardedAlter(string table)
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf($"IF COL_LENGTH(N'dbo.{table}', N'ImportBatchId') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, $"an existing database has {table} without ImportBatchId, and only an ALTER gives it one");
        var alter = ddl.IndexOf($"ALTER TABLE [{table}] ADD [ImportBatchId] uniqueidentifier NULL", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");

        // The tables come from the migrations, so the general index check skips them.
        ddl.Should().Contain($"CREATE INDEX [IX_{table}_ImportBatchId] ON [{table}] ([ImportBatchId])");
    }

    [Fact]
    public void ABatchTableMadeBeforeLastSavedAtUtcIsGivenItByAGuardedAlter()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf("IF COL_LENGTH(N'dbo.ImportBatches', N'LastSavedAtUtc') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, "a database made with the first version of the table has no LastSavedAtUtc, and only an ALTER gives it one");
        var alter = ddl.IndexOf("ALTER TABLE [ImportBatches] ADD [LastSavedAtUtc] datetime2 NULL", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");
    }

    [SqlServerFact]
    public async Task ABatchTableMadeBeforeLastSavedAtUtcIsGivenItOnStart()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [ImportBatches] DROP COLUMN [LastSavedAtUtc]");

            using (SqlServerApp.Start(database))
                (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH(N'dbo.ImportBatches', N'LastSavedAtUtc')")).Should().NotBeNull();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ANewDatabaseHasTheImportSchema()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database))
                await ShouldHaveTheImportSchemaAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnExistingDatabaseIsGivenTheImportSchemaOnStart()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }

            // As production is before this deploy.
            foreach (var table in new[] { "Leads", "Contacts", "Companies" })
            {
                await SqlServerApp.ExecuteAsync(database, $"DROP INDEX [IX_{table}_ImportBatchId] ON [{table}]");
                await SqlServerApp.ExecuteAsync(database, $"ALTER TABLE [{table}] DROP COLUMN [ImportBatchId]");
            }
            await SqlServerApp.ExecuteAsync(database, "DROP TABLE [ImportBatches]");

            using (SqlServerApp.Start(database))
                await ShouldHaveTheImportSchemaAsync(database);

            // And a third start finds it all there already.
            using (SqlServerApp.Start(database))
                await ShouldHaveTheImportSchemaAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldHaveTheImportSchemaAsync(string database)
    {
        (await SqlServerApp.ScalarAsync(database, "SELECT OBJECT_ID(N'dbo.ImportBatches', N'U')")).Should().NotBeNull();
        (await SqlServerApp.ScalarAsync(database,
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('ImportBatches') AND name = 'IX_ImportBatches_OrganizationId_StartedAtUtc'"))
            .Should().Be(1);
        foreach (var table in new[] { "Leads", "Contacts", "Companies" })
        {
            (await SqlServerApp.ScalarAsync(database, $"SELECT COL_LENGTH(N'dbo.{table}', N'ImportBatchId')")).Should().NotBeNull(table);
            (await SqlServerApp.ScalarAsync(database,
                $"SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('{table}') AND name = 'IX_{table}_ImportBatchId'"))
                .Should().Be(1, table);
        }
    }
}
