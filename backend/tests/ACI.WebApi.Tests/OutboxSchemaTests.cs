using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// The outbox tables, which only the hand-written DDL in Program.cs creates, and the
/// workspace's sending address, which reaches an existing Organizations table only by a
/// guarded ALTER.
/// </summary>
/// <remarks>
/// A column added to an entity and forgotten in the DDL compiles and passes every test on
/// the in-memory provider, then fails every query against the real database. The claim and
/// the cancel are conditional UPDATEs that only SQL Server runs; the in-memory provider
/// takes another path.
/// </remarks>
public class OutboxSchemaTests
{
    public static readonly TheoryData<string> Tables = new() { "OutboundMessages", "EmailSuppressions", "InboundEmails" };

    [Theory]
    [MemberData(nameof(Tables))]
    public void EachTableIsCreatedAsEfWouldWriteIt(string table)
    {
        var block = ClubSchemaTests.Between(ClubSchemaTests.ProgramDdl(), $"CREATE TABLE [{table}]", ");");
        var expected = ClubSchemaTests.Between(ClubSchemaTests.EfCreateScript(), $"CREATE TABLE [{table}]", "CONSTRAINT");

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
        columns.Should().BeGreaterThan(5, "the EF script should have been read");
    }

    [Fact]
    public void AnExistingOrganizationsTableIsGivenTheSendingAddressByAGuardedAlter()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf("IF COL_LENGTH('Organizations', 'SendingAddress') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, "production has Organizations without SendingAddress, and only an ALTER gives it one");
        var alter = ddl.IndexOf("ALTER TABLE [Organizations] ADD [SendingAddress] nvarchar(256) NULL", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");
    }

    [SqlServerFact]
    public async Task ANewDatabaseHasTheOutboxSchema()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database))
                await ShouldHaveTheOutboxSchemaAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnExistingDatabaseIsGivenTheOutboxSchemaOnStart()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }

            // As production is before this deploy. The messages' attachments (a statement's
            // PDF) came later, by a migration, and refer to the messages: they go first.
            await SqlServerApp.ExecuteAsync(database, "DROP TABLE [OutboundAttachments]");
            foreach (var table in new[] { "OutboundMessages", "EmailSuppressions", "InboundEmails" })
                await SqlServerApp.ExecuteAsync(database, $"DROP TABLE [{table}]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Organizations] DROP COLUMN [SendingAddress]");

            using (SqlServerApp.Start(database))
                await ShouldHaveTheOutboxSchemaAsync(database);

            // And a third start finds it all there already.
            using (SqlServerApp.Start(database))
                await ShouldHaveTheOutboxSchemaAsync(database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OnSqlServerExactlyOneInstanceClaimsAMessage_AndNobodyCancelsOneBeingSent()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            var org = Guid.NewGuid();
            // Each context is a request or job in the message's workspace; the dispatcher's
            // own reads (due, claim, by header) look across workspaces whatever it acts for.
            AppDbContext Db()
            {
                var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);
                db.ActFor(org, null);
                return db;
            }
            var now = DateTime.UtcNow;
            var id = Guid.NewGuid();
            await using (var db = Db())
            {
                db.OutboundMessages.Add(new OutboundMessage
                {
                    Id = id, OrganizationId = org, Channel = OutboundChannels.Email, Source = OutboundSources.Automation,
                    Status = OutboundStatuses.Queued, NextAttemptAtUtc = now.AddSeconds(-5), CreatedAtUtc = now, To = "jean@example.ch",
                    Subject = "Hello", MessageIdHeader = $"<{id:N}@pavillon46.ch>", ReplyToken = "abcdefghijklmnopqrstu",
                });
                await db.SaveChangesAsync();
            }

            await using var first = Db();
            await using var second = Db();
            var due = await new OutboxRepository(first).DueAsync(now, 50);
            due.Should().ContainSingle().Which.Should().Be(new ACI.Application.Interfaces.DueOutboundMessage(id, 0));
            var claims = await Task.WhenAll(
                new OutboxRepository(first).TryClaimAsync(id, 0, now, now.AddMinutes(5)),
                new OutboxRepository(second).TryClaimAsync(id, 0, now, now.AddMinutes(5)));
            claims.Count(c => c).Should().Be(1, "two instances that read the same row must not both send it");
            (await new OutboxRepository(Db()).DueAsync(now, 50)).Should().BeEmpty("a held message is nobody else's");
            (await new OutboxRepository(Db()).TryCancelAsync(id, now)).Should().BeFalse("it is being sent right now");
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Attempts] FROM [OutboundMessages] WHERE [Id] = '{id}'")).Should().Be(1);

            // Once the hold runs out, it can be cancelled; and it is found by its headers.
            (await new OutboxRepository(Db()).TryCancelAsync(id, now.AddMinutes(6))).Should().BeTrue();
            (await new OutboxRepository(Db()).FindByMessageIdAsync($"{id:N}@pavillon46.ch"))!.Id.Should().Be(id);
            (await new OutboxRepository(Db()).FindByReplyTokenAsync("ABCDEFGHIJKLMNOPQRSTU"))!.Id.Should().Be(id);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OnSqlServerAnAddressIsSuppressedOncePerWorkspace()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            var org = Guid.NewGuid();
            // The mail service's reports are acted on in the workspace that sent the mail.
            AppDbContext Db()
            {
                var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);
                db.ActFor(org, null);
                return db;
            }
            EmailSuppression Bounce() => new()
            {
                Id = Guid.NewGuid(), OrganizationId = org, Email = " Jean@Example.ch ", Reason = EmailSuppressionReasons.Bounced, CreatedAtUtc = DateTime.UtcNow,
            };

            (await new OutboxRepository(Db()).SuppressAsync(Bounce())).Should().BeTrue();
            (await new OutboxRepository(Db()).SuppressAsync(Bounce())).Should().BeFalse();
            (await new OutboxRepository(Db()).FindSuppressionAsync(org, ["JEAN@example.ch"]))!.Email.Should().Be("jean@example.ch");
            (await new OutboxRepository(Db()).FindSuppressionAsync(Guid.NewGuid(), ["jean@example.ch"])).Should().BeNull("another workspace's list");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OnSqlServerTheSameReplyIsTakenInOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options);
            var org = Guid.NewGuid();
            var message = Guid.NewGuid();
            InboundEmail Reply() => new()
            {
                Id = Guid.NewGuid(), OrganizationId = org, OutboundMessageId = message, MessageIdHeader = "<reply-1@mail.example.ch>",
                FromAddress = "jean@example.ch", MatchedBy = "token", ReceivedAtUtc = DateTime.UtcNow,
            };

            (await new OutboxRepository(Db()).TryAddInboundAsync(Reply())).Should().BeTrue();
            (await new OutboxRepository(Db()).TryAddInboundAsync(Reply())).Should().BeFalse("the unique index is what stops two posts at once");
            var otherWorkspace = Reply();
            otherWorkspace.OrganizationId = Guid.NewGuid();
            (await new OutboxRepository(Db()).TryAddInboundAsync(otherWorkspace)).Should().BeTrue("the same Message-ID in another workspace is another row");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task ShouldHaveTheOutboxSchemaAsync(string database)
    {
        foreach (var table in new[] { "OutboundMessages", "EmailSuppressions", "InboundEmails" })
            (await SqlServerApp.ScalarAsync(database, $"SELECT OBJECT_ID(N'dbo.{table}', N'U')")).Should().NotBeNull(table);
        (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH(N'dbo.Organizations', N'SendingAddress')")).Should().NotBeNull();
        foreach (var index in new[]
        {
            "IX_OutboundMessages_Status_NextAttemptAtUtc", "IX_OutboundMessages_OrganizationId_CreatedAtUtc", "IX_OutboundMessages_SourceId_Status",
            "IX_OutboundMessages_MessageIdHeader", "IX_OutboundMessages_ReplyToken", "IX_EmailSuppressions_OrganizationId_Email",
            "IX_InboundEmails_OrganizationId_MessageIdHeader", "IX_InboundEmails_OutboundMessageId",
        })
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM sys.indexes WHERE name = '{index}'")).Should().Be(1, index);
        (await SqlServerApp.ScalarAsync(database, "SELECT is_unique FROM sys.indexes WHERE name = 'IX_EmailSuppressions_OrganizationId_Email'")).Should().Be(true);
    }
}
