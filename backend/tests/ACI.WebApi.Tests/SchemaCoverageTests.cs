using System.Reflection;
using ACI.Domain.Common;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi.Tests;

/// <summary>
/// Whether the schema this application ACTUALLY applies covers the model it maps.
/// </summary>
/// <remarks>
/// <para>
/// This application applies its schema two different ways, and a table only exists
/// in production if one of them creates it: EF's <c>MigrateAsync</c>, and the block
/// of hand-written idempotent DDL in <c>Program.cs</c>.
/// </para>
/// <para>
/// The trap is that EF discovers migrations by looking for the <c>[Migration]</c>
/// attribute, which lives in the generated <c>.Designer.cs</c> file — not in the
/// migration class itself. A migration added without its designer compiles, reads
/// correctly, sits in the Migrations folder, and is never run. Nothing anywhere
/// says so. Two migrations in this repository were in exactly that state:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>AddNotifications</c> — so the Notifications table did not exist at all, while
/// <c>AppDbContext</c> had always exposed <c>DbSet&lt;Notification&gt;</c>. Every
/// read of it failed on "Invalid object name".
/// </description></item>
/// <item><description>
/// <c>AlignLeadStatusVocabulary</c> — so established organisations kept the retired
/// status labels, the pipeline could not resolve a status against them, and leads
/// stayed on "New" however much work was logged against them.
/// </description></item>
/// </list>
/// <para>
/// Both are now applied from <c>Program.cs</c>. These tests are the tripwire for
/// the next one, and they are written against what EF's own discovery returns
/// rather than against a list of file names, so they cannot be satisfied by a
/// migration that merely exists.
/// </para>
/// </remarks>
public class SchemaCoverageTests
{
    /// <summary>
    /// Migrations that EF does NOT discover, whose effect is applied by the raw DDL
    /// in Program.cs instead. Deliberate, and listed here so that a migration
    /// becoming undiscovered by accident goes red rather than going silent.
    /// </summary>
    private static readonly string[] AppliedByProgramInstead =
    {
        "AddNotifications",
        "AlignLeadStatusVocabulary",
    };

    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;")
            .Options;
        return new AppDbContext(options);
    }

    private static string ProgramDdl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("the repository root should be findable from the test assembly");
        var path = Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs");
        File.Exists(path).Should().BeTrue($"expected Program.cs at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// The SQL MigrateAsync would run, from EF's own discovery — so an undiscovered
    /// migration is absent here exactly as it is absent in production. Generated
    /// offline; no database is contacted.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>Database.GenerateCreateScript()</c>. That builds DDL from
    /// the current model, so it contains a CREATE TABLE for every mapped entity
    /// whether or not anything creates it — which made the first version of this
    /// guard pass unconditionally, including for the very table whose absence
    /// prompted it. <see cref="IMigrator"/> goes through the migrations assembly,
    /// which is the same path MigrateAsync takes.
    /// </remarks>
    private static string DiscoveredMigrationSql()
    {
        using var db = NewContext();
        return db.GetService<IMigrator>().GenerateScript(
            fromMigration: Migration.InitialDatabase,
            toMigration: null,
            options: MigrationsSqlGenerationOptions.Default);
    }

    [Fact]
    public void EveryMigrationIsEitherDiscoveredByEfOrKnownToBeAppliedElsewhere()
    {
        using var db = NewContext();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys
            .Select(id => id.Length > 15 ? id[15..] : id) // strip the timestamp prefix
            .ToHashSet(StringComparer.Ordinal);

        var declared = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .Select(t => t.Name)
            .ToArray();

        declared.Should().NotBeEmpty("the Infrastructure assembly should contain migrations");

        var undiscovered = declared.Where(name => !discovered.Contains(name)).ToArray();

        undiscovered.Should().BeEquivalentTo(AppliedByProgramInstead,
            "a migration EF cannot see never runs, and nothing reports that. If a name has "
            + "appeared here, it is missing its .Designer.cs and is dead code: either generate "
            + "the designer, or apply its effect from the DDL in Program.cs and list it in "
            + "AppliedByProgramInstead.");
    }

    [Fact]
    public void EveryTableTheModelMapsIsCreatedBySomethingThatActuallyRuns()
    {
        using var db = NewContext();
        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        tables.Should().Contain("Notifications", "the model maps it, so it is in scope for this test");

        var migrationSql = DiscoveredMigrationSql();
        var ddl = ProgramDdl();

        var missing = tables.Where(table =>
                !migrationSql.Contains($"CREATE TABLE [{table}]", StringComparison.Ordinal)
                && !ddl.Contains($"CREATE TABLE [{table}]", StringComparison.Ordinal))
            .ToArray();

        missing.Should().BeEmpty(
            "a table the model maps but nothing creates fails every query against it at runtime, "
            + "with no sign of trouble at build or boot time");
    }

    [Fact]
    public void ProgramAppliesTheLeadStatusAlignment()
    {
        // Not a string search for the SQL — the point is that Program.cs calls the
        // one generator, so it cannot hold a stale copy of the vocabulary.
        ProgramDdl().Should().Contain("LeadStatusVocabulary.AlignmentSql()",
            "the alignment is what moves established organisations onto the agreed stages");
    }
}
