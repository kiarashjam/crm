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

            // Nothing in the workspace's people, mail, history or club records names her any more.
            foreach (var (table, column) in new[]
            {
                ("Leads", "Name"), ("Contacts", "Name"), ("ClubMembers", "LastName"), ("ClubMembers", "Email"), ("ClubApplications", "Email"),
                ("Activities", "Body"), ("OutboundMessages", "Body"), ("OutboundMessages", "[To]"), ("Notifications", "Message"),
                ("ClubCharges", "MemberName"), ("ClubPayments", "MemberName"), ("ClubVisits", "MemberName"), ("ClubReservations", "MemberName"),
                ("ClubEventRegistrations", "Name"), ("AuditEvents", "EntityLabel"), ("AuditEvents", "Summary"), ("EmailSuppressions", "Email"),
            })
            {
                var col = column.StartsWith('[') ? column : $"[{column}]";
                (await Count($"SELECT COUNT(*) FROM [{table}] WHERE [OrganizationId] = '{org}' AND ({col} LIKE N'%Keller%' OR {col} LIKE N'%anna.keller@%')"))
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
