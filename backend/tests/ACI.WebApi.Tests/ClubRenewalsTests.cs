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
/// The renewals board: whose membership or subscription period ends in the window, in a column
/// for where it stands (upcoming, contacted, at risk, renewed, lapsed) worked out from the
/// records, with the member's owner, what the renewal is worth and when someone last spoke to
/// them; and a renewal marked by hand, the membership moved on a year once.
/// </summary>
public class ClubRenewalsTests
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

    private static async Task<(HttpClient Client, Guid UserId)> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return (client.In(org), userId);
    }

    /// <summary>A member whose membership renews in <paramref name="renewsInDays"/> days (negative: ended that long ago).</summary>
    private static async Task<Guid> MemberAsync(HttpClient client, string first, int renewsInDays, string status = "Active", decimal dues = 299m,
        string frequency = "Monthly") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status,
            joinedAtUtc = DateTime.UtcNow.AddYears(-2), renewsAtUtc = DateTime.UtcNow.AddDays(renewsInDays), duesAmount = dues, duesFrequency = frequency,
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task<JsonElement> BoardAsync(HttpClient client, string query = "") =>
        await Json(await client.GetAsync($"/api/renewals{query}"), "read the board");

    private static JsonElement Column(JsonElement board, string stage) =>
        board.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("stage").GetString() == stage);

    private static IEnumerable<JsonElement> Cards(JsonElement board) =>
        board.GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("cards").EnumerateArray());

    private static JsonElement Card(JsonElement board, Guid memberId) =>
        Cards(board).Single(c => c.GetProperty("memberId").GetGuid() == memberId);

    private static async Task<Guid> SubscriptionAsync(CustomWebApplicationFactory factory, Guid org, Guid memberId, string cadence, int nextInDays, decimal amount = 1200m)
    {
        using var scope = factory.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.ClubSubscriptions.Add(new ClubSubscription
        {
            Id = id, OrganizationId = org, MemberId = memberId, MemberName = "Keller", Name = $"Gold dues ({cadence})", Amount = amount, Currency = "CHF",
            Cadence = cadence, Status = "Active", StartedAtUtc = now.AddYears(-1), NextChargeAtUtc = now.AddDays(nextInDays), CreatedAtUtc = now.AddYears(-1),
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>The daily run's notes of the renewals coming up, acting for the workspace as the run does.</summary>
    private static async Task TrackAsync(CustomWebApplicationFactory factory, Guid org)
    {
        using var scope = factory.Services.CreateScope();
        using (scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(org, null))
            await scope.ServiceProvider.GetRequiredService<IClubRenewals>().TrackUpcomingAsync(org, DateTime.UtcNow);
    }

    // ── the board ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheBoard_ShowsWhoseMembershipEndsInTheWindow_InTheColumnTheRecordsPutThemIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var ben = await MemberAsync(owner, "Ben", 30, dues: 3000m, frequency: "Annual");
        var clara = await MemberAsync(owner, "Clara", 40);
        var dario = await MemberAsync(owner, "Dario", -10);
        var eva = await MemberAsync(owner, "Eva", 100);
        var fritz = await MemberAsync(owner, "Fritz", 5, status: "Lapsed");
        var gina = await MemberAsync(owner, "Gina", 10, status: "Pending");
        // Somebody wrote to Ben; Clara's score says she is at risk.
        await Json(await owner.PostAsync("/api/communications", Body(new { memberId = ben, channel = "Email", category = "Other", subject = "Your renewal" })), "log a message to Ben");
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            db.ClubMemberRisks.Add(new ClubMemberRisk
            {
                Id = Guid.NewGuid(), OrganizationId = org, MemberId = clara, Score = 72, Level = RiskLevels.AtRisk, ComputedAtUtc = DateTime.UtcNow,
                AtRiskSinceUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var board = await BoardAsync(owner);

        board.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("stage").GetString())
            .Should().Equal(RenewalStages.Upcoming, RenewalStages.Contacted, RenewalStages.AtRisk, RenewalStages.Renewed, RenewalStages.Lapsed);
        Cards(board).Select(c => c.GetProperty("memberId").GetGuid()).Should().BeEquivalentTo(new[] { anna, ben, clara, dario, fritz },
            "Eva renews after the 60 days, and Gina is not a member yet");
        Cards(board).Should().NotContain(c => c.GetProperty("memberId").GetGuid() == eva || c.GetProperty("memberId").GetGuid() == gina);

        var annaCard = Card(board, anna);
        annaCard.GetProperty("stage").GetString().Should().Be(RenewalStages.Upcoming);
        annaCard.GetProperty("stageReason").GetString().Should().Be("Nobody has been in touch about it yet");
        (annaCard.GetProperty("ownerUserId").GetGuid(), annaCard.GetProperty("ownerName").GetString()).Should().Be((ownerId, "owner@pavillon46.ch"));
        annaCard.GetProperty("annualValue").GetDecimal().Should().Be(299m * 12, "monthly dues of 299 bring in 3,588 a year");
        annaCard.GetProperty("daysLeft").GetInt32().Should().BeInRange(19, 20);
        var benCard = Card(board, ben);
        (benCard.GetProperty("stage").GetString(), benCard.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Contacted, "Someone wrote to them"));
        benCard.GetProperty("lastContactKind").GetString().Should().Be("message");
        benCard.GetProperty("lastContactAtUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        benCard.GetProperty("annualValue").GetDecimal().Should().Be(3000m);
        var claraCard = Card(board, clara);
        (claraCard.GetProperty("stage").GetString(), claraCard.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.AtRisk, "At-risk score 72"));
        claraCard.GetProperty("riskScore").GetInt32().Should().Be(72);
        Card(board, dario).GetProperty("stageReason").GetString().Should().Be("Ended without a renewal");
        Card(board, fritz).GetProperty("stageReason").GetString().Should().Be("The member is lapsed");
        Column(board, RenewalStages.Lapsed).GetProperty("count").GetInt32().Should().Be(2);
        var upcomingTotal = Column(board, RenewalStages.Upcoming).GetProperty("totals").EnumerateArray().Single();
        (upcomingTotal.GetProperty("currency").GetString(), upcomingTotal.GetProperty("amount").GetDecimal()).Should().Be(("CHF", 3588m));

        // A longer window takes Eva in.
        Cards(await BoardAsync(owner, "?days=120")).Should().Contain(c => c.GetProperty("memberId").GetGuid() == eva);
        (await owner.GetAsync("/api/renewals?days=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkingAMembershipRenewed_MovesItsEndOnAYear_Once_AndTheCardStaysOnTheBoardAsRenewed()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var card = Card(await BoardAsync(owner), anna);
        var endsAt = card.GetProperty("endsAtUtc").GetDateTime();

        var renewed = await Json(await owner.PostAsync("/api/renewals/stage", Body(new
        {
            memberId = anna, source = "membership", endsAtUtc = card.GetProperty("endsAtUtc").GetString(), stage = "renewed", note = "Paid at the bar",
        })), "mark it renewed");

        (renewed.GetProperty("stage").GetString(), renewed.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Renewed, "Renewed by owner@pavillon46.ch"));
        renewed.GetProperty("renewedUntilUtc").GetDateTime().Should().Be(endsAt.AddYears(1));
        var moved = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc);
        moved.Should().Be(endsAt.AddYears(1));

        // Pressed again (from the card it answered with): the date stays where it is.
        var again = await Json(await owner.PostAsync("/api/renewals/stage", Body(new
        {
            memberId = anna, source = "membership", endsAtUtc = card.GetProperty("endsAtUtc").GetString(), stage = "renewed",
            version = renewed.GetProperty("version").GetGuid(),
        })), "press it again");
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc)).Should().Be(endsAt.AddYears(1), "a renewal moves the date once");
        var board = await BoardAsync(owner);
        Card(board, anna).GetProperty("stage").GetString().Should().Be(RenewalStages.Renewed, "the renewal stays on the board for the season it was due in");
        Card(board, anna).GetProperty("note").GetString().Should().Be("Paid at the bar");

        // A change sent from the first copy, after the second press, is refused.
        (await owner.PostAsync("/api/renewals/stage", Body(new
        {
            memberId = anna, source = "membership", endsAtUtc = card.GetProperty("endsAtUtc").GetString(), stage = "lapsed",
            version = renewed.GetProperty("version").GetGuid(),
        }))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        again.GetProperty("version").GetGuid().Should().NotBe(renewed.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task AStageSetByHand_OutranksTheRecords_UntilItIsGivenBackToThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var endsAt = Card(await BoardAsync(owner), anna).GetProperty("endsAtUtc").GetString();

        var contacted = await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "contacted" })), "mark contacted");
        contacted.GetProperty("stage").GetString().Should().Be(RenewalStages.Contacted);
        contacted.GetProperty("stageSetByHand").GetBoolean().Should().BeTrue();
        var atRisk = await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "at_risk" })), "mark at risk");
        atRisk.GetProperty("stageReason").GetString().Should().Be("Marked at risk by owner@pavillon46.ch");

        var auto = await Json(await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "auto" })), "let the board decide");
        (auto.GetProperty("stage").GetString(), auto.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Contacted, "Marked contacted"),
            "having been marked contacted is a contact the board still knows of");
        auto.GetProperty("stageSetByHand").GetBoolean().Should().BeFalse();

        (await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "maybe" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "renewed", renewUntilUtc = DateTime.UtcNow })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "renewed to a date before the end");
        (await owner.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = DateTime.UtcNow.AddDays(3), stage = "contacted" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "no renewal of hers ends then");
    }

    [Fact]
    public async Task ARenewalMadeOnTheMembersForm_ShowsAsRenewed_OnceTheDailyRunHasNotedIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var endsAt = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc);
        await TrackAsync(factory, org);
        await TrackAsync(factory, org);
        (await factory.Services.Read(db => db.ClubRenewals.Count())).Should().Be(1, "the daily run notes each renewal once");

        await Json(await owner.PutAsync($"/api/members/{anna}", Body(new { renewsAtUtc = endsAt.AddMonths(13) })), "renew on the member's form, to a date of its own");

        var card = Card(await BoardAsync(owner), anna);
        (card.GetProperty("stage").GetString(), card.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Renewed, "The renewal date has moved on"));
        card.GetProperty("endsAtUtc").GetDateTime().Should().Be(endsAt);

        // Marked renewed on the board as well: the date the form set stays.
        var marked = await Json(await owner.PostAsync("/api/renewals/stage", Body(new
        {
            memberId = anna, endsAtUtc = card.GetProperty("endsAtUtc").GetString(), stage = "renewed",
        })), "mark it renewed too");
        marked.GetProperty("renewedUntilUtc").GetDateTime().Should().Be(endsAt.AddMonths(13));
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc)).Should().Be(endsAt.AddMonths(13),
            "the board does not move a renewal made on the member's form back to a year from the old end");
    }

    [Fact]
    public async Task WhatTheClubSendsOnItsOwn_IsNoContact_ButAMessageSomeoneWritesIs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        // The month's statement, a payment reminder and the retention rules' own email went to
        // her, as they go to every member whoever looks after them.
        using (var scope = factory.Services.CreateScope())
        await using (var db = scope.ServiceProvider.EveryWorkspace())
        {
            foreach (var source in new[] { OutboundSources.Statement, OutboundSources.PaymentReminder, OutboundSources.Retention })
            {
                var outbound = new OutboundMessage
                {
                    Id = Guid.NewGuid(), OrganizationId = org, Source = source, Status = OutboundStatuses.Sent, To = "anna@example.ch",
                    Subject = source, CreatedAtUtc = DateTime.UtcNow, SentAtUtc = DateTime.UtcNow,
                };
                db.OutboundMessages.Add(outbound);
                db.MemberMessages.Add(new MemberMessage
                {
                    Id = Guid.NewGuid(), OrganizationId = org, MemberId = anna, MemberName = "Anna Keller", Email = "anna@example.ch",
                    Channel = CampaignChannels.Email, Category = "Dues", Purpose = ConsentPurposes.Service, Subject = source,
                    Status = MemberMessageStatuses.Queued, OutboundMessageId = outbound.Id, CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var card = Card(await BoardAsync(owner), anna);
        (card.GetProperty("stage").GetString(), card.GetProperty("stageReason").GetString())
            .Should().Be((RenewalStages.Upcoming, "Nobody has been in touch about it yet"), "a statement or a reminder is nobody getting in touch about the renewal");
        card.TryGetProperty("lastContactAtUtc", out _).Should().BeFalse();

        // Someone writes to her from her page.
        await Json(await owner.PostAsync("/api/communications/send", Body(new
        {
            memberId = anna, purpose = "service", subject = "Your renewal", body = "Shall we renew you for another year?",
        })), "write to Anna");
        var written = Card(await BoardAsync(owner), anna);
        (written.GetProperty("stage").GetString(), written.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Contacted, "Someone wrote to them"));
        written.GetProperty("lastContactKind").GetString().Should().Be("message");
    }

    [Fact]
    public async Task ARenewalDateCorrected_MovesTheCardToTheNewEnd_InsteadOfLeavingTheOldOneBehind()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var ben = await MemberAsync(owner, "Ben", 10);
        var annaEnd = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc);
        var benEnd = await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == ben).RenewsAtUtc);
        // The daily run notes both renewals; then their dates are put right on the members' forms:
        // Anna's five days later, Ben's to the date his membership really ended, a month back.
        await TrackAsync(factory, org);
        await Json(await owner.PutAsync($"/api/members/{anna}", Body(new { renewsAtUtc = annaEnd.AddDays(5) })), "correct Anna's date");
        await Json(await owner.PutAsync($"/api/members/{ben}", Body(new { renewsAtUtc = benEnd.AddDays(-30) })), "correct Ben's date");

        var board = await BoardAsync(owner);

        var annas = Cards(board).Where(c => c.GetProperty("memberId").GetGuid() == anna).ToList();
        annas.Should().ContainSingle("a date put right is the same renewal, not a second one");
        annas[0].GetProperty("endsAtUtc").GetDateTime().Should().Be(annaEnd.AddDays(5));
        annas[0].GetProperty("stage").GetString().Should().Be(RenewalStages.Upcoming);
        var bens = Cards(board).Where(c => c.GetProperty("memberId").GetGuid() == ben).ToList();
        bens.Should().ContainSingle();
        (bens[0].GetProperty("endsAtUtc").GetDateTime(), bens[0].GetProperty("stage").GetString()).Should().Be((benEnd.AddDays(-30), RenewalStages.Lapsed));
        Column(board, RenewalStages.Upcoming).GetProperty("count").GetInt32().Should().Be(1, "Ben's old date is not counted as upcoming as well");
    }

    [Fact]
    public async Task ASubscriptionPeriodBilledAndPaid_ShowsAsRenewed_AndMonthlyOnesShowOnlyWhenAsked()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var hugo = await MemberAsync(owner, "Hugo", 200);
        var ivo = await MemberAsync(owner, "Ivo", 200);
        var yearly = await SubscriptionAsync(factory, org, hugo, "Annual", 15);
        await SubscriptionAsync(factory, org, ivo, "Monthly", 10, 99m);

        var board = await BoardAsync(owner);
        var card = Card(board, hugo);
        (card.GetProperty("source").GetString(), card.GetProperty("subscriptionId").GetGuid()).Should().Be((RenewalSources.Subscription, yearly));
        card.GetProperty("stage").GetString().Should().Be(RenewalStages.Upcoming);
        card.GetProperty("annualValue").GetDecimal().Should().Be(1200m);
        Cards(board).Should().NotContain(c => c.GetProperty("memberId").GetGuid() == ivo, "a monthly period is not a renewal worth a card");
        Card(await BoardAsync(owner, "?monthly=true"), ivo).GetProperty("cadence").GetString().Should().Be("Monthly");

        // The daily run notes the period; finance bills it early and Hugo pays.
        await TrackAsync(factory, org);
        await Json(await owner.PostAsync($"/api/subscriptions/{yearly}/bill", Body(new { early = true })), "bill the next year early");
        Card(await BoardAsync(owner), hugo).GetProperty("stage").GetString().Should().Be(RenewalStages.Upcoming, "billed is not paid");
        var charge = await factory.Services.Read(db => db.ClubCharges.Single(c => c.SubscriptionId == yearly));
        await Json(await owner.PostAsync("/api/payments", Body(new { chargeId = charge.Id, amount = 1200m, method = "Wire" })), "Hugo pays");

        var paid = Card(await BoardAsync(owner), hugo);
        (paid.GetProperty("stage").GetString(), paid.GetProperty("stageReason").GetString()).Should().Be((RenewalStages.Renewed, "The next period is billed and paid"));
    }

    [Fact]
    public async Task EachOwnerCanSeeTheirOwnMembersRenewals()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var (director, directorId) = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        var anna = await MemberAsync(owner, "Anna", 20);
        var ben = await MemberAsync(director, "Ben", 25);

        var mine = await BoardAsync(director, "?owner=me");
        Cards(mine).Select(c => c.GetProperty("memberId").GetGuid()).Should().Equal(ben);
        Cards(await BoardAsync(owner, $"?owner={directorId}")).Select(c => c.GetProperty("memberId").GetGuid()).Should().Equal(ben);
        Cards(await BoardAsync(director)).Select(c => c.GetProperty("memberId").GetGuid()).Should().BeEquivalentTo(new[] { anna, ben });
    }

    [Fact]
    public async Task AnotherWorkspacesRenewals_AreNeitherShownNorMarked()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await ClubAsync(factory);
        var anna = await MemberAsync(owner, "Anna", 20);
        var card = Card(await BoardAsync(owner), anna);
        var endsAt = card.GetProperty("endsAtUtc").GetString();
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@elsewhere.ch");
        stranger.In(await CreateAsync(factory, strangerId, "Elsewhere"));

        Cards(await BoardAsync(stranger)).Should().BeEmpty();
        (await stranger.PostAsync("/api/renewals/stage", Body(new { memberId = anna, endsAtUtc = endsAt, stage = "renewed" }))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await factory.Services.Read(db => db.ClubRenewals.Count())).Should().Be(0);
        (await factory.Services.Read(db => db.ClubMembers.Single(m => m.Id == anna).RenewsAtUtc)).Should().Be(card.GetProperty("endsAtUtc").GetDateTime());
    }
}
