using System.Text.RegularExpressions;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club tables are created by the hand-written DDL in Program.cs, not by a
/// migration, so a column the model maps exists in production only if that DDL adds it.
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

    private static string ProgramDdl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the repository root should be findable from the test assembly");
        return File.ReadAllText(Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs"));
    }

    /// <summary>What EF itself would write for this model, with no database involved.</summary>
    private static string EfCreateScript()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;Trusted_Connection=True;")
            .Options;
        using var db = new AppDbContext(options);
        return db.Database.GenerateCreateScript();
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"expected to find '{start}'");
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        to.Should().BeGreaterThan(from, $"expected to find '{end}' after '{start}'");
        return text[from..to];
    }
}
