using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A member's retention and referral records when the member is exported, erased, merged into
/// another record or deleted: the score, renewals and concerns are theirs and go with them; the
/// referral bonuses are the books' and stay; the links to them as referrer or sponsor follow the
/// kept member, or name nobody.
/// </summary>
public class ClubRetentionPrivacyTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Owner, Guid Org)> ClubAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string last = "Keller") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = last, email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
            joinedAtUtc = DateTime.UtcNow.AddYears(-3), renewsAtUtc = DateTime.UtcNow.AddDays(20),
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>A score, a renewal marked contacted and a complaint, for <paramref name="memberId"/>.</summary>
    private static async Task RetentionRecordsAsync(HttpClient owner, CustomWebApplicationFactory factory, Guid memberId)
    {
        await Json(await owner.PostAsync($"/api/members/{memberId}/risk/recompute", Body(new { })), "work the score out");
        await Json(await owner.PostAsync($"/api/members/{memberId}/concerns", Body(new { kind = "complaint", note = "Unhappy with the locker room" })), "a complaint");
        var endsAt = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == memberId).RenewsAtUtc);
        await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId, endsAtUtc = endsAt, stage = "contacted", note = "Called about the renewal" })), "mark contacted");
    }

    [Fact]
    public async Task ErasingAMember_TakesTheirScoreRenewalsAndConcerns_AndKeepsTheirReferralBonusesWithTheBooks()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org) = await ClubAsync(factory);
        await Json(await owner.PutAsync("/api/retention/settings", Body(new { referralBonus = "points", referralBonusPoints = 100 })), "points for referrals");
        var anna = await MemberAsync(owner, "Anna");
        var ben = await MemberAsync(owner, "Ben");
        await RetentionRecordsAsync(owner, factory, anna);
        await Json(await owner.PutAsync($"/api/members/{ben}/referrer", Body(new { referredByMemberId = anna })), "Anna referred Ben");
        await Json(await owner.PostAsync($"/api/members/{ben}/referral-bonus", Body(new { })), "Anna's bonus");
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            // A bonus once held back, its reason naming her.
            var carl = Guid.NewGuid();
            db.ClubReferralBonuses.Add(new ClubReferralBonus
            {
                Id = Guid.NewGuid(), OrganizationId = org, ReferrerMemberId = anna, ReferredMemberId = carl, Kind = ReferralBonusKinds.Points,
                Status = ReferralBonusStatuses.Skipped, Reason = "Anna Keller is lapsed, not an active member", Currency = "CHF", CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var plan = await Json(await owner.PostAsync("/api/privacy/erasure/preview", Body(new { memberIds = new[] { anna } })), "preview");
        var lines = plan.GetProperty("lines").EnumerateArray()
            .ToDictionary(l => l.GetProperty("area").GetString()!, l => (l.GetProperty("action").GetString(), l.GetProperty("count").GetInt32()));
        lines["At-risk score"].Should().Be(("delete", 1));
        lines["Renewals on the renewals board"].Should().Be(("delete", 1));
        lines["Complaints and concerns about them"].Should().Be(("delete", 1));
        lines["Referral bonuses"].Should().Be(("keep", 2));

        var export = await owner.PostAsync("/api/privacy/export", Body(new { memberIds = new[] { anna } }));
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(), ZipArchiveMode.Read))
        {
            zip.Entries.Select(e => e.FullName).Should().Contain(new[] { "club/retention.json", "club/referral-bonuses.json" });
            using var reader = new StreamReader(zip.GetEntry("club/retention.json")!.Open());
            var retention = await reader.ReadToEndAsync();
            retention.Should().Contain("Unhappy with the locker room").And.Contain("Called about the renewal").And.Contain("factorsJson");
        }

        await Json(await owner.PostAsync("/api/privacy/erasure", Body(new { memberIds = new[] { anna }, confirm = "ERASE" })), "erase Anna");

        (await factory.Services.Read(db => db.ClubMemberRisks.Count(r => r.MemberId == anna))).Should().Be(0);
        (await factory.Services.Read(db => db.ClubRenewals.Count(r => r.MemberId == anna))).Should().Be(0);
        (await factory.Services.Read(db => db.ClubMemberConcerns.Count(c => c.MemberId == anna))).Should().Be(0);
        var bonuses = await factory.Services.Read(db => db.ClubReferralBonuses.Where(b => b.ReferrerMemberId == anna).ToList());
        bonuses.Should().HaveCount(2, "what was given stays with the books");
        bonuses.Select(b => b.Reason).Should().NotContain(r => r != null && r.Contains("Anna"));
        // Ben still shows who brought him in: under her pseudonym.
        var bens = await Json(await owner.GetAsync($"/api/members/{ben}/referrals"), "Ben's referrals");
        bens.GetProperty("referredBy").GetProperty("name").GetString().Should().StartWith("Erased");
        // And the daily run does not score her again.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubRetention>().RunScheduledAsync(DateTime.UtcNow.Date.AddHours(12));
        (await factory.Services.Read(db => db.ClubMemberRisks.Count(r => r.MemberId == anna))).Should().Be(0);
    }

    [Fact]
    public async Task MergingTwoRecords_MovesConcernsRenewalsAndReferrals_ToTheMemberKept()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _) = await ClubAsync(factory);
        var keep = await MemberAsync(owner, "Anna");
        var drop = await MemberAsync(owner, "Anna", "Keller-Weber");
        var carl = await MemberAsync(owner, "Carl");
        var referrer = await MemberAsync(owner, "Rita");
        await RetentionRecordsAsync(owner, factory, drop);
        await Json(await owner.PutAsync($"/api/members/{carl}/referrer", Body(new { referredByMemberId = drop })), "the duplicate referred Carl");
        await Json(await owner.PutAsync($"/api/members/{drop}/referrer", Body(new { referredByMemberId = referrer })), "Rita referred the duplicate");
        var application = (await Json(await owner.PostAsync("/api/applications", Body(new
        {
            firstName = "Claire", lastName = "Martin", email = "claire@example.ch", requestedTier = "Gold", proposerMemberId = drop,
        })), "an application the duplicate proposed")).GetProperty("id").GetGuid();

        (await owner.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await factory.Services.Read(db => db.ClubMemberConcerns.Single().MemberId)).Should().Be(keep);
        (await factory.Services.Read(db => db.ClubRenewals.Single().MemberId)).Should().Be(keep);
        (await factory.Services.Read(db => db.ClubMemberRisks.Any(r => r.MemberId == drop))).Should().BeFalse("the duplicate's score goes; the next run scores the member kept");
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == carl).ReferredByMemberId)).Should().Be(keep);
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == keep).ReferredByMemberId)).Should().Be(referrer);
        (await factory.Services.Read(db => db.ClubApplications.Single(a => a.Id == application).ProposerMemberId)).Should().Be(keep);
    }

    [Fact]
    public async Task DeletingAMember_LeavesNoScoreRenewalsOrConcerns_AndNobodyNamedAsReferredOrSponsoredByThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna");
        var carl = await MemberAsync(owner, "Carl");
        await RetentionRecordsAsync(owner, factory, anna);
        await Json(await owner.PutAsync($"/api/members/{carl}/referrer", Body(new { referredByMemberId = anna })), "Anna referred Carl");
        var application = (await Json(await owner.PostAsync("/api/applications", Body(new
        {
            firstName = "Claire", lastName = "Martin", email = "claire@example.ch", requestedTier = "Gold", referredByMemberId = anna, seconderMemberId = anna,
        })), "an application naming Anna")).GetProperty("id").GetGuid();

        (await owner.DeleteAsync($"/api/members/{anna}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await factory.Services.Read(db => db.ClubMemberRisks.Count() + db.ClubRenewals.Count() + db.ClubMemberConcerns.Count())).Should().Be(0);
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == carl).ReferredByMemberId)).Should().BeNull();
        var app = await factory.Services.Read(db => db.ClubApplications.Single(a => a.Id == application));
        (app.ReferredByMemberId, app.SeconderMemberId).Should().Be(((Guid?)null, (Guid?)null));
    }
}
