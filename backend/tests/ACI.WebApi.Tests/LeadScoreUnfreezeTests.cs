using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ACI.WebApi.Tests;

/// <summary>
/// Giving leads their automatic score back: the one-time unfreeze of the scores
/// #57 froze by accident, and the rescore that then keeps every automatic score
/// current.
/// </summary>
public class LeadScoreUnfreezeTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Org = Guid.NewGuid();

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Lead Lead(Action<Lead>? shape = null)
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), OrganizationId = Org, Name = "Jean", Email = "jean@example.ch",
            Status = "Attempted Contact", Phone = "+41790000000", CreatedAtUtc = Now.AddDays(-20),
            FirstResponseAtUtc = Now.AddDays(-19), UpdatedAtUtc = Now.AddDays(-10),
        };
        shape?.Invoke(lead);
        return lead;
    }

    private static async Task<int?> ScoreOf(AppDbContext db, Guid id) =>
        (await db.Leads.AsNoTracking().SingleAsync(l => l.Id == id)).LeadScore;

    [Fact]
    public async Task ScoresMoveWithTimeWithoutAnyoneSavingTheLead()
    {
        // Built-in rules: phone +10, contacted within 7 days +15. Scored 25 when it
        // was called ten days ago; the call no longer counts.
        await using var db = NewDb();
        var called = Lead(l => { l.LastContactedAt = Now.AddDays(-10); l.LeadScore = 25; });
        // Never answered, created five days ago: phone +10, stale -10.
        var untouched = Lead(l => { l.FirstResponseAtUtc = null; l.CreatedAtUtc = Now.AddDays(-5); l.LeadScore = 10; });
        db.Leads.AddRange(called, untouched);
        await db.SaveChangesAsync();

        var moved = await new LeadOpsRepository(db).RescoreOpenLeadsAsync(Now);

        moved.Should().Be(2);
        (await ScoreOf(db, called.Id)).Should().Be(10);
        (await ScoreOf(db, untouched.Id)).Should().Be(0);
        (await db.Leads.AsNoTracking().SingleAsync(l => l.Id == called.Id)).UpdatedAtUtc
            .Should().Be(Now.AddDays(-10), "a rescore is not an edit by anyone");
    }

    [Fact]
    public async Task HandSetAndConvertedLeadsKeepTheirScore()
    {
        await using var db = NewDb();
        var manual = Lead(l => { l.LeadScore = 90; l.ScoreIsManual = true; });
        var converted = Lead(l => { l.LeadScore = 70; l.IsConverted = true; });
        db.Leads.AddRange(manual, converted);
        await db.SaveChangesAsync();

        await new LeadOpsRepository(db).RescoreOpenLeadsAsync(Now);

        (await ScoreOf(db, manual.Id)).Should().Be(90);
        (await ScoreOf(db, converted.Id)).Should().Be(70);
    }

    [Fact]
    public async Task EachWorkspaceIsScoredByItsOwnRules()
    {
        await using var db = NewDb();
        var other = Guid.NewGuid();
        db.LeadScoringRules.Add(new LeadScoringRule { Id = Guid.NewGuid(), OrganizationId = Org, Field = "source", MatchValue = "Website", Points = 50, IsActive = true });
        var ours = Lead(l => l.Source = "Website");
        var theirs = Lead(l => { l.Source = "Website"; l.OrganizationId = other; });
        db.Leads.AddRange(ours, theirs);
        await db.SaveChangesAsync();

        await new LeadOpsRepository(db).RescoreOpenLeadsAsync(Now);

        (await ScoreOf(db, ours.Id)).Should().Be(50);
        (await ScoreOf(db, theirs.Id)).Should().Be(20, "no rules of its own: built-in website +10 and phone +10");
    }

    [Fact]
    public async Task EveryLeadIsReachedPastTheFirstPage()
    {
        await using var db = NewDb();
        db.Leads.AddRange(Enumerable.Range(0, 1203).Select(_ => Lead(l => l.LeadScore = 99)));
        await db.SaveChangesAsync();

        var moved = await new LeadOpsRepository(db).RescoreOpenLeadsAsync(Now);

        moved.Should().Be(1203);
        (await db.Leads.CountAsync(l => l.LeadScore == 99)).Should().Be(0);
    }

    // ── the one-time unfreeze ────────────────────────────────────────────────

    [Fact]
    public void TheUnfreezeIsValidTSql()
    {
        foreach (var sql in new[] { DataFixes.CreateTableSql, DataFixes.UnfreezeLeadScoresSql })
        {
            new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(sql), out var errors);
            errors.Should().BeEmpty(sql);
        }
    }

    [Fact]
    public void TheUnfreezeRunsOnceAndRecordsItselfInTheSameTransaction()
    {
        // Run on every start, it would undo every score typed in by hand since.
        var sql = DataFixes.UnfreezeLeadScoresSql;
        var guard = sql.IndexOf($"IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'{DataFixes.UnfreezeLeadScoresName}')", StringComparison.Ordinal);
        var update = sql.IndexOf("UPDATE [Leads] SET [ScoreIsManual] = 0", StringComparison.Ordinal);
        var record = sql.IndexOf($"INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'{DataFixes.UnfreezeLeadScoresName}'", StringComparison.Ordinal);
        var begin = sql.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal);
        var commit = sql.IndexOf("COMMIT TRANSACTION", StringComparison.Ordinal);

        new[] { begin, guard, update, record, commit }.Should().NotContain(-1);
        new[] { begin, guard, update, record, commit }.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ProgramRunsTheUnfreezeAtStartup()
    {
        var program = File.ReadAllText(ProgramPath());
        program.Should().Contain("ExecuteSqlRawAsync(ACI.WebApi.DataFixes.CreateTableSql)");
        program.Should().Contain("ExecuteSqlRawAsync(ACI.WebApi.DataFixes.UnfreezeLeadScoresSql)");
    }

    private static string ProgramPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs");
    }
}
