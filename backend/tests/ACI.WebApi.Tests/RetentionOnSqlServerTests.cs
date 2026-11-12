using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The retention rules' queries, run on a real SQL Server: the in-memory provider evaluates
/// them in C#, and a rule that SQL Server cannot translate (or translates differently) would
/// only fail in production.
/// </summary>
public class RetentionOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task EveryRulePreviewsAndRunsOnSqlServer_AndASecondRunFindsNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" })), "create the club")).GetProperty("id").GetGuid();
            owner.In(org);

            var old = DateTime.UtcNow.AddYears(-3);
            var ancient = DateTime.UtcNow.AddYears(-12);
            await using (var db = app.Services.EveryWorkspace())
            {
                db.RecordsAuditTrail = false;
                var lead = new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Old Prospect", Email = "old@example.ch", Status = "New", CreatedAtUtc = old };
                db.Leads.Add(lead);
                db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Type = "note", Body = "Long ago", LeadId = lead.Id, CreatedAtUtc = old, OccurredAtUtc = old });
                db.ClubMembers.Add(new ClubMember
                {
                    Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, FirstName = "Gone", LastName = "Former", Email = "gone@example.ch",
                    Tier = "Gold", Status = "Cancelled", JoinedAtUtc = ancient, RenewsAtUtc = ancient.AddYears(1),
                });
                var message = new OutboundMessage { Id = Guid.NewGuid(), OrganizationId = org, Status = OutboundStatuses.Sent, To = "x@example.ch", Subject = "Old", Body = "Old words", TextBody = "Old words", CreatedAtUtc = old };
                db.OutboundMessages.Add(message);
                db.OutboundAttachments.Add(new OutboundAttachment { Id = Guid.NewGuid(), OrganizationId = org, OutboundMessageId = message.Id, FileName = "a.pdf", Content = [1], CreatedAtUtc = old });
                db.AuditEvents.Add(new AuditEvent { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, EntityType = "contact", EntityLabel = "Someone", Action = "updated", Summary = "Updated phone", ActorName = "Olga", CreatedAtUtc = old });
                db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Old news", CreatedAtUtc = old });
                await db.SaveChangesAsync();
            }

            await Json(await owner.PutAsync("/api/privacy/settings", Body(new
            {
                marketingConsent = "optional", leadRetentionMonths = 24, memberRetentionYears = 10, mailRetentionMonths = 12, detailRetentionMonths = 12,
            })), "set the periods");
            var preview = await Json(await owner.GetAsync("/api/privacy/retention/preview"), "preview");
            preview.GetProperty("rules").EnumerateArray().ToDictionary(r => r.GetProperty("rule").GetString()!, r => r.GetProperty("count").GetInt32())
                .Should().BeEquivalentTo(new Dictionary<string, int> { ["leads"] = 1, ["members"] = 1, ["mail"] = 1, ["details"] = 2 });

            var run = await Json(await owner.PostAsync("/api/privacy/retention/run", null), "run");
            (run.GetProperty("leadsDeleted").GetInt32(), run.GetProperty("membersAnonymised").GetInt32(), run.GetProperty("messagesPurged").GetInt32(), run.GetProperty("detailsPurged").GetInt32())
                .Should().Be((1, 1, 1, 2));
            run.GetProperty("status").GetString().Should().Be(RetentionRunStatuses.Succeeded, run.GetProperty("failures").ToString());

            async Task<int> Count(string sql) => Convert.ToInt32(await SqlServerApp.ScalarAsync(database, sql));
            (await Count($"SELECT COUNT(*) FROM [Leads] WHERE [OrganizationId] = '{org}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [Activities] WHERE [OrganizationId] = '{org}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [ClubMembers] WHERE [OrganizationId] = '{org}' AND [ErasedAtUtc] IS NOT NULL AND [Email] = N''")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [OutboundMessages] WHERE [OrganizationId] = '{org}' AND [TextBody] IS NULL")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [OutboundAttachments] WHERE [OrganizationId] = '{org}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [Notifications] WHERE [OrganizationId] = '{org}'")).Should().Be(0);

            var again = await Json(await owner.PostAsync("/api/privacy/retention/run", null), "run again");
            (again.GetProperty("leadsDeleted").GetInt32() + again.GetProperty("membersAnonymised").GetInt32()
                + again.GetProperty("messagesPurged").GetInt32() + again.GetProperty("detailsPurged").GetInt32()).Should().Be(0);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
