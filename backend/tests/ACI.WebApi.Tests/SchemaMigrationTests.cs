using System.Net.Http.Json;
using System.Text.Json;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.WebApi.Tests;

/// <summary>
/// The schema by migration, on a real SQL Server: a new database is made by the migrations
/// alone, and one made the old way is recorded at the baseline without running it.
/// </summary>
/// <remarks>
/// A database made the old way is rebuilt here as it was made: the four migrations EF ran
/// before the baseline (Fixtures/LegacyMigrations.sql), then the API's start-up DDL, which
/// still runs before the baseline is recorded. Running the baseline there instead would
/// fail on its first CREATE TABLE, and recording it while a table or column was missing
/// would leave that missing for good.
/// </remarks>
public class SchemaMigrationTests
{
    [SqlServerFact]
    public async Task ANewDatabaseGetsItsSchemaFromTheMigrationsAlone()
    {
        await SqlServerApp.WaitForServerAsync();
        var started = await SqlServerApp.NewDatabaseAsync();
        var migrated = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (var app = SqlServerApp.Start(started))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(started)).Should().Equal(SqlServerSchema.AllMigrationIds(),
                "a new database runs every migration, the baseline first");

            // The start-up DDL that still runs after the migrations found everything in place
            // and added nothing: the schema is exactly what the migrations alone make. Only
            // the ledger of one-time data fixes is extra, which DataFixes creates for itself.
            await SqlServerSchema.MigrateAloneAsync(migrated);
            var schema = await SqlServerSchema.ListAsync(started);
            schema.Where(line => !line.Contains(" AppliedDataFixes.", StringComparison.Ordinal))
                .Should().Equal(await SqlServerSchema.ListAsync(migrated));

            await using var db = SqlServerSchema.Context(started);
            var comparison = await SchemaBaseline.CompareAsync(db);
            comparison.Missing.Should().BeEmpty();
            comparison.Different.Should().BeEmpty();

