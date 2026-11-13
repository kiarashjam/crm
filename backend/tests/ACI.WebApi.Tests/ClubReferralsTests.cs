using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Referrals and sponsorship: the referrer, proposer and seconder are members of the club (not
/// names typed in), sponsors held to the club's rule; the referrer's bonus is given once when the
/// applicant they brought becomes a member, as points or as a credit through the ledger; and a
/// report says which members bring members in and what those members bring.
/// </summary>
public class ClubReferralsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
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

    private static async Task<Guid> MemberAsync(HttpClient client, string first, int joinedDaysAgo = 3 * 365, string status = "Active") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status,
            joinedAtUtc = DateTime.UtcNow.AddDays(-joinedDaysAgo),
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task<JsonElement> ApplyAsync(HttpClient client, Guid? referrer = null, Guid? proposer = null, Guid? seconder = null,
        string first = "Claire", HttpStatusCode expected = HttpStatusCode.OK) =>
        await Json(await client.PostAsync("/api/applications", Body(new
        {
            firstName = first, lastName = "Martin", email = $"{first.ToLowerInvariant()}@example.ch", requestedTier = "Gold",
            referredByMemberId = referrer, proposerMemberId = proposer, seconderMemberId = seconder,
        })), "add the application", expected);

    private static async Task Rules(HttpClient owner, object rules) => await Json(await owner.PutAsync("/api/retention/settings", Body(rules)), "set the club's rules");

    // ── naming members ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnApplicationNamesItsReferrerProposerAndSeconder_AsMembersOfTheClub()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben");
        var carl = await MemberAsync(owner, "Carl");

        var application = await ApplyAsync(owner, referrer: ana, proposer: ben, seconder: carl);

        (application.GetProperty("referredByMemberId").GetGuid(), application.GetProperty("referredByMemberName").GetString()).Should().Be((ana, "Ana Keller"));
        (application.GetProperty("proposerMemberId").GetGuid(), application.GetProperty("proposerName").GetString()).Should().Be((ben, "Ben Keller"));
        (application.GetProperty("seconderMemberId").GetGuid(), application.GetProperty("seconderName").GetString()).Should().Be((carl, "Carl Keller"));
        var id = application.GetProperty("id").GetGuid();

        // Left out, they stay; an empty id takes one off.
        var cleared = await Json(await owner.PutAsync($"/api/applications/{id}", Body(new { referredByMemberId = Guid.Empty })), "take the referrer off");
        (cleared.TryGetProperty("referredByMemberId", out var referrer) && referrer.ValueKind != JsonValueKind.Null).Should().BeFalse("the referrer was taken off");
        cleared.GetProperty("proposerMemberId").GetGuid().Should().Be(ben);

        (await Refused(await owner.PutAsync($"/api/applications/{id}", Body(new { referredByMemberId = Guid.NewGuid() })), HttpStatusCode.NotFound, "an unknown referrer"))
            .Should().Contain("referrer");
        await Refused(await owner.PutAsync($"/api/applications/{id}", Body(new { seconderMemberId = ben })), HttpStatusCode.BadRequest, "the proposer seconding too");
        (await factory.Services.Read(db => db.ClubApplications.Single(a => a.Id == id).SeconderMemberId)).Should().Be(carl, "a refused change keeps nothing");
    }

    [Fact]
    public async Task WithSponsorsRequired_AnApprovalNeedsAProposerAndSeconderInGoodStanding()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        await Rules(owner, new { sponsorRule = "required", sponsorMinTenureMonths = 12, sponsorNoOverdue = true });
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben");
        var newbie = await MemberAsync(owner, "Nora", joinedDaysAgo: 70);
        var owing = await MemberAsync(owner, "Otto");
        await Json(await owner.PostAsync("/api/charges", Body(new
        {
            memberId = owing, kind = "Dues", description = "Gold dues", amount = 450m, dueAtUtc = DateTime.UtcNow.AddDays(-40).ToString("o"),
        })), "Otto's overdue dues");
        var id = (await ApplyAsync(owner, proposer: ana)).GetProperty("id").GetGuid();

        (await Refused(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), HttpStatusCode.BadRequest, "approve with one sponsor"))
            .Should().Contain("proposer and a seconder");
        (await Refused(await owner.PutAsync($"/api/applications/{id}", Body(new { seconderMemberId = newbie })), HttpStatusCode.BadRequest, "a member of two months"))
            .Should().Contain("Nora Keller").And.Contain("2 months").And.Contain("12");
        (await Refused(await owner.PutAsync($"/api/applications/{id}", Body(new { seconderMemberId = owing })), HttpStatusCode.BadRequest, "a member who owes"))
            .Should().Contain("overdue");
        var standing = await Json(await owner.GetAsync($"/api/referrals/standing?memberIds={newbie}&memberIds={ana}"), "who may sponsor");
        standing.EnumerateArray().Single(s => s.GetProperty("memberId").GetGuid() == ana).GetProperty("inGoodStanding").GetBoolean().Should().BeTrue();
        standing.EnumerateArray().Single(s => s.GetProperty("memberId").GetGuid() == newbie).GetProperty("inGoodStanding").GetBoolean().Should().BeFalse();
        (await factory.Services.Read(db => db.ClubMembers.Count(m => m.FirstName == "Claire"))).Should().Be(0, "the refused approval made nobody a member");

        await Json(await owner.PutAsync($"/api/applications/{id}", Body(new { seconderMemberId = ben })), "Ben seconds");
        var approved = await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve");
        approved.GetProperty("memberCreated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AnApplicationWhoseReferrerOrProposerWasErased_CanStillBeWorkedOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben");
        var id = (await ApplyAsync(owner, referrer: ana, proposer: ben)).GetProperty("id").GetGuid();
        await Json(await owner.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { ana, ben }, confirm = "ERASE" })), "erase Ana and Ben");

        // The committee goes on with the application: what it changes is saved, and the erased
        // referrer and proposer stay named under the pseudonyms erasure gave them.
        var edited = await Json(await owner.PutAsync($"/api/applications/{id}", Body(new { phone = "+41 79 000 00 00" })), "add a phone number");
        edited.GetProperty("phone").GetString().Should().Be("+41 79 000 00 00");
        (edited.GetProperty("referredByMemberId").GetGuid(), edited.GetProperty("proposerMemberId").GetGuid()).Should().Be((ana, ben));

        // Naming an erased member anew is refused, as naming anyone outside the club's records is.
        await ApplyAsync(owner, referrer: ana, first: "Dora", expected: HttpStatusCode.NotFound);
        await Refused(await owner.PutAsync($"/api/applications/{id}", Body(new { seconderMemberId = ana })), HttpStatusCode.NotFound, "an erased seconder");
    }

    // ── the bonus ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovingAReferredApplicant_CarriesTheReferrerOntoTheMember_AndGivesTheirPointsOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        await Rules(owner, new { referralBonus = "points", referralBonusPoints = 500 });
        var ana = await MemberAsync(owner, "Ana");
        var id = (await ApplyAsync(owner, referrer: ana)).GetProperty("id").GetGuid();

        var approved = await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve");
        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve again");

        var claire = approved.GetProperty("memberId").GetGuid();
        approved.GetProperty("notes").EnumerateArray().Select(n => n.GetString()).Should().Contain("Ana Keller was given 500 points for introducing Claire Martin.");
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == claire).ReferredByMemberId)).Should().Be(ana);
        var points = await factory.Services.Read(db => db.LoyaltyLedger.Where(e => e.MemberId == ana).ToList());
        points.Should().ContainSingle();
        (points[0].Points, points[0].Reason).Should().Be((500, "Referral"));
        var bonus = await factory.Services.Read(db => db.ClubReferralBonuses.Single());
        (bonus.ReferrerMemberId, bonus.ReferredMemberId, bonus.ApplicationId, bonus.Status, bonus.LoyaltyEntryId)
            .Should().Be((ana, claire, id, ReferralBonusStatuses.Awarded, points[0].Id));

        // Both members' pages say so, and a second bonus by hand is refused.
        var theirs = await Json(await owner.GetAsync($"/api/members/{claire}/referrals"), "Claire's referrals");
        theirs.GetProperty("referredBy").GetProperty("name").GetString().Should().Be("Ana Keller");
        theirs.GetProperty("bonusForThem").GetProperty("status").GetString().Should().Be(ReferralBonusStatuses.Awarded);
        theirs.GetProperty("bonusAvailable").GetBoolean().Should().BeFalse();
        var hers = await Json(await owner.GetAsync($"/api/members/{ana}/referrals"), "Ana's referrals");
        hers.GetProperty("referred").EnumerateArray().Select(p => p.GetProperty("memberId").GetGuid()).Should().Equal(claire);
        hers.GetProperty("bonusesEarned").GetArrayLength().Should().Be(1);
        await Refused(await owner.PostAsync($"/api/members/{claire}/referral-bonus", Body(new { })), HttpStatusCode.Conflict, "a second bonus");
    }

    [Fact]
    public async Task PointsGivenWhenTheDirectorApproves_AreNotGivenAgainWhenFinanceFinishesTheMoney()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        await Rules(owner, new { referralBonus = "points", referralBonusPoints = 300 });
        var ana = await MemberAsync(owner, "Ana");
        var id = (await ApplyAsync(director, referrer: ana)).GetProperty("id").GetGuid();

        var started = await Json(await director.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the director approves");
        started.GetProperty("moneyPending").GetBoolean().Should().BeTrue();
        (await factory.Services.Read(db => db.LoyaltyLedger.Count(e => e.MemberId == ana))).Should().Be(1, "points are not money: the director's approval gives them");
        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the owner finishes the money");

        (await factory.Services.Read(db => db.LoyaltyLedger.Count(e => e.MemberId == ana))).Should().Be(1, "the bonus is given once");
    }

    [Fact]
    public async Task ACreditBonus_WaitsForSomeoneWhoMayChargeAccounts_AndGoesThroughTheLedgerOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        await Rules(owner, new { referralBonus = "credit", referralBonusAmount = 100m });
        var ana = await MemberAsync(owner, "Ana");
        var id = (await ApplyAsync(director, referrer: ana)).GetProperty("id").GetGuid();

        var started = await Json(await director.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the director approves");
        started.GetProperty("moneyPending").GetBoolean().Should().BeTrue();
        started.GetProperty("notes").EnumerateArray().Select(n => n.GetString())
            .Should().Contain(n => n!.Contains("referral credit") && n.Contains("100.00") && n.Contains("waits for someone who can charge accounts"));
        (await factory.Services.Read(db => db.ClubReferralBonuses.Single().Status)).Should().Be(ReferralBonusStatuses.Pending);
        (await factory.Services.Read(db => db.ClubPayments.Count(p => p.MemberId == ana))).Should().Be(0, "a credit is money: the director may not give it");

        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the owner finishes the money");
        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "and presses again");

        var claire = started.GetProperty("memberId").GetGuid();
        var credits = await factory.Services.Read(db => db.ClubPayments.Where(p => p.MemberId == ana).ToList());
        credits.Should().ContainSingle();
        (credits[0].EntryType, credits[0].Amount, credits[0].IdempotencyKey).Should().Be((ClubEntryTypes.Adjustment, 100m, $"referral:{claire}"));
        credits[0].Description.Should().Be("Referral bonus for introducing Claire Martin");
        var bonus = await factory.Services.Read(db => db.ClubReferralBonuses.Single());
        (bonus.Status, bonus.PaymentId, bonus.Amount).Should().Be((ReferralBonusStatuses.Awarded, credits[0].Id, 100m));
    }

    [Fact]
    public async Task ABonusAnotherRequestIsGiving_IsLeftToIt_UntilItsHoldRunsOut()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        await Rules(owner, new { referralBonus = "credit", referralBonusAmount = 100m });
        var ana = await MemberAsync(owner, "Ana");
        var id = (await ApplyAsync(director, referrer: ana)).GetProperty("id").GetGuid();
        var claire = (await Json(await director.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the director approves"))
            .GetProperty("memberId").GetGuid();
        (await factory.Services.Read(db => db.ClubReferralBonuses.Single().ClaimedUntilUtc)).Should().BeNull("a credit left waiting holds nothing");

        async Task HeldUntil(DateTime until)
        {
            using var scope = factory.Services.CreateScope();
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.ClubReferralBonuses.Single().ClaimedUntilUtc = until;
            await db.SaveChangesAsync();
        }

        // Another request is posting the credit at this moment: this press leaves it to that one.
        await HeldUntil(DateTime.UtcNow.AddMinutes(1));
        var refused = await Refused(await owner.PostAsync($"/api/members/{claire}/referral-bonus", Body(new { })), HttpStatusCode.Conflict, "a bonus being given");
        refused.Should().Contain("Someone is giving this referral bonus at this moment");
        (await factory.Services.Read(db => db.ClubPayments.Count(p => p.MemberId == ana))).Should().Be(0);

        // That request stopped part way: once its hold runs out, the next press finishes the job.
        await HeldUntil(DateTime.UtcNow.AddSeconds(-1));
        var given = await Json(await owner.PostAsync($"/api/members/{claire}/referral-bonus", Body(new { })), "give it once the hold ran out");
        given.GetProperty("status").GetString().Should().Be(ReferralBonusStatuses.Awarded);
        (await factory.Services.Read(db => db.ClubPayments.Count(p => p.MemberId == ana))).Should().Be(1);
        (await factory.Services.Read(db => db.ClubReferralBonuses.Single().ClaimedUntilUtc)).Should().BeNull("the hold is let go with the bonus given");
    }

    [Fact]
    public async Task AReferrerSetOnTheMembersPage_CanBeGivenTheirBonusByHand_Once()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben");

        var set = await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = ana })), "Ana referred Ben");
        set.GetProperty("referredBy").GetProperty("memberId").GetGuid().Should().Be(ana);
        set.GetProperty("bonusAvailable").GetBoolean().Should().BeFalse("the club gives no bonus yet");
        await Refused(await owner.PostAsync($"/api/members/{ben}/referral-bonus", Body(new { })), HttpStatusCode.BadRequest, "a bonus the club does not give");
        await Refused(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = ben })), HttpStatusCode.BadRequest, "referring himself");
        await Refused(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = Guid.NewGuid() })), HttpStatusCode.NotFound, "an unknown referrer");
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        await Refused(await desk.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = (Guid?)null })), HttpStatusCode.Forbidden, "the front desk");

        await Rules(owner, new { referralBonus = "points", referralBonusPoints = 250 });
        var given = await Json(await owner.PostAsync($"/api/members/{ben}/referral-bonus", Body(new { })), "give the bonus");
        (given.GetProperty("status").GetString(), given.GetProperty("points").GetInt32(), given.GetProperty("referrerName").GetString())
            .Should().Be((ReferralBonusStatuses.Awarded, 250, "Ana Keller"));
        await Refused(await owner.PostAsync($"/api/members/{ben}/referral-bonus", Body(new { })), HttpStatusCode.Conflict, "the same bonus again");
        (await factory.Services.Read(db => db.LoyaltyLedger.Count(e => e.MemberId == ana))).Should().Be(1);
    }

    // ── the report ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TheReport_SaysWhoBringsMembersInAndWhatTheyBring_AndExportsForThoseAllowed()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben", joinedDaysAgo: 100);
        var carl = await MemberAsync(owner, "Carl", joinedDaysAgo: 500);
        var dora = await MemberAsync(owner, "Dora");
        await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = ana })), "Ana referred Ben");
        await Json(await owner.PutAsync($"/api/members/{carl}/referrer", Body(new { referredByMemberId = ana })), "and Carl");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Dues", description = "Gold dues", amount = 299m })), "Ben's dues");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Bar", amount = 40m })), "Ben's bar");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = carl, kind = "Spa", description = "Massage", amount = 60m })), "Carl's spa");
        var wrong = (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = carl, kind = "Bar", description = "Wrong member", amount = 500m })), "a charge in error"))
            .GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/charges/{wrong}/void", Body(new { reason = "Posted to the wrong member" })), "void it");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = dora, kind = "Bar", description = "Bar", amount = 25m })), "Dora's bar");
        // Dora proposed an applicant who was approved.
        var id = (await ApplyAsync(owner, proposer: dora)).GetProperty("id").GetGuid();
        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve Dora's applicant");

        var report = await Json(await owner.GetAsync("/api/referrals/report"), "the report");

        var rows = report.GetProperty("referrers").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("name").GetString()).Should().Equal("Ana Keller", "Dora Keller");
        var anaRow = rows[0];
        (anaRow.GetProperty("referredAllTime").GetInt32(), anaRow.GetProperty("referredInPeriod").GetInt32()).Should().Be((2, 1), "Carl joined before the last twelve months");
        Amount(anaRow.GetProperty("dues")).Should().Be(299m);
        Amount(anaRow.GetProperty("spend")).Should().Be(100m, "40 at the bar and 60 at the spa; the voided charge is taken off");
        Amount(anaRow.GetProperty("revenue")).Should().Be(399m);
        anaRow.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("name").GetString()).Should().Equal("Ben Keller", "Carl Keller");
        (rows[1].GetProperty("proposed").GetInt32(), rows[1].GetProperty("referredAllTime").GetInt32()).Should().Be((1, 0));
        rows[1].GetProperty("revenue").GetArrayLength().Should().Be(0, "Dora's own spending is not what she brought in");
        var totals = report.GetProperty("totals");
        (totals.GetProperty("referrers").GetInt32(), totals.GetProperty("referredAllTime").GetInt32()).Should().Be((1, 2));
        Amount(totals.GetProperty("revenue")).Should().Be(399m);

        var export = await owner.GetAsync("/api/referrals/report/export");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = Encoding.UTF8.GetString(await export.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        lines[0].Should().StartWith("Referrer").And.Contain("Referred member").And.Contain("Revenue");
        lines.Should().Contain(l => l.Contains("Ana Keller") && l.Contains("Ben Keller") && l.Contains("339"));
        lines.Should().Contain(l => l.Contains("Dora Keller") && !l.Contains("Ben Keller"));
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        (await desk.GetAsync("/api/referrals/report/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.GetAsync($"/api/referrals/report?from={DateTime.UtcNow:yyyy-MM-dd}&to={DateTime.UtcNow.AddYears(-1):yyyy-MM-dd}")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ABalanceCorrectionTakenBack_TakesNothingOffWhatAReferredMemberBrought()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben", joinedDaysAgo: 100);
        await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = ana })), "Ana referred Ben");
        await Json(await owner.PostAsync("/api/charges", Body(new { memberId = ben, kind = "Bar", description = "Bar", amount = 40m })), "Ben's bar");
        // Ben's balance was set by hand to owe 200 more, in error, and the correction was voided.
        var balance = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == ben).HouseAccountBalance);
        await Json(await owner.PutAsync($"/api/members/{ben}", Body(new { houseAccountBalance = balance - 200m })), "set Ben's balance by hand");
        var correction = await factory.Services.Read(db => db.ClubCharges.Single(c => c.MemberId == ben && c.EntryType == ClubEntryTypes.Adjustment).Id);
        await Json(await owner.PostAsync($"/api/charges/{correction}/void", Body(new { reason = "Set on the wrong member" })), "void the correction");

        var report = await Json(await owner.GetAsync("/api/referrals/report"), "the report");

        var anaRow = report.GetProperty("referrers").EnumerateArray().Single();
        Amount(anaRow.GetProperty("spend")).Should().Be(40m, "a correction is no sale, so voiding one takes no sale back");
        Amount(anaRow.GetProperty("revenue")).Should().Be(40m);
        Amount(report.GetProperty("totals").GetProperty("revenue")).Should().Be(40m);
    }

    private static decimal Amount(JsonElement amounts) =>
        amounts.EnumerateArray().Where(a => a.GetProperty("currency").GetString() == "CHF").Sum(a => a.GetProperty("amount").GetDecimal());

    // ── workspaces ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AnotherWorkspacesMembers_CannotBeNamed_AndTheirReferralsAreNotSeen()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var ana = await MemberAsync(owner, "Ana");
        var ben = await MemberAsync(owner, "Ben");
        await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = ana })), "Ana referred Ben");
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@elsewhere.ch");
        stranger.In(await CreateAsync(factory, strangerId, "Elsewhere"));
        var theirs = await MemberAsync(stranger, "Zoe");

        await ApplyAsync(stranger, referrer: ana, expected: HttpStatusCode.NotFound);
        await ApplyAsync(owner, proposer: theirs, expected: HttpStatusCode.NotFound);
        await Refused(await stranger.PutAsync($"/api/members/{theirs}/referrer", Body(new { referredByMemberId = ana })), HttpStatusCode.NotFound, "a referrer from another club");
        await Refused(await stranger.GetAsync($"/api/members/{ben}/referrals"), HttpStatusCode.NotFound, "another club's member");
        (await Json(await stranger.GetAsync("/api/referrals/report"), "their report")).GetProperty("referrers").GetArrayLength().Should().Be(0);
        (await Json(await stranger.GetAsync($"/api/referrals/standing?memberIds={ana}"), "another club's standing")).GetArrayLength().Should().Be(0);
    }
}
