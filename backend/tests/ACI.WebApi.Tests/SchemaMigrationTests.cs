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
    /// <summary>
    /// The upgrade production makes: a database that already took the welcome and email
    /// image migrations (they shipped first) then gets the club migrations, which are dated
    /// earlier and so run after them there, while a new database runs them before. Both made
    /// ClubMembers.ContactId, and a second plain ADD COLUMN failed the start-up.
    /// </summary>
    [SqlServerFact]
    public async Task ADatabaseThatShippedTheWelcomeMigrationsFirstTakesTheClubOnesAfter()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            string[] shippedFirst;
            await using (var db = SqlServerSchema.Context(database))
            {
                // As the deployed release left it: the baseline, then those two alone.
                await db.GetService<IMigrator>().MigrateAsync(SchemaBaseline.MigrationId);
                var assembly = db.GetService<IMigrationsAssembly>();
                var generator = db.GetService<IMigrationsSqlGenerator>();
                var history = db.GetService<IHistoryRepository>();
                shippedFirst = assembly.Migrations.Keys
                    .Where(id => id.EndsWith("_AddDealOnboarding", StringComparison.Ordinal) || id.EndsWith("_AddEmailImages", StringComparison.Ordinal))
                    .ToArray();
                shippedFirst.Should().HaveCount(2);
                foreach (var id in shippedFirst)
                {
                    var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
                    var model = migration.TargetModel!;
                    if (model is Microsoft.EntityFrameworkCore.Metadata.IMutableModel mutable) model = mutable.FinalizeModel();
                    model = db.GetService<IModelRuntimeInitializer>().Initialize(model, designTime: true, validationLogger: null);
                    foreach (var command in generator.Generate(migration.UpOperations, model))
                        await db.Database.ExecuteSqlRawAsync(command.CommandText.Replace("{", "{{").Replace("}", "}}"));
                    await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
                }
            }
            (await SqlServerSchema.HistoryAsync(database)).Should().HaveCount(3);

            // The release with the club migrations starts on it, and runs the ones it lacks.
            using (var app = SqlServerApp.Start(database))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(database)).Should().BeEquivalentTo(SqlServerSchema.AllMigrationIds());
            await using var check = SqlServerSchema.Context(database);
            (await SchemaBaseline.CompareAsync(check)).Missing.Should().BeEmpty("every table and column the model maps is there");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    /// <summary>
    /// The migrations after the baseline that the release before this one had, which production
    /// has run: main's, up to AddOnboardingSender, then AddLeadKinds (kiarashjam/crm#91).
    /// </summary>
    private static readonly string[] ReleaseBefore =
    [
        "AddMemberConsentRecords", "AddOutboundMessagePurpose", "AddClubMoneyLedger", "MoveCampaignsAndMessagesToTables",
        "MoveEventsAndReservationsToTables", "AddClubCatalogAndBilling", "AddClubPaymentsAndAccounting",
        "AddMembershipBridgeAndHouseholds", "RememberWhetherAnApplicantWasAMemberBefore",
        "AddDealOnboarding", "AddEmailImages", "AddOnboardingSender", "AddLeadKinds",
    ];

    /// <summary>
    /// The upgrade this release makes: a database with every migration of the release before
    /// (main's, ending with AddLeadKinds) takes every one this release adds. Four of them
    /// (AddWorkspaceModuleSwitches, AddClubSalesAndPosTickets, AddCampaignSendAttempts,
    /// AddAutomationRunsAndConditions) are dated before migrations it already has, so there
    /// they run after them, while a new database runs them before; the groups made them side by
    /// side and they met only in a merge. The database must end up as a new one is.
    /// </summary>
    [SqlServerFact]
    public async Task ADatabaseAtTheReleaseBeforeTakesThisReleasesMigrations_EvenThoseDatedBeforeOnesItHas()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        var fresh = await SqlServerApp.NewDatabaseAsync();
        try
        {
            string[] added;
            await using (var db = SqlServerSchema.Context(database))
            {
                var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
                var after = ids.Where(id => string.CompareOrdinal(id, SchemaBaseline.MigrationId) > 0).ToList();
                var shipped = after.Where(id => ReleaseBefore.Any(name => id.EndsWith("_" + name, StringComparison.Ordinal))).ToArray();
                shipped.Should().HaveCount(ReleaseBefore.Length, "every migration the release before had is in this one too, unchanged");
                added = after.Except(shipped).ToArray();
                added.Where(id => string.CompareOrdinal(id, shipped[^1]) < 0).Should().HaveCount(4,
                    "the release before had a migration dated after four of this release's");

                // As the release before left it: the baseline, then every later migration it had, in order.
                await db.GetService<IMigrator>().MigrateAsync(SchemaBaseline.MigrationId);
                foreach (var id in shipped)
                    await RunAloneAsync(db, id);
            }
            (await SqlServerSchema.HistoryAsync(database)).Should().NotContain(added);

            // This release starts on it, and runs the ones it lacks.
            using (var app = SqlServerApp.Start(database))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(database)).Should().Equal(SqlServerSchema.AllMigrationIds());
            await SqlServerSchema.MigrateAloneAsync(fresh);
            (await SqlServerSchema.ListAsync(database)).Where(line => !line.Contains(" AppliedDataFixes.", StringComparison.Ordinal))
                .Should().Equal(await SqlServerSchema.ListAsync(fresh), "the upgraded database is what a new one is, whatever order the migrations ran in");
            await using var check = SqlServerSchema.Context(database);
            var comparison = await SchemaBaseline.CompareAsync(check);
            comparison.Missing.Should().BeEmpty();
            comparison.Different.Should().BeEmpty();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
            await SqlServerApp.DropDatabaseAsync(fresh);
        }
    }

    /// <summary>Runs one migration's Up on its own and records it, as a release that had it would have.</summary>
    private static async Task RunAloneAsync(ACI.Infrastructure.Persistence.AppDbContext db, string id)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
        var model = migration.TargetModel!;
        if (model is Microsoft.EntityFrameworkCore.Metadata.IMutableModel mutable) model = mutable.FinalizeModel();
        model = db.GetService<IModelRuntimeInitializer>().Initialize(model, designTime: true, validationLogger: null);
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText.Replace("{", "{{").Replace("}", "}}"));
        await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
    }

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

            // While the start-up DDL runs, a refusal means it did not make what is missing, so
            // starting again on a release that runs it would only refuse again: the message
            // says how to add them.
            var record = () => SchemaBaseline.RecordOnExistingDatabaseAsync(db, NullLogger.Instance);
            (await record.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("This database was made before the baseline migration but lacks *"
                    + "added as the baseline migration defines it*dotnet ef migrations script 0 Baseline*");

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
    public async Task IndexesOfAnOldMigrationThatNeverRanThereAreMadeBeforeTheBaselineIsRecorded()
    {
        // The old start-up fallback only got past error 2714 (a table already there). Where the
        // start-up DDL had already added a column that a pending migration then added again
        // (2705), that migration and every one after it stopped there on every start; production
        // ran AddDealEnhancements by hand from scripts/. SpeedUpLeadAndActivityLookups came last,
        // and its three indexes are in the baseline but in no start-up DDL, so once the baseline
        // is recorded nothing would ever make them. A plain index changes no behaviour and is made
        // first; the foreign key it added is left to the log, as a key changes what a delete does.
        await SqlServerApp.WaitForServerAsync();
        var old = await SqlServerApp.NewDatabaseAsync();
        var fresh = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SqlServerSchema.CreateLegacyAsync(old);
            await SqlServerApp.ExecuteAsync(old, @"
                DROP INDEX [IX_Leads_OrganizationId_CreatedAtUtc] ON [Leads];
                DROP INDEX [IX_Leads_OrganizationId_Status] ON [Leads];
                DROP INDEX [IX_Activities_OrganizationId_LeadId] ON [Activities];
                ALTER TABLE [Leads] DROP CONSTRAINT [FK_Leads_Users_AssignedToUserId];
                DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261005124929_SpeedUpLeadAndActivityLookups';");

            using (var app = SqlServerApp.Start(old))
                await ShouldWorkAsync(app);

            (await SqlServerSchema.HistoryAsync(old)).Should().Contain(SchemaBaseline.MigrationId);
            await using (var db = SqlServerSchema.Context(old))
                (await SchemaBaseline.CompareAsync(db)).Different
                    .Should().Equal("Leads has no foreign key FK_Leads_Users_AssignedToUserId");

            // Made as the baseline makes them: the same columns, in the same order.
            await SqlServerSchema.MigrateAloneAsync(fresh);
            (await SqlServerSchema.ListAsync(fresh)).Except(await SqlServerSchema.ListAsync(old))
                .Should().ContainSingle().Which.Should().StartWith("FK Leads.FK_Leads_Users_AssignedToUserId ");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(old);
            await SqlServerApp.DropDatabaseAsync(fresh);
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
