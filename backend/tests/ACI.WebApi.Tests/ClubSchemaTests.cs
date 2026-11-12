using System.Text.RegularExpressions;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club tables were created by the hand-written DDL in Program.cs before the baseline
/// migration, and that DDL still brings an old database up to the baseline before the
/// baseline is recorded on it. So it must make every column the baseline makes; a column
/// added since comes from its own migration, on every database alike.
/// </summary>
/// <remarks>
/// A column added to the entity and forgotten in the DDL compiles, passes every test
/// on the in-memory provider, and fails every query against the real database. These
/// hold the two together, for new databases (the CREATE TABLE) and for existing ones
/// (the guarded ALTER for a column added after the table shipped).
/// </remarks>
public class ClubSchemaTests
{
    /// <summary>Columns added after their table shipped, which existing databases only get from an ALTER.</summary>
    public static readonly TheoryData<string, string> AddedLater = new()
    {
        { "ClubDocuments", "Version" },
        { "ClubMembers", "ManualBalance" },
    };

    [Fact]
    public void TheOpeningBalanceFixIsValidTSql()
    {
        new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(DataFixes.KeepClubOpeningBalancesSql), out var errors);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void TheOpeningBalanceFixRunsOnceAndRecordsItselfInTheSameTransaction()
    {
        // Run on every start, it would add each member's charges to their balance again.
        var sql = DataFixes.KeepClubOpeningBalancesSql;
        var name = DataFixes.KeepClubOpeningBalancesName;
        var marks = new[]
        {
            sql.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal),
            sql.IndexOf($"IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'{name}')", StringComparison.Ordinal),
            sql.IndexOf("UPDATE m SET [ManualBalance]", StringComparison.Ordinal),
            sql.IndexOf($"INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'{name}'", StringComparison.Ordinal),
            sql.IndexOf("COMMIT TRANSACTION", StringComparison.Ordinal),
        };

        marks.Should().NotContain(-1);
        marks.Should().BeInAscendingOrder();
        sql.Should().Contain("<> N'Voided'", "a voided charge owes nothing");
    }

    [Fact]
    public void ProgramFillsTheManualBalanceOnceTheColumnExists()
    {
        var ddl = ProgramDdl();
        var column = ddl.IndexOf("ALTER TABLE [ClubMembers] ADD [ManualBalance]", StringComparison.Ordinal);
        var fix = ddl.IndexOf("ExecuteSqlRawAsync(ACI.WebApi.DataFixes.KeepClubOpeningBalancesSql)", StringComparison.Ordinal);
        column.Should().BeGreaterThan(-1);
        fix.Should().BeGreaterThan(column, "the fix writes the column, so it runs after the DDL that adds it");
    }

    [Theory]
    [InlineData("ClubDocuments")]
    [InlineData("ClubMembers")]
    public void TheCreateTableDeclaresEveryColumnAsEfWouldWriteIt(string table)
    {
        var block = Between(ProgramDdl(), $"CREATE TABLE [{table}]", ");");
        var expected = Between(EfCreateScript(), $"CREATE TABLE [{table}]", "CONSTRAINT");

        var columns = 0;
        foreach (var line in expected.Split('\n'))
        {
            var m = Regex.Match(line.Trim(), @"^\[(?<col>\w+)\]\s+(?<type>[\w(),]+)\s+(?<null>NOT NULL|NULL)");
            if (!m.Success) continue;
            columns++;
            var column = m.Groups["col"].Value;
            var found = Regex.Match(block, $@"\[{Regex.Escape(column)}\]\s+(?<type>[\w(),]+)\s+(?<null>NOT NULL|NULL)");
            found.Success.Should().BeTrue($"the DDL must create {table}.{column}, which the model maps");
            found.Groups["type"].Value.Should().BeEquivalentTo(m.Groups["type"].Value, $"the DDL and the model disagree about the type of {table}.{column}");
            found.Groups["null"].Value.Should().Be(m.Groups["null"].Value, $"the DDL and the model disagree about whether {table}.{column} is nullable");
        }
        columns.Should().BeGreaterThan(3, "the EF script should have been read");
    }

    /// <summary>
    /// An old database is checked against what the baseline makes before the baseline is
    /// recorded on it. Today's model also has what later migrations add, which only those
    /// migrations give it, after: checking for that first refused every old database.
    /// </summary>
    [Fact]
    public void AnOldDatabaseIsCheckedAgainstTheBaselinesOwnModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;Trusted_Connection=True;")
            .Options;
        using var db = new AppDbContext(options);
        var baseline = ACI.WebApi.SchemaBaseline.BaselineModel(db).GetRelationalModel();
        var today = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel();

        baseline.Tables.Should().Contain(t => t.Name == "ClubMembers");
        baseline.Tables.Select(t => t.Name).Should().NotContain("DealOnboardings", "a later migration makes it");
        baseline.Tables.Single(t => t.Name == "ClubMembers").Columns.Select(c => c.Name).Should().NotContain("StartsOnUtc");
        today.Tables.Single(t => t.Name == "ClubMembers").Columns.Select(c => c.Name).Should().Contain("StartsOnUtc");
        baseline.Tables.Count().Should().BeGreaterThan(50, "the baseline makes the whole schema as it stood");
    }

    [Theory]
    [MemberData(nameof(AddedLater))]
    public void AColumnAddedLaterIsAddedToExistingDatabasesToo(string table, string column)
    {
        var ddl = ProgramDdl();
        var guard = ddl.IndexOf($"IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, $"an existing database has {table} without {column}, and only an ALTER gives it one");
        var alter = ddl.IndexOf($"ALTER TABLE [{table}] ADD [{column}]", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        // Guarded, so the next start-up does not fail on a column that is already there.
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");
    }

    internal static string ProgramDdl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the repository root should be findable from the test assembly");
        return File.ReadAllText(Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs"));
    }

    /// <summary>
    /// What EF itself writes for the schema as the baseline migration makes it, with no
    /// database involved.
    /// </summary>
    /// <remarks>
    /// The baseline's, not the model's as it is now: the start-up DDL stopped growing at the
    /// baseline and only brings an old database up to it, and a column a later migration adds
    /// is made by that migration. Compared with the current model, the first such column
    /// would have had to go into the DDL, which nothing may be added to any more.
    /// </remarks>
    internal static string EfCreateScript()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;Trusted_Connection=True;")
            .Options;
        using var db = new AppDbContext(options);
        return db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .GenerateScript(Microsoft.EntityFrameworkCore.Migrations.Migration.InitialDatabase, SchemaBaseline.MigrationId);
    }

    internal static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"expected to find '{start}'");
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        to.Should().BeGreaterThan(from, $"expected to find '{end}' after '{start}'");
        return text[from..to];
    }
}