            // A second start leaves it as it was.
            using (var again = SqlServerApp.Start(started))
                await ShouldWorkAsync(again);
            (await SqlServerSchema.ListAsync(started)).Should().Equal(schema);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(started);
            await SqlServerApp.DropDatabaseAsync(migrated);
        }
    }

    [SqlServerFact]
    public async Task AFirstStartCutOffDuringTheBaselineIsFinishedByTheNextStart()
    {
        // MigrateAsync makes the history table first, outside the baseline's transaction. A
        // first start cut off part-way through the baseline (a slow start recycled by the
        // host, a dropped connection) rolls the baseline back and leaves that table behind.
        // The next start took the database for one made the old way, and the old DDL's
        // first statement, a foreign key to Users, failed on that start and every one after.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await using (var db = SqlServerSchema.Context(database))
                await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>().GetCreateScript());

            using (var app = SqlServerApp.Start(database))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(database)).Should().Equal(SqlServerSchema.AllMigrationIds());
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ADatabaseMadeTheOldWayIsRecordedAtTheBaselineWithoutRunningIt()
    {
        await SqlServerApp.WaitForServerAsync();
        var old = await SqlServerApp.NewDatabaseAsync();
        var fresh = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SqlServerSchema.CreateLegacyAsync(old);
            // Someone already works in it.
            var userId = Guid.NewGuid();
            var leadId = Guid.NewGuid();
            var email = $"{userId:N}@example.com";
            var hash = new BcryptPasswordHasher().Hash("correct horse battery staple");
            await SqlServerApp.ExecuteAsync(old, $@"
                INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc])
                VALUES ('{userId}', N'Ada Early', N'{email}', N'{hash}', 0, SYSUTCDATETIME());
                INSERT INTO [Leads] ([Id], [UserId], [Name], [Email], [Status], [IsConverted], [CreatedAtUtc])
                VALUES ('{leadId}', '{userId}', N'Lead From Before', N'before@example.com', N'New', 0, SYSUTCDATETIME());");

            using (var app = SqlServerApp.Start(old))
            {
                await ShouldWorkAsync(app);

                // Its records are all still there and still work: the person signs in and
                // finds the lead they had.
                var client = app.CreateClient();
                var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct horse battery staple" });
                var loginBody = await login.Content.ReadAsStringAsync();
                login.IsSuccessStatusCode.Should().BeTrue(loginBody);
                using var token = JsonDocument.Parse(loginBody);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", token.RootElement.GetProperty("token").GetString());
                var lead = await client.GetAsync($"/api/leads/{leadId}");
                var leadBody = await lead.Content.ReadAsStringAsync();
                lead.IsSuccessStatusCode.Should().BeTrue(leadBody);
                leadBody.Should().Contain("Lead From Before");
            }

            // Recorded, after the history it had, and not run: running it would have failed on
            // its first CREATE TABLE, and the start would have reported it.
            (await SqlServerSchema.HistoryAsync(old)).Should().Equal(
                SqlServerSchema.LegacyMigrationIds.Concat(SqlServerSchema.AllMigrationIds()));

            // It has everything a new database has, defined the same way: every column with
            // its type and nullability, every index and key under the same name, every
            // foreign key with the same delete rule. What it has besides (tables and columns
            // the model has dropped, the defaults the DDL gave columns it added) stays.
            await SqlServerSchema.MigrateAloneAsync(fresh);
            var schema = await SqlServerSchema.ListAsync(old);
            (await SqlServerSchema.ListAsync(fresh)).Except(schema).Should().BeEmpty();

            await using (var db = SqlServerSchema.Context(old))
            {
                var comparison = await SchemaBaseline.CompareAsync(db);
                comparison.Missing.Should().BeEmpty();
                comparison.Different.Should().BeEmpty();
            }

            // A second start leaves it as it was, and records nothing twice.
            using (var again = SqlServerApp.Start(old))
                await ShouldWorkAsync(again);
            (await SqlServerSchema.ListAsync(old)).Should().Equal(schema);
            (await SqlServerSchema.HistoryAsync(old)).Should().Equal(
                SqlServerSchema.LegacyMigrationIds.Concat(SqlServerSchema.AllMigrationIds()));
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(old);
            await SqlServerApp.DropDatabaseAsync(fresh);
        }
    }

    [SqlServerFact]
    public async Task AnOldDatabaseThatLacksPartOfTheSchemaIsNotRecorded()
    {
        // How a database that never ran the start-up DDL would arrive once that DDL is
        // deleted: recording it would leave its missing tables missing for good.
        await SqlServerApp.WaitForServerAsync();
        var old = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SqlServerSchema.CreateLegacyAsync(old);

            await using var db = SqlServerSchema.Context(old);
            (await SchemaBaseline.CompareAsync(db)).Missing
                .Should().Contain(new[] { "table Notifications", "column Leads.Version" });

            var record = () => SchemaBaseline.RecordOnExistingDatabaseAsync(db, NullLogger.Instance);
            (await record.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("This database was made before the baseline migration but lacks *");

            (await SqlServerSchema.HistoryAsync(old)).Should().Equal(SqlServerSchema.LegacyMigrationIds);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(old);
        }
    }

    [SqlServerFact]
    public async Task SequenceStepsWhoseSequenceIsGoneAreKeptAndTheirKeyLeftOut()
    {
        // The old DDL made OutreachSteps without its foreign key, so deleting a sequence left
        // its steps behind. The key goes in when the baseline is recorded, but not by deleting
        // those steps: where one exists, the key is left out and the start goes on.
        await SqlServerApp.WaitForServerAsync();
        var old = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SqlServerSchema.CreateLegacyAsync(old);
            var stepId = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(old, $@"
                CREATE TABLE [OutreachSequences] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [UserId] uniqueidentifier NOT NULL,
                    [OrganizationId] uniqueidentifier NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL
                );
                CREATE TABLE [OutreachSteps] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SequenceId] uniqueidentifier NOT NULL,
                    [SortOrder] int NOT NULL,
                    [Type] nvarchar(20) NOT NULL,
                    [DayOffset] int NOT NULL,
                    [Subject] nvarchar(200) NULL,
                    [Body] nvarchar(4000) NULL,
                    [TaskTitle] nvarchar(200) NULL,
                    [Note] nvarchar(1000) NULL
                );
                INSERT INTO [OutreachSteps] ([Id], [SequenceId], [SortOrder], [Type], [DayOffset])
                VALUES ('{stepId}', '{Guid.NewGuid()}', 0, N'email', 0);");

            using (var app = SqlServerApp.Start(old))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(old)).Should().Contain(SchemaBaseline.MigrationId);
            (await SqlServerApp.ScalarAsync(old, $"SELECT COUNT(*) FROM [OutreachSteps] WHERE [Id] = '{stepId}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(old, "SELECT OBJECT_ID(N'[dbo].[FK_OutreachSteps_OutreachSequences_SequenceId]', N'F')"))
                .Should().BeNull();

            await using var db = SqlServerSchema.Context(old);
            (await SchemaBaseline.CompareAsync(db)).Different
                .Should().Equal("OutreachSteps has no foreign key FK_OutreachSteps_OutreachSequences_SequenceId");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(old);
        }
    }

    [SqlServerFact]
    public async Task TwoInstancesStartingTogetherSetUpANewDatabaseOnce()
    {
        // A scale-out or a slot swap starts two at once. Both found the database empty and
        // both ran the migrations, and the second failed part-way through.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        var apps = new List<SqlServerApp>();
        try
        {
            var starts = Enumerable.Range(0, 2).Select(_ => Task.Run(() => SqlServerApp.Start(database))).ToArray();
            apps.AddRange(await Task.WhenAll(starts));
            foreach (var app in apps)
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(database)).Should().Equal(SqlServerSchema.AllMigrationIds());
        }
        finally
        {
            foreach (var app in apps) app.Dispose();
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldWorkAsync(SqlServerApp app)
    {
        var client = app.CreateClient();
        var status = await client.GetStringAsync("/db-status");
        using (var doc = JsonDocument.Parse(status))
            doc.RootElement.GetProperty("status").GetString().Should().Be("ok", status);

        var (signedIn, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var created = await signedIn.PostAsync("/api/leads", Workspaces.Body(new { name = "New Lead", email = $"{Guid.NewGuid():N}@example.com", status = "New" }));
        created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync());
    }
}
