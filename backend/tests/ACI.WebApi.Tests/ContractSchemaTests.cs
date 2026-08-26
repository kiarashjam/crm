using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ACI.WebApi.Tests;

/// <summary>
/// The contract schema, in both of the places it is written down.
/// </summary>
/// <remarks>
/// <para>
/// Two risks, and neither shows up in a unit test of the service:
/// </para>
/// <para>
/// A malformed <c>IEntityTypeConfiguration</c> does not fail at compile time — it
/// throws when the model is first built, which is on the first request of the
/// process. So a mistake in <c>ContractConfiguration</c> would take out EVERY
/// endpoint, not just the contract ones. Building the model here catches that
/// offline, without a database.
/// </para>
/// <para>
/// And the schema now exists twice: in that configuration, and in the raw
/// idempotent DDL in <c>Program.cs</c> that actually creates the table (following
/// the convention <c>Leads.PipelineState</c> shipped under). Two descriptions of
/// one table is precisely the drift this codebase has been bitten by before, so
/// these tests read the DDL and hold the two together.
/// </para>
/// </remarks>
public class ContractSchemaTests
{
    /// <summary>
    /// Builds the real model with the real provider. No connection is opened —
    /// model building is entirely offline — so this needs no database.
    /// </summary>
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=not-connected;Database=none;")
            .Options;
        using var db = new AppDbContext(options);
        return db.Model;
    }

    /// <summary>The DDL block from Program.cs, which is what really creates the table.</summary>
    private static string ProgramDdl()
    {
        // Walk up from the test assembly to the repo, so this works from any
        // working directory the runner happens to pick.
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
    public void TheModelBuilds_SoAMalformedConfigurationCannotTakeDownEveryEndpoint()
    {
        var model = BuildModel();
        model.Should().NotBeNull();
        model.FindEntityType(typeof(Contract)).Should().NotBeNull();
        model.FindEntityType(typeof(ContractEvent)).Should().NotBeNull();
    }

    [Fact]
    public void ContractsMapToTheTableTheDdlCreates()
    {
        var entity = BuildModel().FindEntityType(typeof(Contract))!;
        entity.GetTableName().Should().Be("Contracts");

        var events = BuildModel().FindEntityType(typeof(ContractEvent))!;
        events.GetTableName().Should().Be("ContractEvents");
    }

    [Fact]
    public void TheSigningTokenIndexIsUniqueAndFiltered()
    {
        // The public signing path looks up by this column alone, so it must be
        // indexed; unique so two live contracts cannot share a link; filtered
        // because the many NULLs (drafts, voided) would otherwise collide.
        var entity = BuildModel().FindEntityType(typeof(Contract))!;
        var index = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Count == 1
                && i.Properties[0].Name == nameof(Contract.SigningTokenHash));

        index.Should().NotBeNull("the signing token needs its own index");
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().NotBeNullOrEmpty("unfiltered, the NULLs would collide");
    }

    [Fact]
    public void EveryContractPropertyTheServiceWritesIsMapped()
    {
        // A property the configuration forgot would be silently dropped on save —
        // a signature that appears to be recorded and is not.
        var entity = BuildModel().FindEntityType(typeof(Contract))!;
        var mapped = entity.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
        {
            nameof(Contract.Status), nameof(Contract.Title), nameof(Contract.Body),
            nameof(Contract.BodyHashAtSend), nameof(Contract.SigningTokenHash),
            nameof(Contract.SigningTokenExpiresAtUtc), nameof(Contract.FirstViewedAtUtc),
            nameof(Contract.ClientSignatureName), nameof(Contract.ClientSignedAtUtc),
            nameof(Contract.ClientSignatureIp), nameof(Contract.ClientSignatureUserAgent),
            nameof(Contract.CounterSignatureName), nameof(Contract.CounterSignedAtUtc),
            nameof(Contract.CounterSignedByUserId), nameof(Contract.CounterSignatureIp),
            nameof(Contract.ClosedReason), nameof(Contract.ExecutedCopySentAtUtc),
            nameof(Contract.OrganizationId), nameof(Contract.LeadId), nameof(Contract.DealId),
        })
        {
            mapped.Should().Contain(required);
        }
    }

    [Fact]
    public void TheDdlCreatesEveryColumnTheModelMaps()
    {
        // The drift guard. The model and the raw DDL are two descriptions of one
        // table; a column added to the entity but not to the DDL would work
        // locally against a scaffolded database and fail in production, where the
        // DDL is the only thing that ever runs.
        var ddl = ProgramDdl();
        var contractsBlock = Between(ddl, "CREATE TABLE [Contracts]", ");");
        var entity = BuildModel().FindEntityType(typeof(Contract))!;

        foreach (var column in entity.GetProperties().Select(p => p.GetColumnName()))
        {
            contractsBlock.Should().Contain($"[{column}]",
                $"the DDL in Program.cs must create the column the model maps: {column}");
        }
    }

    [Fact]
    public void TheDdlCreatesEveryContractEventColumnTheModelMaps()
    {
        var ddl = ProgramDdl();
        var block = Between(ddl, "CREATE TABLE [ContractEvents]", ");");
        var entity = BuildModel().FindEntityType(typeof(ContractEvent))!;

        foreach (var column in entity.GetProperties().Select(p => p.GetColumnName()))
        {
            block.Should().Contain($"[{column}]",
                $"the DDL must create the column the model maps: {column}");
        }
    }

    [Fact]
    public void TheDdlMaxLengthsAgreeWithTheModel()
    {
        // A column narrower in the DDL than in the model truncates or throws on
        // real data while every local test passes.
        var block = Between(ProgramDdl(), "CREATE TABLE [Contracts]", ");");
        var entity = BuildModel().FindEntityType(typeof(Contract))!;

        foreach (var property in entity.GetProperties())
        {
            var declared = property.GetMaxLength();
            if (declared is null) continue; // nvarchar(max) or not a string

            var column = property.GetColumnName();
            var match = Regex.Match(block, $@"\[{Regex.Escape(column)}\]\s+nvarchar\((\d+|max)\)");
            match.Success.Should().BeTrue($"expected an nvarchar declaration for {column} in the DDL");
            if (match.Groups[1].Value == "max") continue;

            int.Parse(match.Groups[1].Value).Should().Be(declared.Value,
                $"the DDL and the model disagree about the width of {column}");
        }
    }

    [Fact]
    public void TheDdlIsIdempotent_SoRestartsDoNotFail()
    {
        // It runs on every start-up, not once. Without the guards the second boot
        // of any instance would throw and take the API down.
        var ddl = ProgramDdl();
        var block = Between(ddl, "// Contracts and their audit trail.", "IX_ContractEvents_ContractId_AtUtc");

        block.Should().Contain("IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Contracts')");
        block.Should().Contain("IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ContractEvents')");
    }

    [Fact]
    public void TheDdlColumnTypesAndNullabilityAgreeWithWhatEfWouldWrite()
    {
        // Stronger than checking the column exists, and it earned its place: the
        // hand-written DDL declared [RowVersion] NOT NULL while the model maps a
        // nullable byte[], so EF would have generated `rowversion NULL`. The
        // name-only check passed happily.
        //
        // Compared against EF's OWN generated script rather than against a list
        // maintained here, so a future column cannot be added to one and forgotten
        // in the other.
        var block = Between(ProgramDdl(), "CREATE TABLE [Contracts]", ");");
        var expected = Between(EfCreateScript(), "CREATE TABLE [Contracts]", "CONSTRAINT");

        foreach (var line in expected.Split('\n'))
        {
            var m = Regex.Match(line.Trim(), @"^\[(?<col>\w+)\]\s+(?<type>[\w()]+(?:\s*\(\s*\w+\s*\))?)\s+(?<null>NOT NULL|NULL)");
            if (!m.Success) continue;

            var column = m.Groups["col"].Value;
            var type = m.Groups["type"].Value;
            var nullability = m.Groups["null"].Value;

            // The DDL writes nvarchar(max) where EF writes it too; only compare the
            // type token and the nullability, not whitespace or ordering.
            var found = Regex.Match(block, $@"\[{Regex.Escape(column)}\]\s+(?<type>[\w()]+)\s+(?<null>NOT NULL|NULL)");
            found.Success.Should().BeTrue($"the DDL must declare {column} with a type and nullability");
            found.Groups["type"].Value.Should().BeEquivalentTo(type,
                $"the DDL and the model disagree about the type of {column}");
            found.Groups["null"].Value.Should().Be(nullability,
                $"the DDL and the model disagree about whether {column} is nullable");
        }
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

    /// <summary>Text between the first <paramref name="start"/> and the next <paramref name="end"/>.</summary>
    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"expected to find '{start}' in Program.cs");
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        to.Should().BeGreaterThan(from, $"expected to find '{end}' after '{start}'");
        return text[from..to];
    }
}
