using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Erasing a person on a real SQL Server: the deletes and updates of one erasure go in one
/// transaction, in an order the foreign keys accept.
/// </summary>
/// <remarks>
/// The in-memory provider the other tests use enforces no foreign key and no unique index.
/// Here a contact that has deals, activities, tasks and a lead pointing at it, a lead it was
/// made from, and a household the member heads with someone else in it, all go or change in
/// the erasure's one save; the household's next primary must be set after the old one leaves
/// (one primary per household), and the erasure's number is unique per workspace.
/// </remarks>
public class PersonDataOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task AnErasureOfARichlyLinkedPersonCommitsAsOne_AndLeavesNoRowNamingThem()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" })), "create the club")).GetProperty("id").GetGuid();
            owner.In(org);
            var s = await PersonDataTests.SeedAsync(app, ownerId, org);

            // More links than the in-memory tests need: a deal on her contact with its own
            // activity and task, a lead she referred, and a household she heads with Bob.
            var dealId = Guid.NewGuid();
            var householdId = Guid.NewGuid();
            await using (var db = app.Services.EveryWorkspace())
            {
                db.Deals.Add(new Deal { Id = dealId, UserId = ownerId, OrganizationId = org, Name = "Anna Keller, Gold", Value = "3588", ContactId = s.ContactId, Description = "Wants the terrace", CreatedAtUtc = DateTime.UtcNow });
                db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Type = "meeting", Subject = "Tour", DealId = dealId, CreatedAtUtc = DateTime.UtcNow });
                db.TaskItems.Add(new TaskItem { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Prepare the contract", DealId = dealId, CreatedAtUtc = DateTime.UtcNow });
                var referred = await db.Leads.FindAsync(s.BobLeadId);
                referred!.ReferredByContactId = s.ContactId;
                db.ClubHouseholds.Add(new ClubHousehold { Id = householdId, OrganizationId = org, Name = "Keller household", CreatedAtUtc = DateTime.UtcNow });
                db.ClubHouseholdMembers.Add(new ClubHouseholdMember { Id = Guid.NewGuid(), OrganizationId = org, HouseholdId = householdId, MemberId = s.MemberId, Role = HouseholdRoles.Primary, IsPrimary = true, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2) });
                db.ClubHouseholdMembers.Add(new ClubHouseholdMember { Id = Guid.NewGuid(), OrganizationId = org, HouseholdId = householdId, MemberId = s.BobMemberId, Role = HouseholdRoles.Other, IsPrimary = false, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) });
                // Automation runs about her lead (still waiting) and her member account (finished),
                // each with a log that names her, and the mark that stops the lead's rule firing twice.
                var rule = new AutomationRule
                {
                    Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, Name = "Nurture", Trigger = "lead.created", ActionsJson = "[]",
                    CreatedAtUtc = DateTime.UtcNow, Version = Guid.NewGuid(),
                };
                var leadRun = new AutomationRun
                {
                    Id = Guid.NewGuid(), OrganizationId = org, RuleId = rule.Id, RuleName = rule.Name, Trigger = rule.Trigger, SourceKey = s.LeadId.ToString(),
                    Status = AutomationRunStatuses.Waiting, ActorUserId = ownerId, RecordType = "lead", RecordId = s.LeadId, LeadId = s.LeadId, RecordName = "Anna Keller",
                    Email = "anna.keller@example.ch", StepsJson = "[]", CurrentPath = "1", NextAttemptAtUtc = DateTime.UtcNow.AddDays(2), WaitingFor = "2 days",
                    StartedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                };
                var memberRun = new AutomationRun
                {
                    Id = Guid.NewGuid(), OrganizationId = org, RuleId = rule.Id, RuleName = rule.Name, Trigger = "member.created", SourceKey = s.MemberId.ToString(),
                    Status = AutomationRunStatuses.Completed, ActorUserId = ownerId, RecordType = "member", RecordId = s.MemberId, MemberId = s.MemberId, RecordName = "Anna Keller",
                    StepsJson = "[]", CurrentPath = "1", StartedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, FinishedAtUtc = DateTime.UtcNow,
                };
                db.AutomationRules.Add(rule);
                db.AutomationRuns.AddRange(leadRun, memberRun);
                // The task the member's run made names her and is linked to nothing of hers; another
                // task, on Bob's lead, mentions her.
                var madeTask = new TaskItem { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Welcome call", Description = "About Anna Keller.", CreatedAtUtc = DateTime.UtcNow };
                db.TaskItems.AddRange(madeTask,
                    new TaskItem { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Ask Anna Keller to bring Bob", LeadId = s.BobLeadId, CreatedAtUtc = DateTime.UtcNow });
                db.AutomationFires.Add(new AutomationFire { Id = Guid.NewGuid(), RuleId = rule.Id, SourceKey = s.LeadId.ToString(), FiredAtUtc = DateTime.UtcNow });
                db.AutomationRunSteps.AddRange(
                    new AutomationRunStep
                    {
                        Id = Guid.NewGuid(), OrganizationId = org, RunId = leadRun.Id, Path = "0", StepType = "send_email", Outcome = AutomationStepOutcomes.Done,
                        Summary = "Queued the email \"Welcome\" to anna.keller@example.ch.", Attempt = 1, AtUtc = DateTime.UtcNow,
                    },
                    new AutomationRunStep
                    {
                        Id = Guid.NewGuid(), OrganizationId = org, RunId = memberRun.Id, Path = "0", StepType = "create_task", Outcome = AutomationStepOutcomes.Done,
                        Summary = "Created the task \"Welcome call\".", TaskId = madeTask.Id, Attempt = 1, AtUtc = DateTime.UtcNow,
                    });
                // Her application as the old documents kept it, before it had a table of its own
                // (the moved row kept the document's id): the names apart, the phone beside them.
                db.ClubDocuments.Add(new ClubDocument
                {
                    Id = s.ApplicationId, OrganizationId = org, Kind = "applications", UpdatedAtUtc = DateTime.UtcNow.AddYears(-1),
                    Payload = JsonSerializer.Serialize(new { id = s.ApplicationId, firstName = "Anna", lastName = "Keller", email = "anna.keller@example.ch", phone = "079 123 45 67" },
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                });
                await db.SaveChangesAsync();
            }

            var result = await Json(await owner.PostAsync("/api/privacy/erasure", Body(new
            {
                leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId },
                confirm = "ERASE",
            })), "erase");
            result.GetProperty("pseudonym").GetString().Should().Be("Erased member 1");

            async Task<int> Count(string sql) => Convert.ToInt32(await SqlServerApp.ScalarAsync(database, sql));
            (await Count($"SELECT COUNT(*) FROM [Leads] WHERE [Id] = '{s.LeadId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [Contacts] WHERE [Id] = '{s.ContactId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [Activities] WHERE [DealId] = '{dealId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [TaskItems] WHERE [DealId] = '{dealId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [Deals] WHERE [Id] = '{dealId}' AND [Name] = N'Deal of Erased member 1' AND [ContactId] IS NULL AND [Description] IS NULL")).Should().Be(1,
                "the deal stays for the pipeline's figures, renamed and without her");
            (await Count($"SELECT COUNT(*) FROM [Leads] WHERE [Id] = '{s.BobLeadId}' AND [ReferredByContactId] IS NULL")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [HouseholdId] = '{householdId}' AND [MemberId] = '{s.BobMemberId}' AND [IsPrimary] = 1")).Should().Be(1,
                "Bob heads the household now");
            (await Count($"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [MemberId] = '{s.MemberId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [ClubCharges] WHERE [MemberId] = '{s.MemberId}' AND [MemberName] = N'Erased member 1' AND [Amount] = 50")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [ClubReservations] WHERE [Id] = '{s.FutureReservationId}' AND [Status] = N'Cancelled'")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [EmailSuppressions] WHERE [OrganizationId] = '{org}' AND [Email] LIKE N'erased:%'")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [PersonErasures] WHERE [OrganizationId] = '{org}' AND [Number] = 1")).Should().Be(1);
            (await Count($"SELECT COUNT(*) FROM [AutomationRuns] WHERE [OrganizationId] = '{org}'")).Should().Be(0, "the runs about her go, the one still waiting too");
            (await Count($"SELECT COUNT(*) FROM [AutomationRunSteps] WHERE [OrganizationId] = '{org}'")).Should().Be(0, "with their logs");
            (await Count($"SELECT COUNT(*) FROM [AutomationFires] WHERE [SourceKey] = '{s.LeadId}'")).Should().Be(0);
            (await Count($"SELECT COUNT(*) FROM [TaskItems] WHERE [OrganizationId] = '{org}' AND [Title] = N'Welcome call'")).Should().Be(0, "the task her run made goes with her");
            (await Count($"SELECT COUNT(*) FROM [TaskItems] WHERE [LeadId] = '{s.BobLeadId}' AND [Title] = N'Ask Erased member 1 to bring Bob'")).Should().Be(1, "Bob's task keeps its words, without her name");

            // Nothing in the workspace's people, mail, history or club records names her any more.
            foreach (var (table, column) in new[]
            {
                ("Leads", "Name"), ("Contacts", "Name"), ("ClubMembers", "LastName"), ("ClubMembers", "Email"), ("ClubApplications", "Email"),
                ("Activities", "Body"), ("OutboundMessages", "Body"), ("OutboundMessages", "[To]"), ("Notifications", "Message"),
                ("ClubCharges", "MemberName"), ("ClubPayments", "MemberName"), ("ClubVisits", "MemberName"), ("ClubReservations", "MemberName"),
                ("ClubEventRegistrations", "Name"), ("AuditEvents", "EntityLabel"), ("AuditEvents", "Summary"), ("EmailSuppressions", "Email"),
                ("ClubDocuments", "Payload"), ("AutomationRuns", "RecordName"), ("AutomationRuns", "Email"), ("AutomationRunSteps", "Summary"),
                ("TaskItems", "Title"), ("TaskItems", "Description"),
            })
            {
                var col = column.StartsWith('[') ? column : $"[{column}]";
                (await Count($"SELECT COUNT(*) FROM [{table}] WHERE [OrganizationId] = '{org}' AND ({col} LIKE N'%Keller%' OR {col} LIKE N'%anna.keller@%' OR {col} LIKE N'%079 123 45 67%')"))
                    .Should().Be(0, $"{table}.{column} should not name her");
            }

            // The other workspace's Anna is someone else's record, and stays.
            (await Count($"SELECT COUNT(*) FROM [ClubMembers] WHERE [Id] = '{s.OtherOrgMemberId}' AND [Email] = N'anna.keller@example.ch'")).Should().Be(1);

            // A second erasure takes the next number.
            var bob = await Json(await owner.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { s.BobMemberId }, leadIds = new[] { s.BobLeadId }, confirm = "ERASE" })), "erase Bob");
            bob.GetProperty("number").GetInt32().Should().Be(2);
            (await Count($"SELECT COUNT(*) FROM [ClubHouseholds] WHERE [Id] = '{householdId}'")).Should().Be(0, "a household nobody is left in goes");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
