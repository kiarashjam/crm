using System.Reflection;
using ACI.Domain.Common;
using ACI.Infrastructure.Migrations;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ACI.WebApi.Tests;

/// <summary>
/// Whether the migrations, the schema's only source since the baseline, say what the model says.
/// </summary>
/// <remarks>
/// <para>
/// Until the baseline migration, a database got its schema from whichever migrations EF
/// found and from hand-written DDL in Program.cs. EF finds a migration by the
/// <c>[Migration]</c> attribute in its <c>.Designer.cs</c>, so two migrations that had
/// none (AddNotifications and AlignLeadStatusVocabulary) compiled, sat in the folder and
/// never ran; and a column added to the model with DDL but no migration existed only
/// where that DDL had run. Now the migrations make the whole schema (SchemaMigrationTests
/// proves it on SQL Server), so what is checked here is that they cannot fall behind:
/// none is left undiscovered, and the model has no change that no migration makes.
/// </para>
/// <para>
/// Everything here is worked out offline from EF's own services; no database is contacted.
/// </para>
/// </remarks>
public class SchemaCoverageTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;")
            .Options;
        return new AppDbContext(options);
    }

    private static string ProgramSource()
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

    [Fact]
    public void EveryMigrationIsDiscoveredByEf()
    {
        using var db = NewContext();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Values
            .Select(type => type.AsType())
            .ToHashSet();

        var declared = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .ToArray();

        declared.Should().NotBeEmpty("the Infrastructure assembly holds the migrations");
        declared.Where(t => !discovered.Contains(t)).Select(t => t.Name).Should().BeEmpty(
            "a migration EF cannot see never runs, and nothing reports it. One listed here is missing its "
            + ".Designer.cs (the [Migration] and [DbContext] attributes): add it with `dotnet ef migrations add`, "
            + "not by hand");
    }

    [Fact]
    public void TheBaselineIsTheFirstMigration()
    {
        // A database made before the baseline is recorded at it (SchemaBaseline), so anything
        // ordered before it would never run there.
        using var db = NewContext();
        db.GetService<IMigrationsAssembly>().Migrations.Keys.First().Should().Be(SchemaBaseline.MigrationId);
        SchemaBaseline.MigrationId.Should().EndWith("_Baseline");
    }

    [Fact]
    public void TheModelHasNoChangeThatNoMigrationMakes()
    {
        // What `dotnet ef migrations add` would put in a new migration right now: the
        // difference between the model and the snapshot the last migration left. Anything
        // here is a change that reaches no database, because the start-up DDL no longer
        // adds to the schema.
        using var db = NewContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        snapshot.Should().NotBeNull("the migrations folder holds the model snapshot");
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot!, designTime: true, validationLogger: null);

        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        differences.Select(Describe).Should().BeEmpty(
            "every change to the model needs a migration: from backend/, run "
            + "dotnet ef migrations add <Name> -p src/ACI.Infrastructure -s src/ACI.Infrastructure -o Migrations");
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        AddColumnOperation c => $"add column {c.Table}.{c.Name}",
        DropColumnOperation c => $"drop column {c.Table}.{c.Name}",
        AlterColumnOperation c => $"alter column {c.Table}.{c.Name}",
        CreateTableOperation t => $"create table {t.Name}",
        DropTableOperation t => $"drop table {t.Name}",
        CreateIndexOperation i => $"create index {i.Name} on {i.Table}",
        DropIndexOperation i => $"drop index {i.Name} on {i.Table}",
        AddForeignKeyOperation f => $"add foreign key {f.Name} on {f.Table}",
        DropForeignKeyOperation f => $"drop foreign key {f.Name} on {f.Table}",
        _ => operation.GetType().Name,
    };

    [Fact]
    public void EveryTableTheModelMapsIsCreatedByTheMigrations()
    {
        // The SQL MigrateAsync would run on an empty database, from EF's own discovery: an
        // undiscovered migration is missing here exactly as it would be in production.
        // Deliberately not Database.GenerateCreateScript(), which builds DDL from the
        // current model and so contains every table whether or not a migration makes it.
        using var db = NewContext();
        var migrationSql = db.GetService<IMigrator>().GenerateScript(
            fromMigration: Migration.InitialDatabase,
            toMigration: null,
            options: MigrationsSqlGenerationOptions.Default);

        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        tables.Should().Contain("Notifications", "the model maps it, so it is in scope for this test");

        tables.Where(table => !migrationSql.Contains($"CREATE TABLE [{table}]", StringComparison.Ordinal))
            .Should().BeEmpty("a table the model maps but no migration creates fails every query against it");
    }

    [Fact]
    public void TheBaselineRefusesToBeRolledBack()
    {
        // Its generated Down dropped every table. On a database recorded at the baseline
        // rather than made by it, those are the tables people's records are in.
        var rollBack = () => new Baseline().DownOperations;
        rollBack.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ProgramAppliesTheLeadStatusAlignment()
    {
        // Not a string search for the SQL — the point is that Program.cs runs the one
        // generator (once, through DataFixes), so it cannot hold a stale copy of the vocabulary.
        ProgramSource().Should().Contain("DataFixes.AlignLeadStatusVocabularySql",
            "the alignment is what moves established organisations onto the agreed stages");
        DataFixes.AlignLeadStatusVocabularySql.Should().Contain(LeadStatusVocabulary.AlignmentSql());
    }
}
