using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Retention and referrals on a real SQL Server. The in-memory provider runs any LINQ in memory
/// and enforces no unique index, so only a real server shows that the score's grouped sums, the
/// board's and the report's queries turn into SQL, and that the conditional claim and the unique
/// keys set the save actions off once, take the day once and give a bonus once when requests race.
/// </summary>
public class ClubRetentionOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid Org)> ClubAsync(SqlServerApp app)
    {
        var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon46" }));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        owner.In(org);
        return (owner, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first, int renewsInDays = 20) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
            joinedAtUtc = DateTime.UtcNow.AddYears(-3), renewsAtUtc = DateTime.UtcNow.AddDays(renewsInDays),
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>Weekly visits through the year before the last 90 days, and two events she came to then.</summary>
    private static async Task HabitAsync(SqlServerApp app, Guid org, Guid memberId)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        foreach (var days in Enumerable.Range(0, 38).Select(i => 95 + i * 7))
            db.ClubVisits.Add(new ClubVisit
            {
                Id = Guid.NewGuid(), OrganizationId = org, MemberId = memberId, MemberName = "Keller", MemberTier = "Gold", Venue = "Bar", VisitedAtUtc = now.AddDays(-days),
            });
        foreach (var (days, status) in new[] { (250, RegistrationStatuses.CheckedIn), (300, RegistrationStatuses.CheckedIn), (30, RegistrationStatuses.NoShow) })
        {
            var e = new ClubEvent
            {
                Id = Guid.NewGuid(), OrganizationId = org, Name = $"Dinner {days}", StartAtUtc = now.AddDays(-days), EndAtUtc = now.AddDays(-days).AddHours(3),
                Capacity = 40, Status = ClubEventStatuses.Completed, CreatedAtUtc = now.AddDays(-days - 30), UpdatedAtUtc = now.AddDays(-days),
                EditVersion = Guid.NewGuid(), Version = Guid.NewGuid(),
            };
            db.ClubEvents.Add(e);
            db.ClubEventRegistrations.Add(new ClubEventRegistration
            {
                Id = Guid.NewGuid(), OrganizationId = org, EventId = e.Id, MemberId = memberId, Name = "Keller", Status = status,
                RegisteredAtUtc = now.AddDays(-days - 10),
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> OverdueAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId, kind = "Dues", description = "Gold dues", amount = 900m, dueAtUtc = DateTime.UtcNow.AddDays(-80).ToString("o"),
        })), "overdue dues")).GetProperty("id").GetGuid();

    [SqlServerFact]
    public async Task TheScoreTheBoardAndTheReport_AreWorkedOutInSql_AndTheDailyRunTakesItsDayOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, org) = await ClubAsync(app);
            await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveTask = true, saveEmail = true, saveEmailPurpose = "service" })), "save actions on");
            var anna = await MemberAsync(owner, "Anna");
            var ben = await MemberAsync(owner, "Ben", renewsInDays: 200);
            await HabitAsync(app, org, anna);
            var first = await Json(await owner.PostAsync($"/api/members/{anna}/risk/recompute", Body(new { })), "the baseline");
            first.GetProperty("level").GetString().Should().NotBe(RiskLevels.AtRisk);

            var dues = await OverdueAsync(owner, anna);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ClubPaymentReminders.Add(new ClubPaymentReminder
                {
                    Id = Guid.NewGuid(), OrganizationId = org, ChargeId = dues, MemberId = anna, Step = 1, DaysAfterDue = 30, Status = ReminderStatuses.Queued,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-50),
                });
                await db.SaveChangesAsync();
            }
            await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Noise at night" })), "a complaint");
            await Json(await owner.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Bar", amount = 45m })), "Ben's bar");
            await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = anna })), "Anna referred Ben");

            var risk = await Json(await owner.PostAsync($"/api/members/{anna}/risk/recompute", Body(new { })), "work it out again");

            risk.GetProperty("level").GetString().Should().Be(RiskLevels.AtRisk);
            var factors = risk.GetProperty("factors").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!, f => f.GetProperty("detail").GetString()!);
            factors[RiskScoring.Visits].Should().StartWith("0 visits in the last 90 days, against about 13");
            factors[RiskScoring.Balance].Should().Contain("80 days past its due date; 1 reminder sent in the last 90 days");
            factors[RiskScoring.Events].Should().Be("Came to 0 events in the last 6 months, against about 2 in the 6 months before; 1 no-show.");
            factors[RiskScoring.Concerns].Should().Be("1 open complaint.");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [TaskItems] WHERE [OrganizationId] = '{org}' AND [Title] = N'Member at risk: Anna Keller'"))
                .Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [OrganizationId] = '{org}' AND [Source] = N'retention'")).Should().Be(1);

            var listed = await Json(await owner.GetAsync("/api/retention/members?level=at_risk"), "who is at risk");
            listed.EnumerateArray().Single().GetProperty("memberId").GetGuid().Should().Be(anna);

            var board = await Json(await owner.GetAsync("/api/renewals"), "the board");
            var card = board.GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("cards").EnumerateArray())
                .Single(c => c.GetProperty("memberId").GetGuid() == anna);
            (card.GetProperty("stage").GetString(), card.GetProperty("riskScore").GetInt32()).Should().Be((RenewalStages.AtRisk, risk.GetProperty("score").GetInt32()));
            var renewed = await Json(await owner.PostAsync("/api/renewals/stage", Body(new
            {
                memberId = anna, endsAtUtc = card.GetProperty("endsAtUtc").GetString(), stage = "renewed",
            })), "mark it renewed");
            renewed.GetProperty("stage").GetString().Should().Be(RenewalStages.Renewed);

            var report = await Json(await owner.GetAsync("/api/referrals/report"), "the referrals report");
            var row = report.GetProperty("referrers").EnumerateArray().Single();
            row.GetProperty("memberId").GetGuid().Should().Be(anna);
            row.GetProperty("spend").EnumerateArray().Single().GetProperty("amount").GetDecimal().Should().Be(45m);
            (await owner.GetAsync("/api/referrals/report/export")).StatusCode.Should().Be(HttpStatusCode.OK);

            // Two servers running the daily run at once: the day is taken once.
            var noon = DateTime.UtcNow.Date.AddHours(12);
            async Task<int> RunAsync()
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IClubRetention>().RunScheduledAsync(noon);
            }
            (await Task.WhenAll(RunAsync(), RunAsync())).Sum().Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubScoringRuns] WHERE [OrganizationId] = '{org}' AND [Status] = N'succeeded'"))
                .Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [TaskItems] WHERE [OrganizationId] = '{org}' AND [Title] = N'Member at risk: Anna Keller'"))
                .Should().Be(1, "still at risk: nothing is set off again");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ScoresWorkedOutAtOnce_KeepOneScoreAMember_AndSetTheSaveActionsOffOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, org) = await ClubAsync(app);
            await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveTask = true })), "tasks on");
            var anna = await MemberAsync(owner, "Anna");
            var ben = await MemberAsync(owner, "Ben");
            await HabitAsync(app, org, anna);

            // A first score worked out five times at once: one row for Ben.
            var firsts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => owner.PostAsync($"/api/members/{ben}/risk/recompute", Body(new { }))));
            firsts.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMemberRisks] WHERE [MemberId] = '{ben}'")).Should().Be(1);

            await Json(await owner.PostAsync($"/api/members/{anna}/risk/recompute", Body(new { })), "Anna's baseline");
            await OverdueAsync(owner, anna);
            await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Noise" })), "a complaint");

            var answers = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => owner.PostAsync($"/api/members/{anna}/risk/recompute", Body(new { }))));

            answers.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [TaskItems] WHERE [OrganizationId] = '{org}' AND [Title] = N'Member at risk: Anna Keller'"))
                .Should().Be(1, "five scorings saw her cross; one claimed it");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMemberRisks] WHERE [MemberId] = '{anna}' AND [SaveActionsAtUtc] IS NOT NULL")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoSubscriptionsEndingTogether_EachKeepARenewal_WhileAMembershipKeepsOneAnEnd()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, org) = await ClubAsync(app);
            var hugo = await MemberAsync(owner, "Hugo", renewsInDays: 80);
            // His dues and his locker, yearly and started the same day: both end at one midnight.
            var endsAt = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(15), DateTimeKind.Utc);
            Guid dues, locker;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                ClubSubscription Yearly(string name, decimal amount) => new()
                {
                    Id = Guid.NewGuid(), OrganizationId = org, MemberId = hugo, MemberName = "Hugo Keller", Name = name, Amount = amount, Currency = "CHF",
                    Cadence = "Annual", Status = "Active", StartedAtUtc = endsAt.AddYears(-1), NextChargeAtUtc = endsAt, CreatedAtUtc = endsAt.AddYears(-1),
                    Version = Guid.NewGuid(),
                };
                var a = Yearly("Gold dues", 1200m);
                var b = Yearly("Locker", 240m);
                db.ClubSubscriptions.AddRange(a, b);
                await db.SaveChangesAsync();
                (dues, locker) = (a.Id, b.Id);
            }

            // The daily run notes his membership's renewal and each subscription's: three rows.
            using (var scope = app.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(org, null))
                (await scope.ServiceProvider.GetRequiredService<IClubRenewals>().TrackUpcomingAsync(org, DateTime.UtcNow)).Should().Be(3);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubRenewals] WHERE [MemberId] = '{hugo}' AND [Source] = N'subscription'"))
                .Should().Be(2);

            await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId = hugo, source = "subscription", subscriptionId = locker, endsAtUtc = endsAt, stage = "lapsed" })),
                "the locker is not renewing");
            await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId = hugo, source = "subscription", subscriptionId = dues, endsAtUtc = endsAt, stage = "contacted" })),
                "the dues are talked about");
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Stage] FROM [ClubRenewals] WHERE [SubscriptionId] = '{locker}'")).Should().Be(RenewalStages.Lapsed);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Stage] FROM [ClubRenewals] WHERE [SubscriptionId] = '{dues}'")).Should().Be(RenewalStages.Contacted);

            // A membership's renewal has no subscription: the index still holds it to one row an end.
            var second = () => SqlServerApp.ExecuteAsync(database, $@"
                INSERT INTO [ClubRenewals] ([Id], [OrganizationId], [MemberId], [Source], [SubscriptionId], [EndsAtUtc], [CreatedAtUtc], [Version])
                SELECT NEWID(), [OrganizationId], [MemberId], [Source], [SubscriptionId], [EndsAtUtc], [CreatedAtUtc], NEWID()
                FROM [ClubRenewals] WHERE [MemberId] = '{hugo}' AND [Source] = N'membership';");
            await second.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ABonusPressedSeveralTimesAtOnce_IsGivenOnce_AsPointsOrAsACredit()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, org) = await ClubAsync(app);
            var anna = await MemberAsync(owner, "Anna");
            var ben = await MemberAsync(owner, "Ben");
            var carl = await MemberAsync(owner, "Carl");
            await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = anna })), "Anna referred Ben");
            await Json(await owner.PutAsync($"/api/members/{carl}/referrer", Body(new { referredByMemberId = anna })), "and Carl");

            await Json(await owner.PutAsync("/api/retention/settings", Body(new { referralBonus = "points", referralBonusPoints = 300 })), "points");
            // Enough presses that some look before the first is saved and read the bonus after:
            // they gave nothing, so they are answered 409 like the others, never 200.
            var points = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => owner.PostAsync($"/api/members/{ben}/referral-bonus", Body(new { }))));
            points.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            points.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [LoyaltyLedger] WHERE [MemberId] = '{anna}'")).Should().Be(1);

            await Json(await owner.PutAsync("/api/retention/settings", Body(new { referralBonus = "credit", referralBonusAmount = 50m })), "a credit");
            var credits = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => owner.PostAsync($"/api/members/{carl}/referral-bonus", Body(new { }))));
            credits.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            credits.Where(r => r.StatusCode != HttpStatusCode.OK).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Conflict);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [OrganizationId] = '{org}' AND [IdempotencyKey] = N'referral:{carl}'"))
                .Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [ClubReferralBonuses] WHERE [ReferredMemberId] = '{carl}'")).Should().Be(ReferralBonusStatuses.Awarded);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
