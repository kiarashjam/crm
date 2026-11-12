using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Merging two club members on a real SQL Server, with what both the money ledger and the
/// member communications move in that one transaction.
/// </summary>
/// <remarks>
/// The ledger moves the duplicate's charges, payments and tabs to the member kept and drops
/// the duplicate's house account; the communications move their consent answers and messages.
/// The consent move saves once part-way, so two answers never stand at once under its unique
/// index, and that save also writes the ledger's moves, which are already pending in the same
/// unit of work. Each group tested its own half; only a real server enforces both sides'
/// unique indexes (one house account per member, one standing answer per channel and purpose)
/// on the save they now share, and rolls the whole merge back if either part fails.
/// </remarks>
public class ClubMemberMergeOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task AMergeMovesTheDuplicatesMoneyAndConsentTogether_AndBothWorkAfterIt()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" })), "create the club"))
                .GetProperty("id").GetGuid();
            owner.In(org);
            async Task<Guid> Member(string first, decimal houseAccountBalance) => (await Json(await owner.PostAsync("/api/members", Body(new
            {
                firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active", houseAccountBalance,
            })), "add member")).GetProperty("id").GetGuid();
            async Task Answer(Guid member, string channel, bool granted) => await Json(await owner.PostAsync($"/api/members/{member}/consent", Body(new
            {
                channel, purpose = "marketing", granted, source = "staff",
            })), "record an answer");
            async Task<decimal> Balance(Guid member) =>
                (await Json(await owner.GetAsync($"/api/members/{member}/balance"), "read the balance")).GetProperty("balance").GetDecimal();

            // Two records of one person, each with money and consent of its own: the duplicate
            // owes 50 brought over, a dinner of 30 with 10 paid, has said no to news by email
            // (after the kept record said yes) and yes to news by post, and was texted once.
            var keep = await Member("Jean", -100);
            var drop = await Member("Jeannot", -50);
            var dinner = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = drop, kind = "Dining", description = "Dinner", amount = 30 })), "post a charge"))
                .GetProperty("id").GetString()!;
            await Json(await owner.PostAsync("/api/payments", Body(new { amount = 10, method = "Cash", chargeId = dinner })), "record a payment");
            await Answer(keep, "email", true);
            await Answer(drop, "email", false);
            await Answer(drop, "post", true);
            await Json(await owner.PostAsync("/api/communications", Body(new { memberId = drop, channel = "SMS", subject = "Court 2 at 6pm" })), "log a message");

            (await owner.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            // The money: every entry is the kept member's, under one house account.
            (await Balance(keep)).Should().Be(-170, "-100 and -50 brought over, a dinner of 30, 10 paid");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubHouseAccounts] WHERE [MemberId] = '{drop}'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubHouseAccounts] WHERE [MemberId] = '{keep}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [MemberId] = '{drop}'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [MemberId] = '{drop}'")).Should().Be(0);
            (await Json(await owner.GetAsync("/api/members"), "list members")).EnumerateArray()
                .Single(m => m.GetProperty("id").GetGuid() == keep).GetProperty("houseAccountBalance").GetDecimal()
                .Should().Be(-170, "the members list shows what the ledger adds up to");

            // The consent and messages: every answer moved, one standing per channel, the latest.
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ConsentRecords] WHERE [SubjectId] = '{keep}'")).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ConsentRecords] WHERE [SubjectId] = '{keep}' AND [IsCurrent] = 1")).Should().Be(2);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Granted] FROM [ConsentRecords] WHERE [SubjectId] = '{keep}' AND [IsCurrent] = 1 AND [Channel] = N'email'"))
                .Should().Be(false, "the no given on the record merged away came last");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [MemberMessages] WHERE [MemberId] = '{keep}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [Id] = '{drop}'")).Should().Be(0);

            // Both go on working for the member kept: their account takes a payment of the moved
            // dinner, and their consent takes a new answer.
            await Json(await owner.PostAsync("/api/payments", Body(new { amount = 20, method = "Cash", chargeId = dinner })), "pay the rest of the moved dinner");
            (await Balance(keep)).Should().Be(-150);
            await Answer(keep, "email", true);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ConsentRecords] WHERE [SubjectId] = '{keep}' AND [IsCurrent] = 1")).Should().Be(2);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
