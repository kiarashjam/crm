using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Each member's at-risk score: worked out on the server from their own records, explained sign
/// by sign, weighed by the club's settings, and the club's save actions set off once when a
/// member crosses into at risk (a task for their owner, an email through the outbox, and the
/// "When a member becomes at risk" automations), not on their first score and not again within
/// the cooldown. The daily run scores each workspace once a day, and none whose club is off.
/// </summary>
public class ClubRetentionTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid OwnerId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    /// <summary>A member who joined <paramref name="yearsAgo"/> years ago.</summary>
    private static async Task<Guid> MemberAsync(HttpClient client, string first, double yearsAgo = 3, string? email = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
            joinedAtUtc = DateTime.UtcNow.AddDays(-365 * yearsAgo),
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>Check-ins <paramref name="daysAgo"/> days back, one each.</summary>
    private static async Task VisitsAsync(CustomWebApplicationFactory factory, Guid org, Guid memberId, IEnumerable<int> daysAgo)
    {
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        foreach (var days in daysAgo)
            db.ClubVisits.Add(new ClubVisit
            {
                Id = Guid.NewGuid(), OrganizationId = org, MemberId = memberId, MemberName = "Keller", MemberTier = "Gold", Venue = "Bar",
                VisitedAtUtc = DateTime.UtcNow.AddDays(-days),
            });
        await db.SaveChangesAsync();
    }

    /// <summary>Weekly visits through the year before the last 90 days: a habit to fall from.</summary>
    private static Task HabitAsync(CustomWebApplicationFactory factory, Guid org, Guid memberId) =>
        VisitsAsync(factory, org, memberId, Enumerable.Range(0, 38).Select(i => 95 + i * 7));

    private static async Task<Guid> OverdueDuesAsync(HttpClient client, Guid memberId, decimal amount, int daysPastDue) =>
        (await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId, kind = "Dues", description = "Gold dues", amount, dueAtUtc = DateTime.UtcNow.AddDays(-daysPastDue).ToString("o"),
        })), "post overdue dues")).GetProperty("id").GetGuid();

    private static async Task<JsonElement> RecomputeAsync(HttpClient client, Guid memberId) =>
        await Json(await client.PostAsync($"/api/members/{memberId}/risk/recompute", Body(new { })), "work the score out");

    private static JsonElement Factor(JsonElement risk, string key) =>
        risk.GetProperty("factors").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key);

    // ── the score ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AMembersScore_ComesFromTheirOwnRecords_AndEachSignSaysWhy()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        await OverdueDuesAsync(owner, anna, 450m, 70);

        var risk = await RecomputeAsync(owner, anna);

        risk.GetProperty("score").GetInt32().Should().Be(30 + 25, "visits fell to nothing against a weekly habit, and dues are 70 days overdue");
        risk.GetProperty("level").GetString().Should().Be(RiskLevels.Watch, "55 is over the watch threshold of 35 and under the at-risk threshold of 60");
        var visits = Factor(risk, RiskScoring.Visits);
        visits.GetProperty("points").GetInt32().Should().Be(30);
        visits.GetProperty("detail").GetString().Should().Be("0 visits in the last 90 days, against about 13 in a usual 90 days before.");
        Factor(risk, RiskScoring.Balance).GetProperty("detail").GetString().Should().StartWith("CHF").And.Contain("450.00 overdue, the oldest 70 days past its due date");
        // The member's page reads the same, as worked out.
        var read = await Json(await owner.GetAsync($"/api/members/{anna}/risk"), "read the score");
        read.GetProperty("risk").GetProperty("score").GetInt32().Should().Be(55);
    }

    [Fact]
    public async Task TheClubsWeights_DecideTheScore_AndTheirSettingsKeepAVersion()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        await OverdueDuesAsync(owner, anna, 450m, 90);
        var before = await Json(await owner.GetAsync("/api/retention/settings"), "read the rules");
        before.GetProperty("version").ValueKind.Should().Be(JsonValueKind.Null, "nothing is saved yet: the defaults apply");

        var saved = await Json(await owner.PutAsync("/api/retention/settings", Body(new
        {
            visitWeight = 0, spendWeight = 0, eventWeight = 0, concernWeight = 0, tenureWeight = 0, balanceWeight = 40,
        })), "only money counts");
        var version = saved.GetProperty("version").GetGuid();
        (await RecomputeAsync(owner, anna)).GetProperty("score").GetInt32().Should().Be(100, "the balance is the only sign that counts, and it shows in full");

        // A save made from the first copy is refused; a save from the latest lands.
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { atRiskThreshold = 70, version })), "a second save from the same copy");
        var stale = await owner.PutAsync("/api/retention/settings", Body(new { atRiskThreshold = 75, version }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(await owner.GetAsync("/api/retention/settings"), "read again")).GetProperty("atRiskThreshold").GetInt32().Should().Be(70);
        (await owner.PutAsync("/api/retention/settings", Body(new { watchThreshold = 80 }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the watch threshold must be under the at-risk one");
        (await owner.PutAsync("/api/retention/settings", Body(new { balanceWeight = 0 }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "at least one sign has to count");

        // The front desk reads the rules but may not change them.
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        await Json(await desk.GetAsync("/api/retention/settings"), "the front desk reads them");
        (await desk.PutAsync("/api/retention/settings", Body(new { saveTask = true }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConcernsCountInTheScore_UntilTheyAreResolved()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        var complaint = await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Waited 40 minutes for a table" })), "record a complaint");
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Locker broken again" })), "and another");

        var risk = await RecomputeAsync(owner, anna);
        Factor(risk, RiskScoring.Concerns).GetProperty("points").GetInt32().Should().Be(15, "two open complaints are all of the sign");
        Factor(risk, RiskScoring.Concerns).GetProperty("detail").GetString().Should().Be("2 open complaints.");

        await Json(await owner.PostAsync($"/api/members/{anna}/concerns/{complaint.GetProperty("id").GetGuid()}/resolve",
            Body(new { resolution = "Apologised, dinner on the house" })), "resolve one");
        var after = await RecomputeAsync(owner, anna);
        Factor(after, RiskScoring.Concerns).GetProperty("points").GetInt32().Should().Be(9, "one open complaint is half the sign; one just resolved a little");
        var listed = await Json(await owner.GetAsync($"/api/members/{anna}/concerns"), "list them");
        listed.GetArrayLength().Should().Be(2);
        listed.EnumerateArray().Count(c => c.TryGetProperty("resolvedAtUtc", out var r) && r.ValueKind != JsonValueKind.Null).Should().Be(1);
    }

    [Fact]
    public async Task AnOpeningBalanceTakenBack_IsNoSpending_SoAFallInSpendingStillShows()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        // CHF 900 at the bar in the months before the last 90 days, nothing since; and an opening
        // balance brought over in error, voided 150 days ago.
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            ClubCharge Entry(string type, decimal amount, int daysAgo, Guid? reverses = null) => new()
            {
                Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna Keller", EntryType = type, Kind = "Other",
                Description = type == ClubEntryTypes.Charge ? "Bar" : "Opening balance", Amount = amount, Currency = "CHF",
                PostedAtUtc = DateTime.UtcNow.AddDays(-daysAgo), ReversesChargeId = reverses, CreatedAtUtc = DateTime.UtcNow.AddDays(-daysAgo),
            };
            db.ClubCharges.AddRange(Entry(ClubEntryTypes.Charge, 300m, 120), Entry(ClubEntryTypes.Charge, 300m, 200), Entry(ClubEntryTypes.Charge, 300m, 300));
            var opening = Entry(ClubEntryTypes.Opening, 2000m, 340);
            db.ClubCharges.AddRange(opening, Entry(ClubEntryTypes.Void, -2000m, 150, opening.Id));
            await db.SaveChangesAsync();
        }

        var risk = await RecomputeAsync(owner, anna);

        var spend = Factor(risk, RiskScoring.Spend);
        spend.GetProperty("points").GetInt32().Should().Be(15, "she spent about CHF 300 in a usual 90 days and nothing in the last 90: the whole of the sign");
        spend.GetProperty("detail").GetString().Should().Contain("against about").And.Contain("300.00");
    }

    // ── save actions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AMemberWhoBecomesAtRisk_SetsTheClubsSaveActionsOffOnce_TaskEmailAndAutomation()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new
        {
            saveTask = true, saveTaskDueDays = 2, saveEmail = true, saveEmailPurpose = "service",
        })), "switch the save actions on");
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "Call members at risk", trigger = AutomationCatalog.MemberAtRisk,
            conditions = new { match = "all", conditions = new[] { new { field = "riskScore", op = "gte", value = "60" } } },
            actions = new object[] { new { type = "create_task", config = new { title = "Invite them to the chef's table" } } },
        })), "a rule on the new trigger");
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        // The first score is the baseline: a steady member, at no risk.
        (await RecomputeAsync(owner, anna)).GetProperty("level").GetString().Should().Be(RiskLevels.Low);

        // Then her dues go unpaid for months, and she stopped coming long ago.
        await OverdueDuesAsync(owner, anna, 900m, 80);
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Unhappy with the new opening hours" })), "a complaint");
        var risk = await RecomputeAsync(owner, anna);

        risk.GetProperty("level").GetString().Should().Be(RiskLevels.AtRisk);
        risk.GetProperty("atRiskSinceUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        var tasks = await factory.Services.Read(db => db.TaskItems.Where(t => t.OrganizationId == org).ToList());
        var saveTask = tasks.Single(t => t.Title == "Member at risk: Anna Keller");
        saveTask.AssigneeId.Should().Be(ownerId, "the member's owner gets it");
        saveTask.Description.Should().Contain("at-risk score is 63").And.Contain("0 visits in the last 90 days");
        tasks.Should().ContainSingle(t => t.Title == "Invite them to the chef's table", "the club's own rule on the trigger ran, its condition met");
        var email = await factory.Services.Read(db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Retention));
        (email.To, email.MemberId, email.Purpose).Should().Be(("anna@example.ch", anna, ConsentPurposes.Service));
        email.Subject.Should().Be("We would love to see you at Pavillon46 again");
        email.Body.Should().Contain("Dear Anna,");
        (await factory.Services.Read(db => db.MemberMessages.Count(m => m.MemberId == anna && m.OutboundMessageId == email.Id))).Should().Be(1,
            "the email shows in the member's communications");

        // Worked out again, still at risk: nothing is set off a second time.
        await RecomputeAsync(owner, anna);
        await RecomputeAsync(owner, anna);
        (await factory.Services.Read(db => db.TaskItems.Count(t => t.OrganizationId == org))).Should().Be(2);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Retention))).Should().Be(1);
        (await factory.Services.Read(db => db.AutomationRuns.Count())).Should().Be(1);
    }

    [Fact]
    public async Task AMembersFirstScore_IsTheBaseline_AndSetsNothingOff_EvenAtRisk()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveTask = true, saveEmail = true, saveEmailPurpose = "service" })), "switch the actions on");
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        await OverdueDuesAsync(owner, anna, 900m, 80);
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Noise" })), "a complaint");

        var risk = await RecomputeAsync(owner, anna);

        risk.GetProperty("level").GetString().Should().Be(RiskLevels.AtRisk);
        risk.TryGetProperty("saveActionsAtUtc", out _).Should().BeFalse("switching scoring on must not email every member already at risk");
        (await factory.Services.Read(db => db.TaskItems.Count(t => t.OrganizationId == org))).Should().Be(0);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Retention))).Should().Be(0);
    }

    [Fact]
    public async Task AMemberAtRiskAgainWithinTheCooldown_GetsNoSecondRoundOfSaveActions()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveTask = true, saveCooldownDays = 60 })), "tasks on, a 60-day cooldown");
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        await RecomputeAsync(owner, anna);
        var dues = await OverdueDuesAsync(owner, anna, 900m, 80);
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Noise" })), "a complaint");
        (await RecomputeAsync(owner, anna)).GetProperty("level").GetString().Should().Be(RiskLevels.AtRisk);
        (await factory.Services.Read(db => db.TaskItems.Count(t => t.OrganizationId == org))).Should().Be(1);

        // She pays and comes back for a while: no longer at risk.
        await Json(await owner.PostAsync("/api/payments", Body(new { chargeId = dues, amount = 900m, method = "Wire" })), "she pays");
        await VisitsAsync(factory, org, anna, Enumerable.Range(1, 14));
        (await RecomputeAsync(owner, anna)).GetProperty("level").GetString().Should().NotBe(RiskLevels.AtRisk);

        // Then falls away again, within the 60 days: at risk, but the actions wait.
        await OverdueDuesAsync(owner, anna, 900m, 80);
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            db.ClubVisits.RemoveRange(db.ClubVisits.Where(v => v.MemberId == anna && v.VisitedAtUtc > DateTime.UtcNow.AddDays(-20)));
            await db.SaveChangesAsync();
        }
        var again = await RecomputeAsync(owner, anna);
        again.GetProperty("level").GetString().Should().Be(RiskLevels.AtRisk);
        again.GetProperty("notes").EnumerateArray().Single().GetString().Should().Contain("within the club's 60 days");
        (await factory.Services.Read(db => db.TaskItems.Count(t => t.OrganizationId == org))).Should().Be(1, "the cooldown holds the task back");
    }

    [Fact]
    public async Task AMarketingSaveEmail_GoesOnlyToAMemberWhoAgreed()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveEmail = true, saveEmailPurpose = "marketing" })), "a marketing email");
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        await RecomputeAsync(owner, anna);
        await OverdueDuesAsync(owner, anna, 900m, 80);
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Noise" })), "a complaint");

        await RecomputeAsync(owner, anna);

        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Retention))).Should().Be(0,
            "she never agreed to marketing by email");
        var logged = await factory.Services.Read(db => db.MemberMessages.Single(m => m.MemberId == anna));
        logged.Status.Should().Be(MemberMessageStatuses.Suppressed);
    }

    // ── the daily run ────────────────────────────────────────────────────────

    [Fact]
    public async Task TheDailyRun_ScoresEachWorkspaceOnceADay_AndNoneWhoseClubIsOff()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var (other, otherId) = await SignInAsync(factory, "other@club.ch");
        var off = await CreateAsync(factory, otherId, "Closed club");
        other.In(off);
        var anna = await MemberAsync(owner, "Anna");
        await MemberAsync(other, "Paul");
        var lapsed = await MemberAsync(owner, "Lena");
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            db.Organizations.Single(o => o.Id == off).ClubModuleOff = true;
            db.ClubMemberRisks.Add(new ClubMemberRisk
            {
                Id = Guid.NewGuid(), OrganizationId = org, MemberId = lapsed, Score = 70, Level = RiskLevels.AtRisk, ComputedAtUtc = DateTime.UtcNow.AddDays(-1),
            });
            db.ClubMembers.Single(m => m.Id == lapsed).Status = "Lapsed";
            await db.SaveChangesAsync();
        }
        // Noon in Zurich, after the 05:00 start.
        var noon = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Unspecified),
            ACI.Application.Common.RegionalSettings.ZoneOf("Europe/Zurich"));

        using (var scope = factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubRetention>().RunScheduledAsync(noon)).Should().Be(1, "the club that is off is not scored");
        using (var scope = factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubRetention>().RunScheduledAsync(noon.AddHours(3))).Should().Be(0, "its day is taken");

        var runs = await factory.Services.Read(db => db.ClubScoringRuns.ToList());
        runs.Should().ContainSingle();
        (runs[0].OrganizationId, runs[0].Status, runs[0].MembersScored).Should().Be((org, "succeeded", 1));
        var risks = await factory.Services.Read(db => db.ClubMemberRisks.ToList());
        risks.Should().ContainSingle(r => r.MemberId == anna, "a lapsed member has no score, and the club that is off scored nobody");
        var read = await Json(await owner.GetAsync("/api/retention/runs"), "the runs");
        read.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task TheDailyRun_SetsTheSaveActionsOff_ForAMemberWhoCrossedSinceTheDayBefore()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { saveTask = true, saveEmail = true, saveEmailPurpose = "service" })),
            "switch the save actions on");
        var anna = await MemberAsync(owner, "Anna");
        await HabitAsync(factory, org, anna);
        var noon = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Unspecified),
            ACI.Application.Common.RegionalSettings.ZoneOf("Europe/Zurich"));
        async Task RunAsync(DateTime at)
        {
            using var scope = factory.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<IClubRetention>().RunScheduledAsync(at)).Should().Be(1);
        }

        // The first morning: her baseline, a steady member.
        await RunAsync(noon);
        (await factory.Services.Read(db => db.ClubMemberRisks.Single(r => r.MemberId == anna).Level)).Should().Be(RiskLevels.Low);

        // Her dues go unpaid and she complains; the next morning's run finds her at risk, with
        // nobody signed in: the task, the email and the run's counts are the job's own.
        await OverdueDuesAsync(owner, anna, 900m, 80);
        await Json(await owner.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "complaint", note = "Unhappy with the new opening hours" })), "a complaint");
        await RunAsync(noon.AddDays(1));

        var task = await factory.Services.Read(db => db.TaskItems.Single(t => t.OrganizationId == org && t.Title == "Member at risk: Anna Keller"));
        task.AssigneeId.Should().Be(ownerId);
        (await factory.Services.Read(db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Retention && m.MemberId == anna))).Should().Be(1);
        var run = await factory.Services.Read(db => db.ClubScoringRuns.OrderByDescending(r => r.StartedAtUtc).First());
        (run.Status, run.BecameAtRisk, run.TasksCreated, run.EmailsQueued).Should().Be(("succeeded", 1, 1, 1));
        (await factory.Services.Read(db => db.ClubMemberRisks.Single(r => r.MemberId == anna).SaveTaskId)).Should().Be(task.Id);
    }

    // ── workspaces ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AnotherWorkspacesMember_CannotBeScoredRead_OrHaveConcernsRecorded()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        await RecomputeAsync(owner, anna);
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@elsewhere.ch");
        var theirs = await CreateAsync(factory, strangerId, "Elsewhere");
        stranger.In(theirs);

        (await stranger.GetAsync($"/api/members/{anna}/risk")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/members/{anna}/risk/recompute", Body(new { }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/members/{anna}/concerns", Body(new { kind = "concern", note = "x" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Json(await stranger.GetAsync("/api/retention/members?level=watch"), "their own list")).GetArrayLength().Should().Be(0);
        (await factory.Services.Read(db => db.ClubMemberConcerns.Count())).Should().Be(0);
    }
}
