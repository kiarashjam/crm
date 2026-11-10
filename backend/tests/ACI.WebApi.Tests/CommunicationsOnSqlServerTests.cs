using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Campaigns, member messages and consent on a real SQL Server: the move of the old
/// documents on the migration path an existing database takes, and the conditional
/// updates and unique indexes that make sending happen once.
/// </summary>
/// <remarks>
/// The in-memory provider runs no SQL, enforces no unique index and has no conditional
/// UPDATE, so only a real server shows that a campaign is claimed once and a member is
/// emailed once by it.
/// </remarks>
public class CommunicationsOnSqlServerTests
{
    /// <summary>The migration before the one that moves the documents: where an existing database stands before this release.</summary>
    private const string ReleaseBefore = "20261110062154_AddOutboundMessagePurpose";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string Doc(Guid id, Guid? org, string kind, string payload) =>
        $"INSERT INTO [ClubDocuments] ([Id], [OrganizationId], [Kind], [Payload], [UpdatedAtUtc], [Version]) VALUES ('{id}', {(org == null ? "NULL" : $"'{org}'")}, N'{kind}', N'{payload.Replace("'", "''")}', '2026-10-20T08:00:00', NEWID());";

    [SqlServerFact]
    public async Task TheCampaignAndCommunicationDocumentsAreCopiedOnceAndKept()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            // A database as the release before this one leaves it, already holding documents.
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(ReleaseBefore);
            var org = Guid.NewGuid();
            var member = Guid.NewGuid();
            var (logged, planned, texted, broken, homeless) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var (note, noMember) = (Guid.NewGuid(), Guid.NewGuid());
            var longBody = new string('x', 6000);
            await SqlServerApp.ExecuteAsync(database, string.Join("\n",
                Doc(logged, org, "campaigns", $$"""{"name":"Rooftop opening","channel":"Email","category":"Announcement","subject":"Rooftop","body":"{{longBody}}","segment":{"statuses":["Active"],"tiers":["Gold"]},"status":"Sent","createdAtUtc":"2026-10-01T09:00:00.000Z","sentAtUtc":"2026-10-02T09:30:00.0000000Z","recipientCount":40}"""),
                Doc(planned, org, "campaigns", """{"name":"Autumn gala","channel":"Email","category":"Event","subject":"Gala","body":"Join us","status":"Scheduled","scheduledAtUtc":"2026-11-06T17:00:00Z","createdAtUtc":"2026-10-05T10:00:00Z","recipientCount":0}"""),
                Doc(texted, org, "campaigns", """{"name":"Text blast","channel":"sms","category":"Mystery","subject":"Hi","body":"Hi","status":"Draft"}"""),
                Doc(broken, org, "campaigns", "not json at all"),
                Doc(homeless, null, "campaigns", """{"name":"From a personal workspace","subject":"Hi","body":"Hi","status":"Draft"}"""),
                Doc(note, org, "communications", $$"""{"memberId":"{{member}}","memberName":"Jean Dupont","channel":"SMS","category":"Dues","subject":"We were unable to process your dues","preview":"Your card declined.","status":"Sent","sentAtUtc":"2026-10-03T08:15:00.000Z","campaignName":"dues-failure-notice"}"""),
                Doc(noMember, org, "communications", """{"memberId":"not-an-id","memberName":"Nobody","subject":"Lost","status":"Sent"}""")));

            // The app starts on it and runs the migrations it has not had, the move among them.
            using (SqlServerApp.Start(database)) { }

            await using (var db = SqlServerSchema.Context(database))
            {
                var campaigns = await db.Campaigns.IgnoreQueryFilters().ToDictionaryAsync(c => c.Id);
                campaigns.Keys.Should().BeEquivalentTo([logged, planned, texted], "a broken document and one with no organization stay where they are");

                var rooftop = campaigns[logged];
                rooftop.Status.Should().Be(CampaignStatuses.Logged, "it was logged as sent, and nobody received it");
                rooftop.OrganizationId.Should().Be(org);
                rooftop.Body.Should().HaveLength(6000, "a long body is copied whole");
                rooftop.RecipientCount.Should().Be(40);
                rooftop.SentAtUtc.Should().Be(new DateTime(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc));
                rooftop.CreatedAtUtc.Should().Be(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));
                rooftop.Purpose.Should().Be(ConsentPurposes.Marketing);
                rooftop.Segment.Should().Contain("Gold").And.Contain("Active");

                var gala = campaigns[planned];
                gala.Status.Should().Be(CampaignStatuses.Draft, "the old screen said a planned campaign would not send itself");
                gala.ScheduledAtUtc.Should().BeNull("nothing planned then goes out unless someone schedules it again");

                campaigns[texted].Channel.Should().Be(CampaignChannels.Sms);
                campaigns[texted].Category.Should().Be("Other");

                var messages = await db.MemberMessages.IgnoreQueryFilters().ToListAsync();
                var moved = messages.Should().ContainSingle("a log entry naming no member is left behind").Subject;
                moved.Id.Should().Be(note);
                moved.MemberId.Should().Be(member);
                moved.Status.Should().Be(MemberMessageStatuses.Logged, "nothing was ever delivered");
                moved.Channel.Should().Be(CampaignChannels.Sms);
                moved.CampaignName.Should().Be("dues-failure-notice");
                moved.CreatedAtUtc.Should().Be(new DateTime(2026, 10, 3, 8, 15, 0, DateTimeKind.Utc));
                moved.OutboundMessageId.Should().BeNull();

                (await db.ClubDocuments.IgnoreQueryFilters().CountAsync()).Should().Be(7, "the documents are copied, not moved out");
            }

            // Run again, it adds nothing.
            await SqlServerApp.ExecuteAsync(database, MoveCampaignsAndMessagesToTables.MoveDocumentsSql);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Campaigns]")).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [MemberMessages]")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ACampaignIsClaimedOnceAndEachMemberEmailedOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            var members = new List<Guid>();
            foreach (var name in new[] { "Anna", "Bruno", "Clara" })
                members.Add((await Json(await owner.PostAsync("/api/members", Body(new
                {
                    firstName = name, lastName = "Dupont", email = $"{name.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
                })), "add member")).GetProperty("id").GetGuid());
            await Json(await owner.PostAsync($"/api/members/{members[0]}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })), "consent");

            // Send now, pressed twice at once: the conditional update lets one through.
            var now = (await Json(await owner.PostAsync("/api/campaigns", Body(new { name = "Now", purpose = "service", subject = "Closed Monday", body = "Closed." })), "create")).GetProperty("id").GetGuid();
            var presses = await Task.WhenAll(owner.PostAsync($"/api/campaigns/{now}/send", null), owner.PostAsync($"/api/campaigns/{now}/send", null));
            presses.Select(p => p.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [MemberMessages] WHERE [CampaignId] = '{now}'")).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [SourceId] = '{now}'")).Should().Be(3);

            // The totals are counted by the database: the list's grouped query runs as SQL.
            var listed = (await Json(await owner.GetAsync("/api/campaigns"), "list")).EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == now);
            listed.GetProperty("totals").GetProperty("recipients").GetInt32().Should().Be(3);

            // A scheduled marketing campaign whose time has come, with two schedulers at it at once.
            var later = (await Json(await owner.PostAsync("/api/campaigns", Body(new
            {
                name = "Later", purpose = "marketing", subject = "Gala", body = "Join us", scheduledAtUtc = DateTime.UtcNow.AddHours(1),
            })), "schedule")).GetProperty("id").GetGuid();
            await SqlServerApp.ExecuteAsync(database, $"UPDATE [Campaigns] SET [ScheduledAtUtc] = DATEADD(second, -5, SYSUTCDATETIME()) WHERE [Id] = '{later}'");
            async Task<int> Scheduler()
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<ICampaignService>().RunDueAsync(DateTime.UtcNow);
            }
            await Task.WhenAll(Scheduler(), Scheduler());
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [Campaigns] WHERE [Id] = '{later}'")).Should().Be(CampaignStatuses.Sent);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [MemberMessages] WHERE [CampaignId] = '{later}'")).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [SourceId] = '{later}'")).Should().Be(1,
                "only Anna agreed to marketing, and she is emailed once");

            // A second message for a member a campaign already has one for is refused by the index.
            using (var scope = app.Services.CreateScope())
            {
                using var acting = scope.ServiceProvider.GetRequiredService<ITenantContext>().ActFor(org, null);
                var again = await scope.ServiceProvider.GetRequiredService<ICampaignRepository>().AddMessageAsync(new MemberMessage
                {
                    Id = Guid.NewGuid(), OrganizationId = org, MemberId = members[0], MemberName = "Anna Dupont", CampaignId = later,
                    Subject = "Gala", Status = MemberMessageStatuses.Queued, CreatedAtUtc = DateTime.UtcNow,
                });
                again.Should().BeFalse();
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OneConsentAnswerStandsWhenTwoAreRecordedAtOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            var member = (await Json(await owner.PostAsync("/api/members", Body(new
            {
                firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
            })), "add member")).GetProperty("id").GetGuid();

            var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => owner.PostAsync($"/api/members/{member}/consent", Body(new
            {
                channel = "email", purpose = "marketing", granted = i % 2 == 0, source = "staff",
            }))));
            answers.Should().OnlyContain(a => a.StatusCode == HttpStatusCode.OK || a.StatusCode == HttpStatusCode.Conflict);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ConsentRecords] WHERE [SubjectId] = '{member}' AND [IsCurrent] = 1")).Should().Be(1,
                "the unique index lets one answer stand for a channel and purpose");

            var second = () => SqlServerApp.ExecuteAsync(database, $@"
                INSERT INTO [ConsentRecords] ([Id], [OrganizationId], [SubjectType], [SubjectId], [Channel], [Purpose], [Granted], [Source], [RecordedAtUtc], [IsCurrent])
                VALUES (NEWID(), '{org}', N'member', '{member}', N'email', N'marketing', 1, N'staff', SYSUTCDATETIME(), 1);");
            await second.Should().ThrowAsync<SqlException>();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
