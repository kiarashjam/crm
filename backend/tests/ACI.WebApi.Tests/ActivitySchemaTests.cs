using ACI.WebApi.Tests.Helpers;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// When an activity happened, how it went, how long it took and who reached out: four
/// columns the Activities table, made by the first migration, only gets from the
/// hand-written DDL in Program.cs.
/// </summary>
/// <remarks>
/// Production has years of activities without them. Each arrives by a guarded ALTER, and
/// OccurredAtUtc, which the model requires, is filled from the time each activity was
/// logged, in the transaction that adds it, before it is made required.
/// </remarks>
public class ActivitySchemaTests
{
    public static readonly TheoryData<string, string> Columns = new()
    {
        { "Outcome", "nvarchar(32)" },
        { "DurationMinutes", "int" },
        { "Direction", "nvarchar(16)" },
    };

    private const string OccurredGuard = "IF COL_LENGTH('Activities', 'OccurredAtUtc') IS NULL";

    [Theory]
    [MemberData(nameof(Columns))]
    public void AnExistingTableIsGivenEachColumnByAGuardedAlter(string column, string type)
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        ddl.Should().Contain($"IF COL_LENGTH('Activities', '{column}') IS NULL ALTER TABLE [Activities] ADD [{column}] {type} NULL;");
    }

    [Fact]
    public void OccurredAtUtcIsFilledFromWhenEachActivityWasLoggedBeforeItIsRequired()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var start = ddl.IndexOf(OccurredGuard, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var marks = new[]
        {
            ddl.IndexOf("BEGIN TRANSACTION", start, StringComparison.Ordinal),
            ddl.IndexOf("ALTER TABLE [Activities] ADD [OccurredAtUtc] datetime2 NULL", start, StringComparison.Ordinal),
            ddl.IndexOf("UPDATE [Activities] SET [OccurredAtUtc] = [CreatedAtUtc];", start, StringComparison.Ordinal),
            ddl.IndexOf("ALTER TABLE [Activities] ALTER COLUMN [OccurredAtUtc] datetime2 NOT NULL", start, StringComparison.Ordinal),
            ddl.IndexOf("ADD CONSTRAINT [DF_Activities_OccurredAtUtc] DEFAULT SYSUTCDATETIME() FOR [OccurredAtUtc]", start, StringComparison.Ordinal),
            ddl.IndexOf("COMMIT TRANSACTION", start, StringComparison.Ordinal),
        };
        marks.Should().NotContain(-1);
        marks.Should().BeInAscendingOrder("the column is filled, then required, all or nothing");
    }

    [Fact]
    public void FillingOccurredAtUtcHasTimeAndCannotLeaveTheTableLocked()
    {
        // The fill touches every activity in one transaction. On the Basic database tier
        // that can outlast a command's default 30 seconds, and a timeout never reaches the
        // CATCH: the transaction stayed open on its pooled connection, holding the table.
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf(OccurredGuard, StringComparison.Ordinal);
        var call = ddl.LastIndexOf("ExecuteSqlRawAsync", guard, StringComparison.Ordinal);
        var callBefore = ddl.LastIndexOf("ExecuteSqlRawAsync", call - 1, StringComparison.Ordinal);

        var timeout = ddl.IndexOf("SetCommandTimeout(TimeSpan.FromMinutes(", callBefore, StringComparison.Ordinal);
        timeout.Should().BeInRange(callBefore, call, "the batch is given longer than the default before it runs");
        ddl[call..guard].Should().Contain("SET XACT_ABORT ON;", "a timeout then rolls the transaction back");
    }

    [Fact]
    public void TheBatchThatAddsOccurredAtUtcIsValidTSql()
    {
        var batch = ClubSchemaTests.Between(ClubSchemaTests.ProgramDdl(), OccurredGuard, "\");");
        new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(batch), out var errors);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void TheIndexOnWhenThingsHappenedComesInABatchAfterTheColumn()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var column = ddl.IndexOf(OccurredGuard, StringComparison.Ordinal);
        var index = ddl.IndexOf("CREATE INDEX [IX_Activities_OrganizationId_OccurredAtUtc] ON [Activities] ([OrganizationId], [OccurredAtUtc])", StringComparison.Ordinal);
        index.Should().BeGreaterThan(column);
        ddl[column..index].Should().Contain("ExecuteSqlRawAsync", "an index on a column made in the same batch is compiled before the column exists");
    }

    [SqlServerFact]
    public async Task ANewDatabaseHasTheActivityColumns()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database))
                await ShouldHaveTheColumnsAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnExistingDatabaseIsGivenThemAndItsActivitiesHappenedWhenTheyWereLogged()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid userId;
            using (var app = SqlServerApp.Start(database))
                (_, userId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

            // As production is before this deploy.
            await SqlServerApp.ExecuteAsync(database, "DROP INDEX [IX_Activities_OrganizationId_OccurredAtUtc] ON [Activities]");
            await SqlServerApp.DropColumnsAsync(database, "Activities", "OccurredAtUtc", "Outcome", "DurationMinutes", "Direction");
            var old = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Activities] ([Id], [UserId], [Type], [Subject], [CreatedAtUtc]) VALUES ('{old}', '{userId}', N'call', N'Called in January', '2026-01-15T10:30:00')");

            using (SqlServerApp.Start(database))
            {
                await ShouldHaveTheColumnsAsync(database);
                (await SqlServerApp.ScalarAsync(database, $"SELECT [OccurredAtUtc] FROM [Activities] WHERE [Id] = '{old}'"))
                    .Should().Be(new DateTime(2026, 1, 15, 10, 30, 0));
            }

            // An instance still running the previous code writes activities without the
            // column during the deploy; they are not refused.
            var during = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Activities] ([Id], [UserId], [Type], [CreatedAtUtc]) VALUES ('{during}', '{userId}', N'note', SYSUTCDATETIME())");
            (await SqlServerApp.ScalarAsync(database, $"SELECT [OccurredAtUtc] FROM [Activities] WHERE [Id] = '{during}'")).Should().NotBeNull();

            // And a third start finds it all there already.
            using (SqlServerApp.Start(database))
                await ShouldHaveTheColumnsAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnActivityWithItsFactsIsSavedAndReadBackOnSqlServer()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var lead = await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" }));
            lead.IsSuccessStatusCode.Should().BeTrue(await lead.Content.ReadAsStringAsync());
            using var leadJson = System.Text.Json.JsonDocument.Parse(await lead.Content.ReadAsStringAsync());
            var leadId = leadJson.RootElement.GetProperty("id").GetGuid();
            var made = DateTime.UtcNow.AddHours(-2);

            var res = await client.PostAsync("/api/activities", Body(new
            {
                type = "call", subject = "Called", leadId, occurredAtUtc = made, outcome = "left_voicemail", direction = "outbound", durationMinutes = 1,
            }));
            res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

            var timeline = await client.GetAsync($"/api/activities/lead/{leadId}");
            using var json = System.Text.Json.JsonDocument.Parse(await timeline.Content.ReadAsStringAsync());
            var call = json.RootElement.EnumerateArray().Single();
            call.GetProperty("outcome").GetString().Should().Be("left_voicemail");
            call.GetProperty("direction").GetString().Should().Be("outbound");
            call.GetProperty("durationMinutes").GetInt32().Should().Be(1);
            call.GetProperty("occurredAtUtc").GetDateTime().Should().BeCloseTo(made, TimeSpan.FromSeconds(1));
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldHaveTheColumnsAsync(string database)
    {
        (await SqlServerApp.ScalarAsync(database,
            "SELECT is_nullable FROM sys.columns WHERE object_id = OBJECT_ID('Activities') AND name = 'OccurredAtUtc'"))
            .Should().Be(false, "the model requires it");
        foreach (var column in new[] { "Outcome", "DurationMinutes", "Direction" })
            (await SqlServerApp.ScalarAsync(database, $"SELECT COL_LENGTH(N'dbo.Activities', N'{column}')")).Should().NotBeNull(column);
        (await SqlServerApp.ScalarAsync(database,
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('Activities') AND name = 'IX_Activities_OrganizationId_OccurredAtUtc'"))
            .Should().Be(1);
    }
}
