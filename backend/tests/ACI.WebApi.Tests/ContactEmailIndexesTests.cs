using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ACI.WebApi.Tests;

/// <summary>
/// The startup DDL that puts the per-workspace contact email indexes in place.
/// </summary>
/// <remarks>
/// A database whose InitialCreate was marked applied rather than run never had the old
/// unique index, so it can hold two contacts in one organization with the same email.
/// A plain CREATE UNIQUE INDEX fails there with a duplicate key, and it ran inside the
/// one catch around all the startup schema work, which then stopped on every start.
/// No SQL Server runs here, so these read the statement itself.
/// </remarks>
public class ContactEmailIndexesTests
{
    private static string Normal(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string Text(string sql, TSqlFragment fragment) => Normal(sql.Substring(fragment.StartOffset, fragment.FragmentLength));

    private sealed class Collect : TSqlFragmentVisitor
    {
        public readonly List<IfStatement> Ifs = new();
        public readonly List<CreateIndexStatement> Indexes = new();
        public readonly List<QuerySpecification> Queries = new();
        public override void Visit(IfStatement node) => Ifs.Add(node);
        public override void Visit(CreateIndexStatement node) => Indexes.Add(node);
        public override void Visit(QuerySpecification node) => Queries.Add(node);
    }

    private static (TSqlFragment Script, Collect Found) Parse(string sql)
    {
        var script = new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(sql), out var errors);
        errors.Should().BeEmpty(sql);
        var found = new Collect();
        script.Accept(found);
        return (script, found);
    }

    [Fact]
    public void ItIsValidTSql()
    {
        Parse(ContactEmailIndexes.Sql);
        Parse(ContactEmailIndexes.CountSql);
    }

    [Fact]
    public void EachUniqueIndexGoesInOnlyWhenNoActiveContactsShareItsKey()
    {
        var sql = ContactEmailIndexes.Sql;
        var (_, found) = Parse(sql);
        var unique = found.Indexes.Where(i => i.Unique).ToList();
        unique.Select(i => i.Name.Value).Should().BeEquivalentTo(ContactEmailIndexes.OrganizationIndex, ContactEmailIndexes.PersonalIndex);

        foreach (var index in unique)
        {
            var name = index.Name.Value;
            var guard = found.Ifs.SingleOrDefault(s => ReferenceEquals(s.ThenStatement, index));
            guard.Should().NotBeNull($"{name} is created under an IF");

            var inGuard = new Collect();
            guard!.Predicate.Accept(inGuard);
            var duplicates = inGuard.Queries.SingleOrDefault(q => q.HavingClause != null);
            duplicates.Should().NotBeNull($"{name} first checks that no rows would break it");

            var keyColumns = index.Columns.Select(c => c.Column.MultiPartIdentifier.Identifiers.Last().Value);
            var grouped = duplicates!.GroupByClause.GroupingSpecifications
                .Cast<ExpressionGroupingSpecification>()
                .Select(g => ((ColumnReferenceExpression)g.Expression).MultiPartIdentifier.Identifiers.Last().Value);
            grouped.Should().Equal(keyColumns, $"{name}'s check groups by its key");
            Text(sql, duplicates.WhereClause.SearchCondition).Should().Be(Text(sql, index.FilterPredicate),
                $"{name}'s check looks at the rows the index covers");
            Text(sql, duplicates.HavingClause.SearchCondition).Should().Be("COUNT(*) > 1");
        }
    }

    [Fact]
    public void TheOldIndexIsDroppedOnlyOnceBothNewOnesAreThere()
    {
        var sql = ContactEmailIndexes.Sql;
        var (_, found) = Parse(sql);
        var drop = found.Ifs.Single(s => s.ThenStatement is DropIndexStatement);
        var condition = Text(sql, drop.Predicate);
        condition.Should().Contain($"name = '{ContactEmailIndexes.OrganizationIndex}'")
            .And.Contain($"name = '{ContactEmailIndexes.PersonalIndex}'")
            .And.Contain("name = 'IX_Contacts_Email_OrganizationId'");
    }

    [Fact]
    public void TheDdlCreatesTheIndexesTheModelDeclares()
    {
        // The DDL is what puts them in production; the model is what the code believes.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;").Options);
        var declared = db.Model.FindEntityType(typeof(Contact))!.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Contact.Email)))
            .ToDictionary(i => i.GetDatabaseName()!, i => (Columns: i.Properties.Select(p => p.Name).ToList(), Filter: Normal(i.GetFilter() ?? "")));

        var sql = ContactEmailIndexes.Sql;
        var (_, found) = Parse(sql);
        var created = found.Indexes.Where(i => i.Unique).ToDictionary(
            i => i.Name.Value,
            i => (Columns: i.Columns.Select(c => c.Column.MultiPartIdentifier.Identifiers.Last().Value).ToList(), Filter: Text(sql, i.FilterPredicate)));

        created.Keys.Should().BeEquivalentTo(declared.Keys);
        foreach (var (name, model) in declared)
        {
            created[name].Columns.Should().Equal(model.Columns, name);
            created[name].Filter.Should().Be(model.Filter, name);
        }
    }

    [Fact]
    public void ProgramPutsThemInPlaceWithoutStoppingTheRestOfTheSchemaWork()
    {
        var program = File.ReadAllText(ProgramPath());
        program.Should().MatchRegex(
            @"try\s*\{\s*await db\.Database\.ExecuteSqlRawAsync\(ACI\.WebApi\.ContactEmailIndexes\.Sql\);[\s\S]*?\}\s*catch \(Exception indexEx\)",
            "a failure there is caught and logged on its own, in its own try");
    }

    private static string ProgramPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs");
    }
}
